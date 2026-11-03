using System.Runtime.InteropServices;
using PodBridge.App.Native;
using PodBridge.Core.Audio;

namespace PodBridge.App.Services;

/// <summary>
/// Windows audio endpoints through the MMDevice API (hand-written COM interop, see Native/AudioInterop.cs).
/// Methods can be called from any thread. Events are raised on Windows' own threads:
/// the caller must hop to the UI thread before touching UI.
/// </summary>
internal sealed class AudioService : IDisposable
{
    private readonly IMMDeviceEnumerator _enumerator;
    private readonly NotificationClient _client;
    private bool _registered;
    private bool _disposed;

    public AudioService()
    {
        _enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorCoClass();
        _client = new NotificationClient(this);
    }

    /// <summary>The default render (playback) device for the "console" role changed. Argument: new default id, or null.</summary>
    public event Action<string?>? DefaultRenderChanged;

    /// <summary>An endpoint was added, removed, or changed state (for example Bluetooth headphones connected).</summary>
    public event Action? EndpointsChanged;

    /// <summary>Starts listening for device changes. Returns false if Windows refused.</summary>
    public bool StartListening()
    {
        if (_registered)
        {
            return true;
        }
        try
        {
            _enumerator.RegisterEndpointNotificationCallback(_client);
            _registered = true;
            return true;
        }
        catch (Exception ex)
        {
            Log.Error("Could not register for audio device notifications", ex);
            return false;
        }
    }

    /// <summary>Playback endpoints with friendly names. Unplugged ones (for example disconnected Bluetooth headphones) are optional.</summary>
    public IReadOnlyList<EndpointInfo> ListRenderEndpoints(bool includeUnplugged)
    {
        var result = new List<EndpointInfo>();
        uint mask = AudioInterop.DEVICE_STATE_ACTIVE | (includeUnplugged ? AudioInterop.DEVICE_STATE_UNPLUGGED : 0);
        IMMDeviceCollection? collection = null;
        try
        {
            _enumerator.EnumAudioEndpoints(EDataFlow.Render, mask, out collection);
            collection.GetCount(out uint count);
            for (uint i = 0; i < count; i++)
            {
                IMMDevice? device = null;
                try
                {
                    collection.Item(i, out device);
                    device.GetId(out string id);
                    device.GetState(out uint state);
                    var name = AudioInterop.ReadFriendlyName(device) ?? id;
                    result.Add(new EndpointInfo(id, name, (EndpointState)state));
                }
                catch (COMException ex)
                {
                    Log.Error("Could not read an audio endpoint", ex);
                }
                finally
                {
                    AudioInterop.Release(device);
                }
            }
        }
        catch (COMException ex)
        {
            Log.Error("Could not list audio endpoints", ex);
        }
        finally
        {
            AudioInterop.Release(collection);
        }
        return result;
    }

    /// <summary>The id of the default playback endpoint (console role), or null if there is none.</summary>
    public string? GetDefaultEndpointId()
    {
        IMMDevice? device = null;
        try
        {
            _enumerator.GetDefaultAudioEndpoint(EDataFlow.Render, ERole.Console, out device);
            device.GetId(out string id);
            return id;
        }
        catch (COMException)
        {
            return null; // E_NOTFOUND when no playback device exists
        }
        finally
        {
            AudioInterop.Release(device);
        }
    }

    /// <summary>
    /// Makes an endpoint the default for all three roles (console, multimedia, communications)
    /// through the undocumented IPolicyConfig interface. Returns an error text, or null on success.
    /// </summary>
    public string? SetDefaultEndpoint(string endpointId)
    {
        object? obj = null;
        try
        {
            obj = new PolicyConfigClientCoClass();
            var policy = (IPolicyConfig)obj;
            foreach (var role in new[] { ERole.Console, ERole.Multimedia, ERole.Communications })
            {
                int hr = policy.SetDefaultEndpoint(endpointId, role);
                if (hr < 0)
                {
                    return $"Windows refused to change the default output (error 0x{hr:X8}, role {role}).";
                }
            }
            return null;
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException)
        {
            Log.Error("SetDefaultEndpoint failed", ex);
            return "This version of Windows does not support changing the default output this way.";
        }
        finally
        {
            AudioInterop.Release(obj);
        }
    }

