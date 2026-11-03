using System.Runtime.InteropServices;
using PodBridge.App.Native;

namespace PodBridge.App.Services;

/// <summary>
/// Connects and disconnects Bluetooth headphones' audio link.
///
/// Windows has no public API for this. The Bluetooth audio driver answers two "one-shot" kernel-streaming
/// properties (KSPROPSETID_BtAudio: KSPROPERTY_ONESHOT_RECONNECT and KSPROPERTY_ONESHOT_DISCONNECT, both GET-only
/// with no value buffer, see ksmedia.h and the WDK docs). We reach the driver's KS filter like this
/// (the same path as the MIT-licensed ToothTray and the yasb Bluetooth widget):
///   every endpoint of the headphones (same PKEY_Device_ContainerId: the music endpoint and the call endpoint)
///   -> IDeviceTopology -> each connector -> IConnector::GetDeviceIdConnectedTo (the Bluetooth adapter's KS filter,
///   "{2}.\\?\bthenum#..." or "...\bthhfenum#...") -> IMMDeviceEnumerator::GetDevice(that id)
///   -> IMMDevice::Activate(IKsControl) -> IKsControl::KsProperty.
/// The property is sent to every filter found, so both the music (A2DP) and the call (hands-free) links follow.
/// This is undocumented behaviour: it may not work with every driver or Windows build.
/// Runs on a worker thread; never throws.
/// </summary>
internal sealed class BluetoothAudioControl
{
    private readonly AudioService _audio;

    public BluetoothAudioControl(AudioService audio) => _audio = audio;

    /// <summary>Asks the headphones' Bluetooth audio link to connect. Returns null on success, else a message for the user.</summary>
    public string? Connect(string endpointId) =>
        Send(endpointId, AudioInterop.KSPROPERTY_ONESHOT_RECONNECT, "connect");

    /// <summary>Asks the headphones' Bluetooth audio link to disconnect. Returns null on success, else a message for the user.</summary>
    public string? Disconnect(string endpointId) =>
        Send(endpointId, AudioInterop.KSPROPERTY_ONESHOT_DISCONNECT, "disconnect");

    private string? Send(string endpointId, uint propertyId, string verb)
    {
        try
        {
            var filters = FindBluetoothFilters(endpointId);
            if (filters.Count == 0)
            {
                Log.Error($"Bluetooth {verb}: no Bluetooth audio filter found for endpoint {endpointId}");
                return $"Could not reach the Bluetooth audio driver for this device, so PodBridge cannot {verb} it. Use the Bluetooth settings page instead.";
            }

            int lastHr = 0;
            bool anySucceeded = false;
            foreach (var filterId in filters)
            {
                int hr = SendToFilter(filterId, propertyId);
                if (hr >= 0)
                {
                    anySucceeded = true;
                }
                else
                {
                    lastHr = hr;
                    Log.Error($"Bluetooth {verb} refused by {filterId} (hr=0x{hr:X8})");
                }
            }

            if (anySucceeded)
            {
                return null;
            }
            return $"Windows or the Bluetooth driver did not accept the {verb} request (error 0x{lastHr:X8}). Try the Bluetooth settings page instead.";
        }
        catch (Exception ex)
        {
            Log.Error($"Bluetooth {verb} failed", ex);
            return $"Windows could not {verb} the headphones (error 0x{ex.HResult:X8}). Try the Bluetooth settings page instead.";
        }
    }

    /// <summary>Opens one KS filter and sends the one-shot property. Returns the HRESULT.</summary>
    private int SendToFilter(string filterId, uint propertyId)
    {
        IMMDevice? device = null;
        object? controlObject = null;
        try
        {
            device = _audio.OpenDevice(filterId);
            var iid = AudioInterop.IID_IKsControl;
            device.Activate(ref iid, AudioInterop.CLSCTX_ALL, IntPtr.Zero, out controlObject);
            var control = (IKsControl)controlObject;

            var property = new KsPropertyHeader
            {
                Set = AudioInterop.KSPROPSETID_BtAudio,
                Id = propertyId,
                Flags = AudioInterop.KSPROPERTY_TYPE_GET,
            };
            return control.KsProperty(ref property, (uint)Marshal.SizeOf<KsPropertyHeader>(), IntPtr.Zero, 0, out _);
        }
        catch (Exception ex)
        {
            Log.Error($"Could not open the Bluetooth audio filter {filterId}", ex);
            return ex.HResult < 0 ? ex.HResult : unchecked((int)0x80004005); // E_FAIL
        }
        finally
        {
            AudioInterop.Release(controlObject);
            AudioInterop.Release(device);
        }
    }

    /// <summary>The distinct Bluetooth KS filter device ids wired to any endpoint of the same headphones.</summary>
    private List<string> FindBluetoothFilters(string endpointId)
    {
        var filters = new List<string>();
        foreach (var id in _audio.FindSameDeviceEndpoints(endpointId))
        {
            IMMDevice? device = null;
            object? topologyObject = null;
            try
            {
                device = _audio.OpenDevice(id);
                var iid = AudioInterop.IID_IDeviceTopology;
                device.Activate(ref iid, AudioInterop.CLSCTX_ALL, IntPtr.Zero, out topologyObject);
                var topology = (IDeviceTopology)topologyObject;
                topology.GetConnectorCount(out uint count);
                for (uint i = 0; i < count; i++)
                {
                    IConnector? connector = null;
                    try
                    {
                        topology.GetConnector(i, out connector);
                        connector.GetDeviceIdConnectedTo(out string otherId); // E_NOTFOUND if not wired: caught below
                        if (!string.IsNullOrEmpty(otherId)
                            && otherId.Contains(@"\\?\bth", StringComparison.OrdinalIgnoreCase) // bthenum / bthhfenum
                            && !filters.Contains(otherId, StringComparer.OrdinalIgnoreCase))
                        {
                            filters.Add(otherId);
                        }
                    }
                    catch (Exception)
                    {
                        // Connector not connected to anything: skip it.
                    }
                    finally
                    {
                        AudioInterop.Release(connector);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Could not read the device topology of {id}", ex);
            }
            finally
            {
                AudioInterop.Release(topologyObject);
                AudioInterop.Release(device);
            }
        }
        return filters;
    }
}
