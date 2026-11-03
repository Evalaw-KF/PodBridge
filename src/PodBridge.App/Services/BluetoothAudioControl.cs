using System.Runtime.InteropServices;
using PodBridge.App.Native;

namespace PodBridge.App.Services;

/// <summary>
/// Connects and disconnects Bluetooth headphones' audio link.
///
/// Windows has no public API for this. The Bluetooth audio driver answers two "one-shot" kernel-streaming
/// properties (KSPROPSETID_BtAudio: KSPROPERTY_ONESHOT_RECONNECT and KSPROPERTY_ONESHOT_DISCONNECT).
/// We reach it like this:
///   endpoint (IMMDevice) -> IDeviceTopology -> its connector -> the connector it is wired to on the Bluetooth
///   adapter's topology -> IPart::Activate(IKsControl) -> IKsControl::KsProperty.
/// This is undocumented behaviour: it may not work with every driver or Windows build.
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
        IMMDevice? device = null;
        object? topologyObject = null;
        try
        {
            device = _audio.OpenDevice(endpointId);
            var iid = AudioInterop.IID_IDeviceTopology;
            device.Activate(ref iid, AudioInterop.CLSCTX_ALL, IntPtr.Zero, out topologyObject);
            var topology = (IDeviceTopology)topologyObject;

            int lastHr = 0;
            bool foundControl = false;
            foreach (var control in EnumerateControls(topology))
            {
                try
                {
                    foundControl = true;
                    // The one-shot properties carry no data, so they are sent as a GET with empty buffers.
                    lastHr = Invoke(control, propertyId, AudioInterop.KSPROPERTY_TYPE_GET);
                    if (lastHr < 0)
                    {
                        lastHr = Invoke(control, propertyId, AudioInterop.KSPROPERTY_TYPE_SET);
                    }
                    if (lastHr >= 0)
                    {
                        return null;
                    }
                }
                finally
                {
                    AudioInterop.Release(control);
                }
            }

            Log.Error($"Bluetooth {verb} failed (hr=0x{lastHr:X8}, control found: {foundControl})");
            return foundControl
                ? $"Windows or the Bluetooth driver did not accept the {verb} request (error 0x{lastHr:X8}). Try the Bluetooth settings page instead."
                : $"Could not reach the Bluetooth audio driver for this device, so PodBridge cannot {verb} it. Use the Bluetooth settings page instead.";
        }
        catch (COMException ex)
        {
            Log.Error($"Bluetooth {verb} failed", ex);
            return $"Windows could not {verb} the headphones (error 0x{ex.HResult:X8}). Try the Bluetooth settings page instead.";
        }
        catch (InvalidCastException ex)
        {
            Log.Error($"Bluetooth {verb} failed", ex);
            return $"Windows did not expose the controls PodBridge needs to {verb} these headphones.";
        }
        finally
        {
            AudioInterop.Release(topologyObject);
            AudioInterop.Release(device);
        }
    }

    private static int Invoke(IKsControl control, uint propertyId, uint flags)
    {
        var property = new KsPropertyHeader
        {
            Set = AudioInterop.KSPROPSETID_BtAudio,
            Id = propertyId,
            Flags = flags,
        };
        return control.KsProperty(ref property, (uint)Marshal.SizeOf<KsPropertyHeader>(), IntPtr.Zero, 0, out _);
    }

    /// <summary>
    /// Yields IKsControl objects to try, most likely first: the part the endpoint's connector is wired to,
    /// then the other connectors of that adapter topology. The caller releases each one.
    /// </summary>
    private static IEnumerable<IKsControl> EnumerateControls(IDeviceTopology endpointTopology)
    {
        IConnector? endpointConnector = null;
        IConnector? connectedTo = null;
        IDeviceTopology? adapterTopology = null;
        try
        {
            endpointTopology.GetConnector(0, out endpointConnector);
            endpointConnector.GetConnectedTo(out connectedTo);

            var primary = ActivateControl((IPart)connectedTo);
            if (primary is not null)
            {
                yield return primary;
            }

            ((IPart)connectedTo).GetTopologyObject(out adapterTopology);
            adapterTopology.GetConnectorCount(out uint count);
            for (uint i = 0; i < count; i++)
            {
                IConnector? other = null;
                try
                {
                    adapterTopology.GetConnector(i, out other);
                    var control = ActivateControl((IPart)other);
                    if (control is not null)
                    {
                        yield return control;
                    }
                }
                finally
                {
                    AudioInterop.Release(other);
                }
            }
        }
        finally
        {
            AudioInterop.Release(adapterTopology);
            AudioInterop.Release(connectedTo);
            AudioInterop.Release(endpointConnector);
        }
    }

    private static IKsControl? ActivateControl(IPart part)
    {
        var iid = AudioInterop.IID_IKsControl;
        int hr = part.Activate(AudioInterop.CLSCTX_ALL, ref iid, out IntPtr ptr);
        if (hr < 0 || ptr == IntPtr.Zero)
        {
            return null;
        }
        try
        {
            return (IKsControl)Marshal.GetObjectForIUnknown(ptr);
        }
        finally
        {
            Marshal.Release(ptr); // GetObjectForIUnknown added its own reference
        }
    }
}
