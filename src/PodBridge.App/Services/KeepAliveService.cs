using System.Runtime.InteropServices;
using PodBridge.App.Native;

namespace PodBridge.App.Services;

/// <summary>
/// Plays inaudible silence (real zero samples) to one endpoint through WASAPI shared mode, so that Bluetooth
/// headphones do not go to sleep and clip the start of the next sound. Runs on its own thread.
/// Uses hand-written IAudioClient / IAudioRenderClient interop; no audio library.
/// </summary>
internal sealed class KeepAliveService : IDisposable
{
    private static readonly TimeSpan RetryAfterFailure = TimeSpan.FromSeconds(30);

    private readonly AudioService _audio;
    private readonly object _gate = new();
    private Thread? _thread;
    private ManualResetEventSlim? _stop;
    private string? _endpointId;
    private DateTime _lastFailureUtc = DateTime.MinValue;

    public KeepAliveService(AudioService audio) => _audio = audio;

    public bool IsRunning
    {
        get { lock (_gate) { return _thread is { IsAlive: true }; } }
    }

    /// <summary>Starts (or keeps running, or moves to a different endpoint) the silent stream.</summary>
    public void EnsureRunning(string endpointId)
    {
        lock (_gate)
        {
            if (_thread is { IsAlive: true } && _endpointId == endpointId)
            {
                return;
            }
            StopLocked();

            if (DateTime.UtcNow - _lastFailureUtc < RetryAfterFailure)
            {
                return; // do not hammer a device that just refused us
            }

            var stop = new ManualResetEventSlim(false);
            var thread = new Thread(() => Run(endpointId, stop))
            {
                IsBackground = true,
                Name = "PodBridge keep-alive",
            };
            _stop = stop;
            _endpointId = endpointId;
            _thread = thread;
            thread.Start();
        }
    }

    public void Stop()
    {
        lock (_gate)
        {
            StopLocked();
        }
    }

    private void StopLocked()
    {
        var thread = _thread;
        var stop = _stop;
        _thread = null;
        _stop = null;
        _endpointId = null;
        if (thread is null || stop is null)
        {
            return;
        }
        stop.Set();
        // The loop wakes every 50 ms; wait briefly so the endpoint is really released before it is switched away.
        if (thread != Thread.CurrentThread)
        {
            thread.Join(TimeSpan.FromSeconds(2));
        }
    }

    private void Run(string endpointId, ManualResetEventSlim stop)
    {
        IMMDevice? device = null;
        object? clientObject = null;
        object? renderObject = null;
        IntPtr format = IntPtr.Zero;
        IAudioClient? client = null;
        bool started = false;
        try
        {
            device = _audio.OpenDevice(endpointId);
            var iidClient = AudioInterop.IID_IAudioClient;
            device.Activate(ref iidClient, AudioInterop.CLSCTX_ALL, IntPtr.Zero, out clientObject);
            client = (IAudioClient)clientObject;

            Check(client.GetMixFormat(out format), "GetMixFormat");
            // WAVEFORMATEX: nBlockAlign (bytes per frame) is the ushort at offset 12.
            int blockAlign = Marshal.ReadInt16(format, 12);
            if (blockAlign <= 0)
            {
                throw new InvalidOperationException("Unexpected mix format.");
            }

            // 200 ms buffer, shared mode, the engine's own mix format, so no format negotiation can fail.
            Check(client.Initialize(AudioInterop.AUDCLNT_SHAREMODE_SHARED, 0, 2_000_000, 0, format, IntPtr.Zero), "Initialize");
            Check(client.GetBufferSize(out uint bufferFrames), "GetBufferSize");

            var iidRender = AudioInterop.IID_IAudioRenderClient;
            Check(client.GetService(ref iidRender, out renderObject), "GetService");
            var render = (IAudioRenderClient)renderObject;

            var zeros = new byte[checked((int)bufferFrames * blockAlign)];

            Fill(render, bufferFrames, blockAlign, zeros); // prime the buffer before starting
            Check(client.Start(), "Start");
            started = true;

            while (!stop.Wait(50))
            {
                Check(client.GetCurrentPadding(out uint padding), "GetCurrentPadding");
                uint free = bufferFrames - padding;
                if (free > 0)
                {
                    Fill(render, free, blockAlign, zeros);
                }
            }
        }
        catch (Exception ex)
        {
            if (!stop.IsSet)
            {
                // Device removed, format refused, exclusive-mode app, and so on. The next refresh retries after a pause.
                _lastFailureUtc = DateTime.UtcNow;
                Log.Error("Keep-alive stopped", ex);
            }
        }
        finally
        {
            if (started && client is not null)
            {
                try { client.Stop(); } catch { /* device may already be gone */ }
            }
            if (format != IntPtr.Zero)
            {
                AudioInterop.CoTaskMemFree(format);
            }
            AudioInterop.Release(renderObject);
            AudioInterop.Release(clientObject);
            AudioInterop.Release(device);
        }
    }

    private static void Fill(IAudioRenderClient render, uint frames, int blockAlign, byte[] zeros)
    {
        Check(render.GetBuffer(frames, out IntPtr data), "GetBuffer");
        Marshal.Copy(zeros, 0, data, checked((int)frames * blockAlign));
        Check(render.ReleaseBuffer(frames, 0), "ReleaseBuffer");
    }

    private static void Check(int hr, string what)
    {
        if (hr < 0)
        {
            throw new COMException($"{what} failed", hr);
        }
    }

    public void Dispose() => Stop();
}
