using System.Runtime.InteropServices;

namespace PodBridge.App.Native;

// Hand-written COM interop for the parts of the Windows audio stack PodBridge uses:
// MMDevice (enumeration, default device, notifications), IAudioEndpointVolume, the undocumented IPolicyConfig,
// WASAPI shared-mode rendering, and the device-topology / kernel-streaming path used for connect and disconnect.
//
// RULES FOR EDITING THIS FILE
//  * A COM interface is a vtable. Methods MUST be declared in exactly the order of the native interface,
//    and every method before the last one you call must be declared (use IntPtr placeholders for the ones you do not use).
//  * Do not reorder or "tidy" anything. Each GUID below is the interface's IID.
//  * Out/ref parameters of COM interface type are released by the runtime when the RCW is collected;
//    call Marshal.ReleaseComObject where the lifetime matters.

internal enum EDataFlow
{
    Render = 0,
    Capture = 1,
    All = 2,
}

internal enum ERole
{
    Console = 0,
    Multimedia = 1,
    Communications = 2,
}

[StructLayout(LayoutKind.Sequential)]
internal struct PropertyKey
{
    public Guid FormatId;
    public uint PropertyId;

    public PropertyKey(Guid formatId, uint propertyId)
    {
        FormatId = formatId;
        PropertyId = propertyId;
    }
}

/// <summary>
/// PROPVARIANT: 8 bytes of header then a 16-byte union on 64-bit Windows (24 bytes total).
/// Only used to read a VT_LPWSTR string, so the union is modelled as two pointers.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct PropVariant
{
    public ushort Vt;
    public ushort Reserved1;
    public ushort Reserved2;
    public ushort Reserved3;
    public IntPtr Data1;
    public IntPtr Data2;
}

/// <summary>KSPROPERTY: the property set GUID, the property id and flags (24 bytes).</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct KsPropertyHeader
{
    public Guid Set;
    public uint Id;
    public uint Flags;
}

[ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
internal class MMDeviceEnumeratorCoClass
{
}

[ComImport, Guid("870AF99C-171D-4F9E-AF0D-E63DF40C2BC9")]
internal class PolicyConfigClientCoClass
{
}

[ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDeviceEnumerator
{
    void EnumAudioEndpoints(EDataFlow dataFlow, uint stateMask, out IMMDeviceCollection devices);
    void GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice endpoint);
    void GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);
    void RegisterEndpointNotificationCallback(IMMNotificationClient client);
    void UnregisterEndpointNotificationCallback(IMMNotificationClient client);
}

[ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDeviceCollection
{
    void GetCount(out uint count);
    void Item(uint index, out IMMDevice device);
}

[ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDevice
{
    void Activate(ref Guid iid, uint clsCtx, IntPtr activationParams,
        [MarshalAs(UnmanagedType.IUnknown)] out object interfacePointer);
    void OpenPropertyStore(uint stgmAccess, out IPropertyStore properties);
    void GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
    void GetState(out uint state);
}

[ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IPropertyStore
{
    void GetCount(out uint count);
    void GetAt(uint index, out PropertyKey key);
    void GetValue(ref PropertyKey key, out PropVariant value);
    void SetValue(ref PropertyKey key, ref PropVariant value);
    void Commit();
}

/// <summary>Implemented by PodBridge; Windows calls it (on its own threads) when audio devices change.</summary>
[ComImport, Guid("7991EEC9-7E89-4D85-8390-6C703CEC60C0"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMNotificationClient
{
    void OnDeviceStateChanged([MarshalAs(UnmanagedType.LPWStr)] string deviceId, uint newState);
    void OnDeviceAdded([MarshalAs(UnmanagedType.LPWStr)] string deviceId);
    void OnDeviceRemoved([MarshalAs(UnmanagedType.LPWStr)] string deviceId);
    void OnDefaultDeviceChanged(EDataFlow flow, ERole role, [MarshalAs(UnmanagedType.LPWStr)] string? defaultDeviceId);
    void OnPropertyValueChanged([MarshalAs(UnmanagedType.LPWStr)] string deviceId, PropertyKey key);
}

[ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioEndpointVolume
{
    void RegisterControlChangeNotify(IntPtr notify);
    void UnregisterControlChangeNotify(IntPtr notify);
    void GetChannelCount(out uint channelCount);
    void SetMasterVolumeLevel(float levelDb, ref Guid eventContext);
    void SetMasterVolumeLevelScalar(float level, ref Guid eventContext);
    void GetMasterVolumeLevel(out float levelDb);
    void GetMasterVolumeLevelScalar(out float level);
    void SetChannelVolumeLevel(uint channel, float levelDb, ref Guid eventContext);
    void SetChannelVolumeLevelScalar(uint channel, float level, ref Guid eventContext);
    void GetChannelVolumeLevel(uint channel, out float levelDb);
    void GetChannelVolumeLevelScalar(uint channel, out float level);
    void SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid eventContext);
    void GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
}

/// <summary>
/// The undocumented IPolicyConfig (Windows 7 and later, including Windows 11), used only for SetDefaultEndpoint.
/// Slots before SetDefaultEndpoint are placeholders and must never be called.
/// </summary>
[ComImport, Guid("F8679F50-850A-41CF-9C72-430F290290C8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IPolicyConfig
{
    [PreserveSig] int GetMixFormat(IntPtr deviceId, IntPtr format);                    // 0
    [PreserveSig] int GetDeviceFormat(IntPtr deviceId, int defaultFormat, IntPtr format); // 1
    [PreserveSig] int ResetDeviceFormat(IntPtr deviceId);                              // 2
    [PreserveSig] int SetDeviceFormat(IntPtr deviceId, IntPtr endpointFormat, IntPtr mixFormat); // 3
    [PreserveSig] int GetProcessingPeriod(IntPtr deviceId, int defaultPeriod, IntPtr defaultPeriodOut, IntPtr minimumPeriodOut); // 4
    [PreserveSig] int SetProcessingPeriod(IntPtr deviceId, IntPtr period);             // 5
    [PreserveSig] int GetShareMode(IntPtr deviceId, IntPtr mode);                      // 6
    [PreserveSig] int SetShareMode(IntPtr deviceId, IntPtr mode);                      // 7
    [PreserveSig] int GetPropertyValue(IntPtr deviceId, int fx, IntPtr key, IntPtr value); // 8
    [PreserveSig] int SetPropertyValue(IntPtr deviceId, int fx, IntPtr key, IntPtr value); // 9
    [PreserveSig] int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string deviceId, ERole role); // 10
    [PreserveSig] int SetEndpointVisibility(IntPtr deviceId, int visible);             // 11
}

// ---- WASAPI shared-mode rendering (keep-alive) ----

[ComImport, Guid("1CB9AD4C-DBFA-4C32-B178-C2F568A703B2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioClient
{
    [PreserveSig] int Initialize(int shareMode, uint streamFlags, long bufferDuration, long periodicity,
        IntPtr format, IntPtr audioSessionGuid);
    [PreserveSig] int GetBufferSize(out uint bufferFrames);
    [PreserveSig] int GetStreamLatency(out long latency);
    [PreserveSig] int GetCurrentPadding(out uint paddingFrames);
    [PreserveSig] int IsFormatSupported(int shareMode, IntPtr format, out IntPtr closestMatch);
    [PreserveSig] int GetMixFormat(out IntPtr format);
    [PreserveSig] int GetDevicePeriod(out long defaultPeriod, out long minimumPeriod);
    [PreserveSig] int Start();
    [PreserveSig] int Stop();
    [PreserveSig] int Reset();
    [PreserveSig] int SetEventHandle(IntPtr eventHandle);
    [PreserveSig] int GetService(ref Guid iid, [MarshalAs(UnmanagedType.IUnknown)] out object service);
}

[ComImport, Guid("F294ACFC-3146-4483-A7BF-ADDCA7C260E2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioRenderClient
{
    [PreserveSig] int GetBuffer(uint frames, out IntPtr data);
    [PreserveSig] int ReleaseBuffer(uint frames, uint flags);
}

// ---- Device topology and kernel streaming (connect / disconnect) ----

[ComImport, Guid("2A07407E-6497-4A18-9787-32F79BD0D98F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IDeviceTopology
{
    void GetConnectorCount(out uint count);
    void GetConnector(uint index, out IConnector connector);
    void GetSubunitCount(out uint count);
    void GetSubunit(uint index, out IntPtr subunit);
    void GetPartById(uint id, out IPart part);
    void GetDeviceId([MarshalAs(UnmanagedType.LPWStr)] out string deviceId);
    void GetSignalPath(IntPtr from, IntPtr to, [MarshalAs(UnmanagedType.Bool)] bool rejectMixedPaths, out IntPtr parts);
}

[ComImport, Guid("9C2C4058-23F5-41DE-877A-DF3AF236A09E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IConnector
{
    void GetConnectorType(out int type);
    void GetDataFlow(out int flow);
    void ConnectTo(IntPtr other);
    void Disconnect();
    void IsConnected([MarshalAs(UnmanagedType.Bool)] out bool connected);
    void GetConnectedTo(out IConnector other);
    void GetConnectorIdConnectedTo([MarshalAs(UnmanagedType.LPWStr)] out string connectorId);
    void GetDeviceIdConnectedTo([MarshalAs(UnmanagedType.LPWStr)] out string deviceId);
}

[ComImport, Guid("AE2DE0E4-5BCA-4F2D-AA46-5D13F8FDB3A9"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IPart
{
    void GetName([MarshalAs(UnmanagedType.LPWStr)] out string name);
    void GetLocalId(out uint id);
    void GetGlobalId([MarshalAs(UnmanagedType.LPWStr)] out string id);
    void GetPartType(out int type);
    void GetSubType(out Guid subType);
    void GetControlInterfaceCount(out uint count);
    void GetControlInterface(uint index, out IntPtr controlInterface);
    void EnumPartsIncoming(out IntPtr parts);
    void EnumPartsOutgoing(out IntPtr parts);
    void GetTopologyObject(out IDeviceTopology topology);
    [PreserveSig] int Activate(uint clsContext, ref Guid iid, out IntPtr result);
    void RegisterControlChangeCallback(ref Guid iid, IntPtr notify);
    void UnregisterControlChangeCallback(IntPtr notify);
}

[ComImport, Guid("28F54685-06FD-11D2-B27A-00A0C9223196"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IKsControl
{
    [PreserveSig] int KsProperty(ref KsPropertyHeader property, uint propertyLength, IntPtr propertyData,
        uint dataLength, out uint bytesReturned);
    [PreserveSig] int KsMethod(IntPtr method, uint methodLength, IntPtr methodData, uint dataLength, out uint bytesReturned);
    [PreserveSig] int KsEvent(IntPtr eventPtr, uint eventLength, IntPtr eventData, uint dataLength, out uint bytesReturned);
}

internal static class AudioInterop
{
    public const uint CLSCTX_ALL = 0x17; // INPROC_SERVER | INPROC_HANDLER | LOCAL_SERVER | REMOTE_SERVER

    public const uint DEVICE_STATE_ACTIVE = 0x1;
    public const uint DEVICE_STATE_DISABLED = 0x2;
    public const uint DEVICE_STATE_NOTPRESENT = 0x4;
    public const uint DEVICE_STATE_UNPLUGGED = 0x8;

    public const uint STGM_READ = 0;
    public const ushort VT_LPWSTR = 31;
    public const int AUDCLNT_SHAREMODE_SHARED = 0;

    public const uint KSPROPERTY_TYPE_GET = 0x1;
    public const uint KSPROPERTY_TYPE_SET = 0x2;

    // PKEY_Device_FriendlyName {a45c254e-df1c-4efd-8020-67d146a850e0}, 14
    public static readonly PropertyKey PKEY_Device_FriendlyName =
        new(new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"), 14);

    // KSPROPSETID_BtAudio {7fa06c40-b8f6-4c7e-8556-e8c33a12e54d}
    public static readonly Guid KSPROPSETID_BtAudio = new("7FA06C40-B8F6-4C7E-8556-E8C33A12E54D");
    public const uint KSPROPERTY_ONESHOT_RECONNECT = 0;
    public const uint KSPROPERTY_ONESHOT_DISCONNECT = 1;

    public static readonly Guid IID_IAudioEndpointVolume = new("5CDF2C82-841E-4546-9722-0CF74078229A");
    public static readonly Guid IID_IAudioClient = new("1CB9AD4C-DBFA-4C32-B178-C2F568A703B2");
    public static readonly Guid IID_IAudioRenderClient = new("F294ACFC-3146-4483-A7BF-ADDCA7C260E2");
    public static readonly Guid IID_IDeviceTopology = new("2A07407E-6497-4A18-9787-32F79BD0D98F");
    public static readonly Guid IID_IKsControl = new("28F54685-06FD-11D2-B27A-00A0C9223196");

    [DllImport("ole32.dll")]
    public static extern int PropVariantClear(ref PropVariant pvar);

    [DllImport("ole32.dll")]
    public static extern void CoTaskMemFree(IntPtr ptr);

    /// <summary>Reads the friendly name of an endpoint, or null if it cannot be read.</summary>
    public static string? ReadFriendlyName(IMMDevice device)
    {
        IPropertyStore? store = null;
        try
        {
            device.OpenPropertyStore(STGM_READ, out store);
            var key = PKEY_Device_FriendlyName;
            store.GetValue(ref key, out var value);
            try
            {
                return value.Vt == VT_LPWSTR && value.Data1 != IntPtr.Zero
                    ? Marshal.PtrToStringUni(value.Data1)
                    : null;
            }
            finally
            {
                PropVariantClear(ref value);
            }
        }
        catch (COMException)
        {
            return null;
        }
        finally
        {
            Release(store);
        }
    }

    /// <summary>Releases a COM object's reference now instead of waiting for the garbage collector.</summary>
    public static void Release(object? com)
    {
        if (com is not null && Marshal.IsComObject(com))
        {
            try { Marshal.ReleaseComObject(com); } catch { /* already gone */ }
        }
    }
}
