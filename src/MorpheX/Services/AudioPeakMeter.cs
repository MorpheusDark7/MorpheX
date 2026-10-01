using System.Runtime.InteropServices;

namespace MorpheX.Services;

/// <summary>
/// Ultra-lightweight Windows CoreAudio WASAPI peak meter.
/// Reads the default multimedia audio endpoint peak level.
/// The IMMDeviceEnumerator vtable starts after IUnknown's 3 slots,
/// so EnumAudioEndpoints is slot[3] and GetDefaultAudioEndpoint is slot[4].
/// We model this by including a dummy EnumAudioEndpoints first.
/// </summary>
public sealed class AudioPeakMeter : IDisposable
{
    private IAudioMeterInformation? _meter;
    private bool _initialized;

    public float GetPeak()
    {
        try
        {
            if (!_initialized || _meter == null)
                InitializeMeter();

            if (_meter != null && _meter.GetPeakValue(out float peak) == 0)
                return Math.Clamp(peak, 0f, 1f);
        }
        catch
        {
            // Audio device changed or unavailable — reset so we re-init next tick
            if (_meter != null) { Marshal.ReleaseComObject(_meter); _meter = null; }
            _initialized = false;
        }
        return 0f;
    }

    private void InitializeMeter()
    {
        _initialized = true;
        try
        {
            var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
            // eRender=0, eMultimedia=1
            enumerator.GetDefaultAudioEndpoint(0, 1, out IMMDevice dev);
            if (dev != null)
            {
                var iid = typeof(IAudioMeterInformation).GUID;
                // CLSCTX_ALL = 23
                dev.Activate(ref iid, 23, IntPtr.Zero, out object meterObj);
                _meter = meterObj as IAudioMeterInformation;
            }
        }
        catch
        {
            _meter = null;
        }
    }

    public void Dispose()
    {
        if (_meter != null) { Marshal.ReleaseComObject(_meter); _meter = null; }
    }

    #region COM Interfaces

    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private class MMDeviceEnumeratorComObject { }

    // Full IMMDeviceEnumerator vtable (post-IUnknown slots):
    //   [0] EnumAudioEndpoints
    //   [1] GetDefaultAudioEndpoint
    //   [2] GetDevice
    //   [3] RegisterEndpointNotificationCallback
    //   [4] UnregisterEndpointNotificationCallback
    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        int EnumAudioEndpoints(int dataFlow, int dwStateMask, out IntPtr ppDevices);
        int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice ppEndpoint);
        int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string pwstrId, out IMMDevice ppDevice);
        int RegisterEndpointNotificationCallback(IntPtr pClient);
        int UnregisterEndpointNotificationCallback(IntPtr pClient);
    }

    // IMMDevice vtable (post-IUnknown):
    //   [0] Activate
    //   [1] OpenPropertyStore
    //   [2] GetId
    //   [3] GetState
    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        int Activate(ref Guid iid, int dwClsCtx, IntPtr pActivationParams,
            [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);
        int OpenPropertyStore(int stgmAccess, out IntPtr ppProperties);
        int GetId([MarshalAs(UnmanagedType.LPWStr)] out string ppstrId);
        int GetState(out int pdwState);
    }

    // IAudioMeterInformation vtable (post-IUnknown):
    //   [0] GetPeakValue
    //   [1] GetMeteringChannelCount
    //   [2] GetChannelsPeakValues
    //   [3] QueryHardwareSupport
    [ComImport, Guid("C02216F6-8C67-4B5B-9D00-D008E73E0064"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioMeterInformation
    {
        int GetPeakValue(out float pfPeak);
        int GetMeteringChannelCount(out int pnChannelCount);
        int GetChannelsPeakValues(int u32ChannelCount, [Out] float[] afPeakValues);
        int QueryHardwareSupport(out int pdwHardwareSupportMask);
    }

    #endregion
}