    /// <summary>Master volume (0 to 1) of an endpoint, or of the default endpoint if no id is given. Null if unavailable.</summary>
    public float? GetVolume(string? endpointId = null)
    {
        return WithVolume(endpointId, volume =>
        {
            volume.GetMasterVolumeLevelScalar(out float level);
            return (float?)level;
        });
    }

    /// <summary>Sets the master volume (0 to 1). Returns false if it could not be set.</summary>
    public bool SetVolume(float level, string? endpointId = null)
    {
        level = Math.Clamp(level, 0f, 1f);
        return WithVolume(endpointId, volume =>
        {
            var context = Guid.Empty;
            volume.SetMasterVolumeLevelScalar(level, ref context);
            return (bool?)true;
        }) == true;
    }

    private T? WithVolume<T>(string? endpointId, Func<IAudioEndpointVolume, T?> action) where T : struct
    {
        IMMDevice? device = null;
        object? volumeObject = null;
        try
        {
            if (endpointId is null)
            {
                _enumerator.GetDefaultAudioEndpoint(EDataFlow.Render, ERole.Console, out device);
            }
            else
            {
                _enumerator.GetDevice(endpointId, out device);
            }
            var iid = AudioInterop.IID_IAudioEndpointVolume;
            device.Activate(ref iid, AudioInterop.CLSCTX_ALL, IntPtr.Zero, out volumeObject);
            return action((IAudioEndpointVolume)volumeObject);
        }
        catch (COMException)
        {
            return null;
        }
        finally
        {
            AudioInterop.Release(volumeObject);
            AudioInterop.Release(device);
        }
    }

    /// <summary>Friendly name of an endpoint, or null if it is gone.</summary>
    public string? GetName(string endpointId)
    {
        IMMDevice? device = null;
        try
        {
            _enumerator.GetDevice(endpointId, out device);
            return AudioInterop.ReadFriendlyName(device);
        }
        catch (COMException)
        {
            return null;
        }
        finally
        {
            AudioInterop.Release(device);
        }
    }

    /// <summary>Opens an endpoint. The caller releases it with <see cref="AudioInterop.Release"/>.</summary>
    internal IMMDevice OpenDevice(string endpointId)
    {
        _enumerator.GetDevice(endpointId, out var device);
        return device;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        if (_registered)
        {
            try { _enumerator.UnregisterEndpointNotificationCallback(_client); } catch { /* shutting down */ }
            _registered = false;
        }
        AudioInterop.Release(_enumerator);
    }

    private void RaiseDefaultChanged(string? id)
    {
        if (!_disposed)
        {
            try { DefaultRenderChanged?.Invoke(id); } catch (Exception ex) { Log.Error("Default-device handler failed", ex); }
        }
    }

    private void RaiseEndpointsChanged()
    {
        if (!_disposed)
        {
            try { EndpointsChanged?.Invoke(); } catch (Exception ex) { Log.Error("Endpoint handler failed", ex); }
        }
    }

    /// <summary>The COM callback object. Windows calls it on arbitrary threads and expects it to return quickly.</summary>
    private sealed class NotificationClient : IMMNotificationClient
    {
        private readonly AudioService _owner;

        public NotificationClient(AudioService owner) => _owner = owner;

        public void OnDeviceStateChanged(string deviceId, uint newState) => _owner.RaiseEndpointsChanged();

        public void OnDeviceAdded(string deviceId) => _owner.RaiseEndpointsChanged();

        public void OnDeviceRemoved(string deviceId) => _owner.RaiseEndpointsChanged();

        public void OnDefaultDeviceChanged(EDataFlow flow, ERole role, string? defaultDeviceId)
        {
            // Windows sends one callback per role. Playback + console is enough to know the output changed.
            if (flow == EDataFlow.Render && role == ERole.Console)
            {
                _owner.RaiseDefaultChanged(defaultDeviceId);
            }
        }

        public void OnPropertyValueChanged(string deviceId, PropertyKey key)
        {
            // Fires very often (for example on every volume change). Deliberately ignored.
        }
    }
}
