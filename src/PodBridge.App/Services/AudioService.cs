using System.Runtime.InteropServices;
using PodBridge.App.Native;
using PodBridge.Core.Audio;

namespace PodBridge.App.Services;

/// <summary>
/// Windows audio endpoints through the MMDevice API (hand-written COM interop, see Native/AudioInterop.cs).
/// Methods can be called from any thread. Events are raised on Windows' own threads:
/// the caller must hop to the UI thread before touching UI.
///
/// Threading: every query creates its own short-lived MMDeviceEnumerator on the calling thread, so a COM object
/// created on the UI (STA) thread is never used from a worker (MTA) thread or the other way round.
/// Only the enumerator that holds the notification registration lives long, and it is used on the UI thread only.
/// Nothing here throws: failures are logged and reported as empty / null / false.
/// </summary>
internal sealed class AudioService : IDisposable
{
    private readonly NotificationClient _client;
    private IMMDeviceEnumerator? _notifyEnumerator;
    private bool _registered;
    private bool _disposed;

    public AudioService()
    {
        // No COM work here: constructing the service can never fail.
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
            _notifyEnumerator ??= CreateEnumerator();
            _notifyEnumerator.RegisterEndpointNotificationCallback(_client);
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
        IMMDeviceEnumerator? enumerator = null;
        IMMDeviceCollection? collection = null;
        try
        {
            enumerator = CreateEnumerator();
            enumerator.EnumAudioEndpoints(EDataFlow.Render, mask, out collection);
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
                catch (Exception ex)
                {
                    Log.Error("Could not read an audio endpoint", ex);
                }
                finally
                {
                    AudioInterop.Release(device);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error("Could not list audio endpoints", ex);
        }
        finally
        {
            AudioInterop.Release(collection);
            AudioInterop.Release(enumerator);
        }
        return result;
    }

    /// <summary>The id of the default playback endpoint (console role), or null if there is none.</summary>
    public string? GetDefaultEndpointId()
    {
        IMMDeviceEnumerator? enumerator = null;
        IMMDevice? device = null;
        try
        {
            enumerator = CreateEnumerator();
            enumerator.GetDefaultAudioEndpoint(EDataFlow.Render, ERole.Console, out device);
            device.GetId(out string id);
            return id;
        }
        catch (COMException)
        {
            return null; // E_NOTFOUND when no playback device exists
        }
        catch (Exception ex)
        {
            Log.Error("Could not read the default output", ex);
            return null;
        }
        finally
        {
            AudioInterop.Release(device);
            AudioInterop.Release(enumerator);
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
        catch (Exception ex)
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
        IMMDeviceEnumerator? enumerator = null;
        IMMDevice? device = null;
        object? volumeObject = null;
        try
        {
            enumerator = CreateEnumerator();
            if (endpointId is null)
            {
                enumerator.GetDefaultAudioEndpoint(EDataFlow.Render, ERole.Console, out device);
            }
            else
            {
                enumerator.GetDevice(endpointId, out device);
            }
            var iid = AudioInterop.IID_IAudioEndpointVolume;
            device.Activate(ref iid, AudioInterop.CLSCTX_ALL, IntPtr.Zero, out volumeObject);
            return action((IAudioEndpointVolume)volumeObject);
        }
        catch (COMException)
        {
            return null;
        }
        catch (Exception ex)
        {
            Log.Error("Volume control failed", ex);
            return null;
        }
        finally
        {
            AudioInterop.Release(volumeObject);
            AudioInterop.Release(device);
            AudioInterop.Release(enumerator);
        }
    }

    /// <summary>Friendly name of an endpoint, or null if it is gone.</summary>
    public string? GetName(string endpointId)
    {
        IMMDeviceEnumerator? enumerator = null;
        IMMDevice? device = null;
        try
        {
            enumerator = CreateEnumerator();
            enumerator.GetDevice(endpointId, out device);
            return AudioInterop.ReadFriendlyName(device);
        }
        catch (Exception)
        {
            return null;
        }
        finally
        {
            AudioInterop.Release(device);
            AudioInterop.Release(enumerator);
        }
    }

    /// <summary>
    /// Opens an endpoint (or a device-topology device id) on the calling thread.
    /// The caller releases it with <see cref="AudioInterop.Release"/>. Throws on failure.
    /// </summary>
    internal IMMDevice OpenDevice(string endpointId)
    {
        var enumerator = CreateEnumerator();
        try
        {
            enumerator.GetDevice(endpointId, out var device);
            return device;
        }
        finally
        {
            AudioInterop.Release(enumerator);
        }
    }

    /// <summary>
    /// Ids of every endpoint (playback and recording, any state) that belongs to the same physical device as
    /// <paramref name="endpointId"/>, found through PKEY_Device_ContainerId. Always contains endpointId itself, first.
    /// </summary>
    internal IReadOnlyList<string> FindSameDeviceEndpoints(string endpointId)
    {
        var result = new List<string> { endpointId };
        IMMDeviceEnumerator? enumerator = null;
        IMMDevice? self = null;
        IMMDeviceCollection? collection = null;
        try
        {
            enumerator = CreateEnumerator();
            enumerator.GetDevice(endpointId, out self);
            var container = AudioInterop.ReadContainerId(self);
            if (container is not Guid wanted || wanted == Guid.Empty)
            {
                return result;
            }

            enumerator.EnumAudioEndpoints(EDataFlow.All, AudioInterop.DEVICE_STATEMASK_ALL, out collection);
            collection.GetCount(out uint count);
            for (uint i = 0; i < count; i++)
            {
                IMMDevice? device = null;
                try
                {
                    collection.Item(i, out device);
                    device.GetId(out string id);
                    if (id != endpointId && AudioInterop.ReadContainerId(device) == wanted)
                    {
                        result.Add(id);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error("Could not read an audio endpoint", ex);
                }
                finally
                {
                    AudioInterop.Release(device);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error("Could not find the endpoints of the same device", ex);
        }
        finally
        {
            AudioInterop.Release(collection);
            AudioInterop.Release(self);
            AudioInterop.Release(enumerator);
        }
        return result;
    }

    private static IMMDeviceEnumerator CreateEnumerator() =>
        (IMMDeviceEnumerator)new MMDeviceEnumeratorCoClass();

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        if (_registered && _notifyEnumerator is not null)
        {
            try { _notifyEnumerator.UnregisterEndpointNotificationCallback(_client); } catch { /* shutting down */ }
            _registered = false;
        }
        AudioInterop.Release(_notifyEnumerator);
        _notifyEnumerator = null;
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
