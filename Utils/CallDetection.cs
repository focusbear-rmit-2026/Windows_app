using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Focus_Bear.Utils
{
    static class CallDetection
    {
        private const uint MMSYSERR_NOERROR = 0;
        private const uint WAVE_MAPPER = unchecked((uint)-1);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct WAVEINCAPS
        {
            public ushort wMid;
            public ushort wPid;
            public uint vDriverVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string szPname;
            public uint dwFormats;
            public ushort wChannels;
            public ushort wReserved1;
        }

        [DllImport("winmm.dll", SetLastError = true)]
        private static extern uint waveInGetNumDevs();

        [DllImport("winmm.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern uint waveInGetDevCaps(uint uDeviceID, ref WAVEINCAPS pwic, uint cbwic);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("kernel32.dll")]
        private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr hObject);

        private const uint PROCESS_QUERY_INFORMATION = 0x0400;



        private static void InitializeMicrophone()
        {
            uint numDevices = waveInGetNumDevs();
            if (numDevices > 0)
            {
                WAVEINCAPS caps = new WAVEINCAPS();
                uint result = waveInGetDevCaps(WAVE_MAPPER, ref caps, (uint)Marshal.SizeOf(typeof(WAVEINCAPS)));

                if (result == MMSYSERR_NOERROR)
                {
                    string deviceName = caps.szPname;

                    IntPtr foregroundWindow = GetForegroundWindow();
                    uint processId;
                    GetWindowThreadProcessId(foregroundWindow, out processId);
                    IntPtr processHandle = OpenProcess(PROCESS_QUERY_INFORMATION, false, processId);

                    if (deviceName.Contains("Microphone")) // You may need to adjust this check based on your device name
                    {
                        // Microphone is currently in use
                        /// microphoneStatus.Text = "In use";
                        /// microphoneStatus.Foreground = System.Windows.Media.Brushes.Red;
                    }
                    else
                    {
                        // Microphone is not in use
                        /// microphoneStatus.Text = "Not in use";
                        /// microphoneStatus.Foreground = System.Windows.Media.Brushes.Green;
                    }

                    CloseHandle(processHandle);
                }
            }
            else
            {
                ///microphoneStatus.Text = "No microphone found";
                /// microphoneStatus.Foreground = System.Windows.Media.Brushes.Red;
            }
        }

        enum CLSCTX : uint
        {
            INPROC_SERVER = 0x1,
            INPROC_HANDLER = 0x2,
            LOCAL_SERVER = 0x4,
            INPROC_SERVER16 = 0x8,
            REMOTE_SERVER = 0x10,
            INPROC_HANDLER16 = 0x20,
            RESERVED1 = 0x40,
            RESERVED2 = 0x80,
            RESERVED3 = 0x100,
            RESERVED4 = 0x200,
            NO_CODE_DOWNLOAD = 0x400,
            RESERVED5 = 0x800,
            NO_CUSTOM_MARSHAL = 0x1000,
            ENABLE_CODE_DOWNLOAD = 0x2000,
            NO_FAILURE_LOG = 0x4000,
            DISABLE_AAA = 0x8000,
            ENABLE_AAA = 0x10000,
            FROM_DEFAULT_CONTEXT = 0x20000,
            ACTIVATE_32_BIT_SERVER = 0x40000,
            ACTIVATE_64_BIT_SERVER = 0x80000,
            INPROC = INPROC_SERVER | INPROC_HANDLER,
            SERVER = INPROC_SERVER | LOCAL_SERVER | REMOTE_SERVER,
            ALL = SERVER | INPROC_HANDLER
        }

      /**   static void CheckMicrophoneUsage()
        {
            IMMDeviceEnumerator deviceEnumerator = null;
            IMMDeviceCollection devices = null;
            IntPtr pCollection = IntPtr.Zero;

            try
            {
                // Initialize COM
                // Marshal.ThrowExceptionForHR(CoInitializeEx(IntPtr.Zero, COINIT.MULTITHREADED));

                // Create the device enumerator
                deviceEnumerator = (IMMDeviceEnumerator)(new MMDeviceEnumerator());
                deviceEnumerator.EnumAudioEndpoints(EDataFlow.eCapture, DEVICE_STATE.ACTIVE, out devices);

                uint count;
                devices.GetCount(out count);

                for (int i = 0; i < count; i++)
                {
                    IMMDevice device;
                    devices.Item(i, out device);

                    IAudioSessionManager2 sessionManager = null;
                    IAudioSessionEnumerator sessionEnum = null;
                    try
                    {
                        // Activate the session manager
                        Guid IID_IAudioSessionManager2 = typeof(IAudioSessionManager2).GUID;
                        object o;
                        device.Activate(ref IID_IAudioSessionManager2, 0, IntPtr.Zero, out o);
                        sessionManager = (IAudioSessionManager2)o;


                        // Enumerate sessions for this device
                        sessionManager.GetSessionEnumerator(out sessionEnum);

                        int sessionCount;
                        sessionEnum.GetCount(out sessionCount);

                        for (int j = 0; j < sessionCount; j++)
                        {
                            IAudioSessionControl sessionControl;
                            sessionEnum.GetSession(j, out sessionControl);

                            AudioSessionState state;
                            sessionControl.GetState(out state);

                            if (state == AudioSessionState.Active)
                            {
                                int processId = 0;
                                sessionControl.GetProcessId(out processId);
                                Console.WriteLine($"Active session detected. Process ID: {processId}");

                                // Get process name from process ID
                                try
                                {
                                    Process process = Process.GetProcessById((int)processId);
                                    Console.WriteLine($"Process Name: {process.ProcessName}");
                                }
                                catch (ArgumentException)
                                {
                                    // Process with the specified process ID is not running
                                    Console.WriteLine($"Process with ID {processId} is not running.");
                                }
                            }

                            Marshal.ReleaseComObject(sessionControl);
                        }
                    }
                    finally
                    {
                        // Cleanup COM objects
                        if (sessionEnum != null)
                            Marshal.ReleaseComObject(sessionEnum);
                        if (sessionManager != null)
                            Marshal.ReleaseComObject(sessionManager);
                        Marshal.ReleaseComObject(device);
                    }
                }
            }
            finally
            {
                // Cleanup COM objects and uninitialize COM
                if (devices != null)
                    Marshal.ReleaseComObject(devices);
                if (deviceEnumerator != null)
                    Marshal.ReleaseComObject(deviceEnumerator);
            }
        }***/

        // COM Interop Declarations
        // Include the declarations for IMMDeviceEnumerator, MMDeviceEnumerator, and other necessary interfaces here
     

     /***   private static ISimpleAudioVolume GetVolumeObject(int pid = 0)
        {
            IMMDeviceEnumerator deviceEnumerator = null;
            IAudioSessionEnumerator sessionEnumerator = null;
            IAudioSessionManager mgr = null;
            IAudioSessionManager2 mgr2 = null;
            IMMDevice microphone = null;

            try
            {
                // get the speakers (1st render + multimedia) device
                deviceEnumerator = (IMMDeviceEnumerator)(new MMDeviceEnumerator());

                deviceEnumerator.GetDefaultAudioEndpoint(EDataFlow.eCapture, ERole.eCommunications, out microphone);

                if (microphone == null)//microphone not detected
                {
                    return null;
                }

                // activate the session manager. we need the enumerator
                Guid IID_IAudioSessionManager2 = typeof(IAudioSessionManager2).GUID;
                object o;
                microphone.Activate(ref IID_IAudioSessionManager2, 0, IntPtr.Zero, out o);
                mgr2 = (IAudioSessionManager2)o;

                // enumerate sessions for on this device
                mgr2.GetSessionEnumerator(out sessionEnumerator);
                int count;
                sessionEnumerator.GetCount(out count);


                // search for an audio session with the required process-id
                ISimpleAudioVolume volumeControl = null;
                for (int i = 0; i < count; ++i)
                {
                    IAudioSessionControl2 ctl = null;
                    try
                    {
                        sessionEnumerator.GetSession(i, out ctl);

                        if (0 == ctl.IsSystemSoundsSession())
                        {
                            continue;
                        }




                        // NOTE: we could also use the app name from ctl.GetDisplayName()
                        int cpid;
                        ctl.GetProcessId(out cpid);

                        if (cpid > 0)
                        {
                            string pRetVal = "";
                            ctl.GetDisplayName(out pRetVal);

                            volumeControl = ctl as ISimpleAudioVolume;
                            if (volumeControl == null)
                                continue;

                            float level;
                            volumeControl.GetMasterVolume(out level);
                        }



                    }
                    finally
                    {
                        if (ctl != null) Marshal.ReleaseComObject(ctl);
                    }
                }

                return volumeControl;
            }
            finally
            {
                if (sessionEnumerator != null) Marshal.ReleaseComObject(sessionEnumerator);
                if (mgr2 != null) Marshal.ReleaseComObject(mgr2);
                if (microphone != null) Marshal.ReleaseComObject(microphone);
                if (deviceEnumerator != null) Marshal.ReleaseComObject(deviceEnumerator);
            }
        }///***/


        public static bool IsUserOnCall()
        {
            test();
            /// CheckMicrophoneUsage();

            /// GetVolumeObject();

            ///InitializeMicrophone();
            //return false;
            //if (IsMicrophoneInUse())
            {
                Console.WriteLine("IsMicrophoneInUse");
                //return true;
            }
            //if(IsWebCamInUse())
            //{
            //    Console.WriteLine("IsWebCamInUse");
            //    //return true;
            //}

            //InitializeMicrophoneUsageDetection();

            return false; // Microphone is not in use or an error occurred

        }

        //https://gist.github.com/sverrirs/d099b34b7f72bb4fb386
       /*** private static void InitializeMicrophoneUsageDetection()
        {
            IMMDeviceEnumerator deviceEnumerator = null;
            IAudioSessionEnumerator sessionEnumerator = null;
            IAudioSessionManager2 mgr = null;
            IMMDevice defaultCaptureDevice = null;

            // get the speakers (1st render + multimedia) device
            deviceEnumerator = (IMMDeviceEnumerator)(new MMDeviceEnumerator());
            deviceEnumerator.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eMultimedia, out defaultCaptureDevice);

            // activate the session manager. we need the enumerator
            Guid IID_IAudioSessionManager2 = typeof(IAudioSessionManager2).GUID;
            object o;
            defaultCaptureDevice.Activate(ref IID_IAudioSessionManager2, 0, IntPtr.Zero, out o);
            mgr = (IAudioSessionManager2)o;

            // enumerate sessions for on this device
            mgr.GetSessionEnumerator(out sessionEnumerator);
            int count;
            sessionEnumerator.GetCount(out count);

            // search for an audio session with the required process-id
            ISimpleAudioVolume volumeControl = null;
            for (int i = 0; i < count; ++i)
            {
                IAudioSessionControl2 ctl = null;
                try
                {
                    sessionEnumerator.GetSession(i, out ctl);

                    string name;
                    ctl.GetDisplayName(out name);
                    Console.WriteLine(": " + name);
                    // NOTE: we could also use the app name from ctl.GetDisplayName()
                    //int cpid;
                    //ctl.GetProcessId(out cpid);

                    //if (cpid == pid)
                    //{
                    //    volumeControl = ctl as ISimpleAudioVolume;
                    //    break;
                    //}
                }
                finally
                {
                    if (ctl != null) Marshal.ReleaseComObject(ctl);
                }
            }


            //    mgr.OnSessionCreated += AudioSessionManager_OnSessionCreated;

            //List<string> microphoneUsageApplications = GetMicrophoneUsageApplications();
            //DisplayMicrophoneUsageApplications(microphoneUsageApplications);
        }
        ***/
        //private static List<string> GetMicrophoneUsageApplications()
        //{
        //    List<string> microphoneUsageApplications = new List<string>();

        //    foreach (var session in defaultCaptureDevice.AudioSessionManager.Sessions)
        //    {
        //        if (session.State == AudioSessionState.AudioSessionStateActive)
        //        {
        //            string applicationName = session.GetDisplayName();
        //            microphoneUsageApplications.Add(applicationName);
        //        }
        //    }

        //    return microphoneUsageApplications;
        //}

        //private static void AudioSessionManager_OnSessionCreated(object sender, IAudioSessionControl2 newSession)
        //{
        //    string applicationName = newSession.GetDisplayName();

        //    Dispatcher.Invoke(() =>
        //    {
        //        Label label = new Label
        //        {
        //            Content = $"New microphone usage: {applicationName}"
        //        };
        //        MicrophoneUsageStackPanel.Children.Add(label);
        //    });
        //}

        //public static bool IsMicrophoneInUse1()
        //{
        //    try
        //    {
        //        MMDeviceEnumerator enumerator = new MMDeviceEnumerator();
        //        MMDeviceCollection devices = enumerator.EnumerateAudioEndPoints(EDataFlow.Capture, DeviceState.Active);

        //        foreach (MMDevice device in devices)
        //        {
        //            // Check if any active audio session is capturing from the microphone.
        //            foreach (var session in device.AudioSessionManager.Sessions)
        //            {
        //                if (session.State == AudioSessionState.AudioSessionStateActive)
        //                {
        //                    return true; // Microphone is in use
        //                }
        //            }
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine("Error checking microphone status: " + ex.Message);
        //    }

        //    return false; // Microphone is not in use or an error occurred
        //}

            //mj new
        public static PROPERTYKEY PKEY_Device_FriendlyName = new PROPERTYKEY(new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"), 14);
        public static PROPERTYKEY PKEY_Device_DeviceDesc = new PROPERTYKEY(new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"), 2);

        public enum HRESULT : int
        {
            S_OK = 0,
            S_FALSE = 1,
            E_NOINTERFACE = unchecked((int)0x80004002),
            E_NOTIMPL = unchecked((int)0x80004001),
            E_FAIL = unchecked((int)0x80004005)
        }

        [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IPropertyStore
        {
            HRESULT GetCount([Out] out uint propertyCount);
            HRESULT GetAt([In] uint propertyIndex, [Out, MarshalAs(UnmanagedType.Struct)] out PROPERTYKEY key);
            HRESULT GetValue([In, MarshalAs(UnmanagedType.Struct)] ref PROPERTYKEY key, [Out, MarshalAs(UnmanagedType.Struct)] out PROPVARIANT pv);
            HRESULT SetValue([In, MarshalAs(UnmanagedType.Struct)] ref PROPERTYKEY key, [In, MarshalAs(UnmanagedType.Struct)] ref PROPVARIANT pv);
            HRESULT Commit();
        }

        public const int STGM_READ = 0x00000000;
        public const int STGM_WRITE = 0x00000001;
        public const int STGM_READWRITE = 0x00000002;

        public enum EDataFlow
        {
            eRender = 0,
            eCapture = (eRender + 1),
            eAll = (eCapture + 1),
            EDataFlow_enum_count = (eAll + 1)
        }
        public enum ERole
        {
            eConsole = 0,
            eMultimedia = (eConsole + 1),
            eCommunications = (eMultimedia + 1),
            ERole_enum_count = (eCommunications + 1)
        }

        [ComImport]
        [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IMMDeviceEnumerator
        {
            HRESULT EnumAudioEndpoints(EDataFlow dataFlow, int dwStateMask, out IMMDeviceCollection ppDevices);
            HRESULT GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice ppEndpoint);
            HRESULT GetDevice(string pwstrId, out IMMDevice ppDevice);
            HRESULT RegisterEndpointNotificationCallback(IMMNotificationClient pClient);
            HRESULT UnregisterEndpointNotificationCallback(IMMNotificationClient pClient);
        }

        public const int DEVICE_STATE_ACTIVE = 0x00000001;
        public const int DEVICE_STATE_DISABLED = 0x00000002;
        public const int DEVICE_STATE_NOTPRESENT = 0x00000004;
        public const int DEVICE_STATE_UNPLUGGED = 0x00000008;
        public const int DEVICE_STATEMASK_ALL = 0x0000000f;

        [ComImport]
        [Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IMMDeviceCollection
        {
            HRESULT GetCount(out uint pcDevices);
            HRESULT Item(uint nDevice, out IMMDevice ppDevice);
        }

        [ComImport]
        [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IMMDevice
        {
            [PreserveSig]
            HRESULT Activate(ref Guid iid, int dwClsCtx, ref PROPVARIANT pActivationParams, out IntPtr ppInterface);
            //HRESULT Activate(ref Guid iid, int dwClsCtx, IntPtr pActivationParams, out IntPtr ppInterface);

            HRESULT OpenPropertyStore(int stgmAccess, out IPropertyStore ppProperties);

            //HRESULT GetId(StringBuilder ppstrId);
            HRESULT GetId(out IntPtr ppstrId);
            HRESULT GetState(out int pdwState);
        }

        [ComImport]
        [Guid("7991EEC9-7E89-4D85-8390-6C703CEC60C0")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IMMNotificationClient
        {
            HRESULT OnDeviceStateChanged(string pwstrDeviceId, int dwNewState);
            HRESULT OnDeviceAdded(string pwstrDeviceId);
            HRESULT OnDeviceRemoved(string pwstrDeviceId);
            HRESULT OnDefaultDeviceChanged(EDataFlow flow, ERole role, string pwstrDefaultDeviceId);
            HRESULT OnPropertyValueChanged(string pwstrDeviceId, ref PROPERTYKEY key);
        }

        [ComImport]
        [Guid("1BE09788-6894-4089-8586-9A2A6C265AC5")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IMMEndpoint
        {
            HRESULT GetDataFlow(out EDataFlow pDataFlow);
        }

        public struct PROPERTYKEY
        {
            public PROPERTYKEY(Guid InputId, UInt32 InputPid)
            {
                fmtid = InputId;
                pid = InputPid;
            }
            Guid fmtid;
            uint pid;
        };

        [StructLayout(LayoutKind.Sequential)]
        public struct PROPARRAY
        {
            public UInt32 cElems;
            public IntPtr pElems;
        }

        [StructLayout(LayoutKind.Explicit, Pack = 1)]
        public struct PROPVARIANT
        {
            [FieldOffset(0)]
            public ushort varType;
            [FieldOffset(2)]
            public ushort wReserved1;
            [FieldOffset(4)]
            public ushort wReserved2;
            [FieldOffset(6)]
            public ushort wReserved3;

            [FieldOffset(8)]
            public byte bVal;
            [FieldOffset(8)]
            public sbyte cVal;
            [FieldOffset(8)]
            public ushort uiVal;
            [FieldOffset(8)]
            public short iVal;
            [FieldOffset(8)]
            public UInt32 uintVal;
            [FieldOffset(8)]
            public Int32 intVal;
            [FieldOffset(8)]
            public UInt64 ulVal;
            [FieldOffset(8)]
            public Int64 lVal;
            [FieldOffset(8)]
            public float fltVal;
            [FieldOffset(8)]
            public double dblVal;
            [FieldOffset(8)]
            public short boolVal;
            [FieldOffset(8)]
            public IntPtr pclsidVal; // GUID ID pointer
            [FieldOffset(8)]
            public IntPtr pszVal; // Ansi string pointer
            [FieldOffset(8)]
            public IntPtr pwszVal; // Unicode string pointer
            [FieldOffset(8)]
            public IntPtr punkVal; // punkVal (interface pointer)
            [FieldOffset(8)]
            public PROPARRAY ca;
            [FieldOffset(8)]
            public System.Runtime.InteropServices.ComTypes.FILETIME filetime;
        }

        [ComImport]
        [Guid("5CDF2C82-841E-4546-9722-0CF74078229A")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IAudioEndpointVolume
        {
            HRESULT RegisterControlChangeNotify(IAudioEndpointVolumeCallback pNotify);
            HRESULT UnregisterControlChangeNotify(IAudioEndpointVolumeCallback pNotify);
            HRESULT GetChannelCount(out uint pnChannelCount);
            HRESULT SetMasterVolumeLevel(float fLevelDB, ref Guid pguidEventContext);
            HRESULT SetMasterVolumeLevelScalar(float fLevel, ref Guid pguidEventContext);
            HRESULT GetMasterVolumeLevel(out float pfLevelDB);
            HRESULT GetMasterVolumeLevelScalar(out float pfLevel);
            HRESULT SetChannelVolumeLevel(uint nChannel, float fLevelDB, ref Guid pguidEventContext);
            HRESULT SetChannelVolumeLevelScalar(uint nChannel, float fLevel, ref Guid pguidEventContext);
            HRESULT GetChannelVolumeLevel(uint nChannel, out float pfLevelDB);
            HRESULT GetChannelVolumeLevelScalar(uint nChannel, out float pfLevel);
            HRESULT SetMute(bool bMute, ref Guid pguidEventContext);
            HRESULT GetMute(out bool pbMute);
            HRESULT GetVolumeStepInfo(out uint pnStep, out uint pnStepCount);
            HRESULT VolumeStepUp(ref Guid pguidEventContext);
            HRESULT VolumeStepDown(ref Guid pguidEventContext);
            HRESULT QueryHardwareSupport(out int pdwHardwareSupportMask);
            HRESULT GetVolumeRange(out float pflVolumeMindB, out float pflVolumeMaxdB, out float pflVolumeIncrementdB);
        }

        [ComImport]
        [Guid("657804FA-D6AD-4496-8A60-352752AF4F89")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IAudioEndpointVolumeCallback
        {
            HRESULT OnNotify(IntPtr pNotify);
        }


        [ComImport]
        [Guid("f8679f50-850a-41cf-9c72-430f290290c8")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IPolicyConfig
        {
            HRESULT GetMixFormat([In] [MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, out WAVEFORMATEXTENSIBLE ppFormat);
            HRESULT GetDeviceFormat([In] [MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, [In] [MarshalAs(UnmanagedType.Bool)] bool bDefault, out WAVEFORMATEXTENSIBLE ppFormat);
            HRESULT ResetDeviceFormat([In] [MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName);
            HRESULT SetDeviceFormat([In] [MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, [In] [MarshalAs(UnmanagedType.LPStruct)] WAVEFORMATEXTENSIBLE pEndpointFormat, [In] [MarshalAs(UnmanagedType.LPStruct)] WAVEFORMATEXTENSIBLE pMixFormat);
            HRESULT GetProcessingPeriod([In] [MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, [In] [MarshalAs(UnmanagedType.Bool)] bool bDefault, out Int64 pmftDefaultPeriod, out Int64 pmftMinimumPeriod);
            HRESULT SetProcessingPeriod([In] [MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, Int64 pmftPeriod);
            HRESULT GetShareMode([In] [MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, [Out] out DeviceShareMode pMode);
            HRESULT SetShareMode([In] [MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, [In] DeviceShareMode mode);
            HRESULT GetPropertyValue([In] [MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, [In] [MarshalAs(UnmanagedType.Bool)] bool bFxStore, ref PROPERTYKEY pKey, out PROPVARIANT pv);
            HRESULT SetPropertyValue([In] [MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, [In] [MarshalAs(UnmanagedType.Bool)] bool bFxStore, [In] ref PROPERTYKEY pKey, ref PROPVARIANT pv);
            HRESULT SetDefaultEndpoint([In] [MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, [In] [MarshalAs(UnmanagedType.U4)] ERole role);
            HRESULT SetEndpointVisibility([In] [MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, [In] [MarshalAs(UnmanagedType.Bool)] bool bVisible);
        }

        [StructLayout(LayoutKind.Explicit, Pack = 1)]
        public class WAVEFORMATEXTENSIBLE : WAVEFORMATEX
        {
            [FieldOffset(0)]
            public short wValidBitsPerSample;
            [FieldOffset(0)]
            public short wSamplesPerBlock;
            [FieldOffset(0)]
            public short wReserved;
            [FieldOffset(2)]
            public WaveMask dwChannelMask;
            [FieldOffset(6)]
            public Guid SubFormat;
        }

        [Flags]
        public enum WaveMask
        {
            None = 0x0,
            FrontLeft = 0x1,
            FrontRight = 0x2,
            FrontCenter = 0x4,
            LowFrequency = 0x8,
            BackLeft = 0x10,
            BackRight = 0x20,
            FrontLeftOfCenter = 0x40,
            FrontRightOfCenter = 0x80,
            BackCenter = 0x100,
            SideLeft = 0x200,
            SideRight = 0x400,
            TopCenter = 0x800,
            TopFrontLeft = 0x1000,
            TopFrontCenter = 0x2000,
            TopFrontRight = 0x4000,
            TopBackLeft = 0x8000,
            TopBackCenter = 0x10000,
            TopBackRight = 0x20000
        }

        [StructLayout(LayoutKind.Sequential, Pack = 2)]
        public class WAVEFORMATEX
        {
            public short wFormatTag;
            public short nChannels;
            public int nSamplesPerSec;
            public int nAvgBytesPerSec;
            public short nBlockAlign;
            public short wBitsPerSample;
            public short cbSize;
        }

        public enum DeviceShareMode
        {
            Shared,
            Exclusive
        }

        [Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface IAudioSessionManager2
        {
            int NotImpl1();
            int NotImpl2();

            [PreserveSig]
            int GetSessionEnumerator(out IAudioSessionEnumerator SessionEnum);

            // the rest is not implemented
        }


        [Guid("bfb7ff88-7239-4fc9-8fa2-07c950be9c6d"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface IAudioSessionControl2
        {
            // IAudioSessionControl
            [PreserveSig]
            int NotImpl0();

            [PreserveSig]
            int GetState(out AudioSessionState pRetVal);

            [PreserveSig]
            int GetDisplayName([MarshalAs(UnmanagedType.LPWStr)] out string pRetVal);

            [PreserveSig]
            int SetDisplayName([MarshalAs(UnmanagedType.LPWStr)]string Value, [MarshalAs(UnmanagedType.LPStruct)] Guid EventContext);

            [PreserveSig]
            int GetIconPath([MarshalAs(UnmanagedType.LPWStr)] out string pRetVal);

            [PreserveSig]
            int SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string Value, [MarshalAs(UnmanagedType.LPStruct)] Guid EventContext);

            [PreserveSig]
            int GetGroupingParam(out Guid pRetVal);

            [PreserveSig]
            int SetGroupingParam([MarshalAs(UnmanagedType.LPStruct)] Guid Override, [MarshalAs(UnmanagedType.LPStruct)] Guid EventContext);

            [PreserveSig]
            int NotImpl1();

            [PreserveSig]
            int NotImpl2();

            // IAudioSessionControl2
            [PreserveSig]
            int GetSessionIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string pRetVal);

            [PreserveSig]
            int GetSessionInstanceIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string pRetVal);

            [PreserveSig]
            int GetProcessId(out int pRetVal);

            [PreserveSig]
            int IsSystemSoundsSession();

            [PreserveSig]
            int SetDuckingPreference(bool optOut);
        }

        [Guid("F4B1A599-7266-4319-A8CA-E70ACB11E8CD"),
        InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface IAudioSessionControl
        {
            void NotNeeded(); // Unused
            void NotNeeded2(); // Unused
            void NotNeeded3(); // Unused
            void GetState(out AudioSessionState state);

            [PreserveSig]
            int GetProcessId(out int pRetVal);
        }

        [Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface IAudioSessionEnumerator
        {
            [PreserveSig]
            int GetCount(out int SessionCount);

            [PreserveSig]
            int GetSession(int SessionCount, out IAudioSessionControl Session);
        }

        internal enum AudioSessionState
        {
            Inactive = 0,
            Active = 1,
            Expired = 2
        }

        static void test()
        {
            HRESULT hr = HRESULT.E_FAIL;
            Guid CLSID_MMDeviceEnumerator = new Guid("{BCDE0395-E52F-467C-8E3D-C4579291692E}");
            Type MMDeviceEnumeratorType = Type.GetTypeFromCLSID(CLSID_MMDeviceEnumerator, true);
            object MMDeviceEnumerator = Activator.CreateInstance(MMDeviceEnumeratorType);
            IMMDeviceEnumerator pMMDeviceEnumerator = (IMMDeviceEnumerator)MMDeviceEnumerator;
            if (pMMDeviceEnumerator != null)
            {
                IMMDevice pDevice = null;
                IMMDeviceCollection pDevices = null;
                hr = pMMDeviceEnumerator.EnumAudioEndpoints(EDataFlow.eCapture, DEVICE_STATE_ACTIVE, out pDevices);
                if (hr == HRESULT.S_OK)
                {
                    uint count;
                    pDevices.GetCount(out count);
                    for (uint i = 0; i < count; i++)
                    {
                        IMMDevice device = null;
                        hr = pDevices.Item(i, out device);
                        if (hr == HRESULT.S_OK)
                        {
                        }

                        IAudioSessionManager2 sessionManager = null;
                        IAudioSessionEnumerator sessionEnum = null;
                        try
                        {
                            // Activate the session manager

                            IntPtr pSessionManagerPtr = IntPtr.Zero;
                            PROPVARIANT pvActivate = new PROPVARIANT();
                            hr = device.Activate(typeof(IAudioSessionManager2).GUID, 0, ref pvActivate, out pSessionManagerPtr);
                            if (hr == HRESULT.S_OK)
                            {

                                IAudioSessionManager2 sessionManager2 = Marshal.GetObjectForIUnknown(pSessionManagerPtr) as IAudioSessionManager2;
                                // Enumerate sessions for this device
                                sessionManager2.GetSessionEnumerator(out sessionEnum);
                                int sessionCount;
                                sessionEnum.GetCount(out sessionCount);
                                for (int j = 0; j < sessionCount; j++)
                                {
                                    IAudioSessionControl sessionControl = null;
                                    sessionEnum.GetSession(j, out sessionControl);

                                    AudioSessionState state = AudioSessionState.Inactive;

                                    sessionControl.GetState(out state);

                                    if (state == AudioSessionState.Active)
                                    {
                                        int processId = 0;
                                        sessionControl.GetProcessId(out processId);
                                        Console.WriteLine($"Active session detected. Process ID: {processId}");

                                        // Get process name from process ID
                                        try
                                        {
                                            Process process = Process.GetProcessById((int)processId);
                                            Console.WriteLine($"Process Name: {process.ProcessName}");
                                        }
                                        catch (ArgumentException)
                                        {
                                            // Process with the specified process ID is not running
                                            Console.WriteLine($"Process with ID {processId} is not running.");
                                        }
                                    }

                                    Marshal.ReleaseComObject(sessionControl);
                                }
                            }



                            //for (int j = 0; j < sessionCount; j++)
                            //{
                            //    IAudioSessionControl sessionControl;
                            //    sessionEnum.GetSession(j, out sessionControl);

                            //    AudioSessionState state;
                            //    sessionControl.GetState(out state);

                            //    if (state == AudioSessionState.Active)
                            //    {
                            //        int processId = 0;
                            //        sessionControl.GetProcessId(out processId);
                            //        Console.WriteLine($"Active session detected. Process ID: {processId}");

                            //        // Get process name from process ID
                            //        try
                            //        {
                            //            Process process = Process.GetProcessById((int)processId);
                            //            Console.WriteLine($"Process Name: {process.ProcessName}");
                            //        }
                            //        catch (ArgumentException)
                            //        {
                            //            // Process with the specified process ID is not running
                            //            Console.WriteLine($"Process with ID {processId} is not running.");
                            //        }
                            //    }

                            //    Marshal.ReleaseComObject(sessionControl);
                            //}
                        }
                        finally
                        {
                            // Cleanup COM objects
                            if (sessionEnum != null)
                                Marshal.ReleaseComObject(sessionEnum);
                            if (sessionManager != null)
                                Marshal.ReleaseComObject(sessionManager);
                            Marshal.ReleaseComObject(device);
                        }
                    }
                }

                hr = pMMDeviceEnumerator.GetDefaultAudioEndpoint(EDataFlow.eCapture, ERole.eCommunications, out pDevice);
                if (hr == HRESULT.S_OK)
                {
                    IntPtr pAudioEndpointVolumePtr = IntPtr.Zero;
                    PROPVARIANT pvActivate = new PROPVARIANT();
                    hr = pDevice.Activate(typeof(IAudioEndpointVolume).GUID, 0, ref pvActivate, out pAudioEndpointVolumePtr);
                    if (hr == HRESULT.S_OK)
                    {
                        string sFriendlyName = null;
                        string sDesc = null;
                        IPropertyStore pPropertyStore = null;
                        hr = pDevice.OpenPropertyStore(STGM_READ, out pPropertyStore);
                        if (hr == HRESULT.S_OK)
                        {
                            PROPVARIANT pv = new PROPVARIANT();
                            hr = pPropertyStore.GetValue(ref PKEY_Device_FriendlyName, out pv);
                            if (hr == HRESULT.S_OK)
                            {
                                sFriendlyName = Marshal.PtrToStringUni(pv.pwszVal);
                            }
                            hr = pPropertyStore.GetValue(ref PKEY_Device_DeviceDesc, out pv);
                            if (hr == HRESULT.S_OK)
                            {
                                sDesc = Marshal.PtrToStringUni(pv.pwszVal);
                            }
                        }

                        IAudioEndpointVolume pAudioEndpointVolume = Marshal.GetObjectForIUnknown(pAudioEndpointVolumePtr) as IAudioEndpointVolume;
                        float nVolume = 0;
                        hr = pAudioEndpointVolume.GetMasterVolumeLevelScalar(out nVolume);
                        bool bMute = false;
                        hr = pAudioEndpointVolume.GetMute(out bMute);
                        Marshal.ReleaseComObject(pAudioEndpointVolume);

                        int nState = 0;
                        string sState = "";
                        hr = pDevice.GetState(out nState);
                        if ((nState == DEVICE_STATE_ACTIVE))
                            sState = "Active";
                        else if ((nState == DEVICE_STATE_DISABLED))
                            sState = "Disabled";
                        else if ((nState == DEVICE_STATE_NOTPRESENT))
                            sState = "Not present";
                        else if ((nState == DEVICE_STATE_UNPLUGGED))
                            sState = "Unplugged";

                        Console.WriteLine("Device: {0} - {1} - Volume : {2} {3}", sFriendlyName, sState, nVolume.ToString(), bMute ? "(Mute)" : "");
                    }
                    Marshal.ReleaseComObject(pDevice);
                }
                Marshal.ReleaseComObject(pMMDeviceEnumerator);
            }
        }

        public static void CheckForAudioVideoCall()
        {
            ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PnPEntity WHERE Caption LIKE '%Microphone%' OR Caption LIKE '%Camera%'");
            ManagementObjectCollection collection = searcher.Get();

            bool isAudioCallActive = false;
            bool isVideoCallActive = false;

            foreach (ManagementObject obj in collection)
            {
                string caption = obj["Caption"].ToString();

                if (caption.Contains("Microphone"))
                {
                    isAudioCallActive = true;
                }

                if (caption.Contains("Camera"))
                {
                    isVideoCallActive = true;
                }
            }

            if (isAudioCallActive)
            {
                // The user is on an audio call
                Console.WriteLine("User is on an audio call.");
            }

            if (isVideoCallActive)
            {
                // The user is on a video call
                Console.WriteLine("User is on a video call.");
            }
        }
        
        private static bool IsMicrophoneInUse()
        {
            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\microphone"))
                {
                    foreach (var subKeyName in key.GetSubKeyNames())
                    {
                        // NonPackaged has multiple subkeys
                        if (subKeyName == "NonPackaged")
                        {
                            using (var nonpackagedkey = key.OpenSubKey(subKeyName))
                            {
                                foreach (var nonpackagedSubKeyName in nonpackagedkey.GetSubKeyNames())
                                {
                                    using (var subKey = nonpackagedkey.OpenSubKey(nonpackagedSubKeyName))
                                    {
                                        if (subKey.GetValueNames().Contains("LastUsedTimeStop"))
                                        {
                                            var endTime = subKey.GetValue("LastUsedTimeStop") is long ? (long)subKey.GetValue("LastUsedTimeStop") : -1;
                                            if (endTime <= 0)
                                            {
                                                return true;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        else
                        {
                            using (var subKey = key.OpenSubKey(subKeyName))
                            {
                                if (subKey.GetValueNames().Contains("LastUsedTimeStop"))
                                {
                                    var endTime = subKey.GetValue("LastUsedTimeStop") is long ? (long)subKey.GetValue("LastUsedTimeStop") : -1;
                                    if (endTime <= 0)
                                    {
                                        return true;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("IsMicrophoneInUse() Exception" + ex.Message);
            }
            try
            { 
                using (var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\microphone"))
                {
                    foreach (var subKeyName in key.GetSubKeyNames())
                    {
                        // NonPackaged has multiple subkeys
                        if (subKeyName == "NonPackaged")
                        {
                            using (var nonpackagedkey = key.OpenSubKey(subKeyName))
                            {
                                foreach (var nonpackagedSubKeyName in nonpackagedkey.GetSubKeyNames())
                                {
                                    using (var subKey = nonpackagedkey.OpenSubKey(nonpackagedSubKeyName))
                                    {
                                        if (subKey.GetValueNames().Contains("LastUsedTimeStop"))
                                        {
                                            var endTime = subKey.GetValue("LastUsedTimeStop") is long ? (long)subKey.GetValue("LastUsedTimeStop") : -1;
                                            if (endTime <= 0)
                                            {
                                                return true;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        else
                        {
                            using (var subKey = key.OpenSubKey(subKeyName))
                            {
                                if (subKey.GetValueNames().Contains("LastUsedTimeStop"))
                                {
                                    var endTime = subKey.GetValue("LastUsedTimeStop") is long ? (long)subKey.GetValue("LastUsedTimeStop") : -1;
                                    if (endTime <= 0)
                                    {
                                        return true;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("IsMicrophoneInUse() Exception" + ex.Message);
            }

            return false;
        }

        private static bool IsWebCamInUse()
        {
            using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\webcam"))
            {
                foreach (var subKeyName in key.GetSubKeyNames())
                {
                    // NonPackaged has multiple subkeys
                    if (subKeyName == "NonPackaged")
                    {
                        using (var nonpackagedkey = key.OpenSubKey(subKeyName))
                        {
                            foreach (var nonpackagedSubKeyName in nonpackagedkey.GetSubKeyNames())
                            {
                                using (var subKey = nonpackagedkey.OpenSubKey(nonpackagedSubKeyName))
                                {
                                    if (subKey.GetValueNames().Contains("LastUsedTimeStop"))
                                    {
                                        var endTime = subKey.GetValue("LastUsedTimeStop") is long ? (long)subKey.GetValue("LastUsedTimeStop") : -1;
                                        if (endTime <= 0)
                                        {
                                            return true;
                                        }
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        using (var subKey = key.OpenSubKey(subKeyName))
                        {
                            if (subKey.GetValueNames().Contains("LastUsedTimeStop"))
                            {
                                var endTime = subKey.GetValue("LastUsedTimeStop") is long ? (long)subKey.GetValue("LastUsedTimeStop") : -1;
                                if (endTime <= 0)
                                {
                                    return true;
                                }
                            }
                        }
                    }
                }
            }

            using (var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\webcam"))
            {
                foreach (var subKeyName in key.GetSubKeyNames())
                {
                    // NonPackaged has multiple subkeys
                    if (subKeyName == "NonPackaged")
                    {
                        using (var nonpackagedkey = key.OpenSubKey(subKeyName))
                        {
                            foreach (var nonpackagedSubKeyName in nonpackagedkey.GetSubKeyNames())
                            {
                                using (var subKey = nonpackagedkey.OpenSubKey(nonpackagedSubKeyName))
                                {
                                    if (subKey.GetValueNames().Contains("LastUsedTimeStop"))
                                    {
                                        var endTime = subKey.GetValue("LastUsedTimeStop") is long ? (long)subKey.GetValue("LastUsedTimeStop") : -1;
                                        if (endTime <= 0)
                                        {
                                            return true;
                                        }
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        using (var subKey = key.OpenSubKey(subKeyName))
                        {
                            if (subKey.GetValueNames().Contains("LastUsedTimeStop"))
                            {
                                var endTime = subKey.GetValue("LastUsedTimeStop") is long ? (long)subKey.GetValue("LastUsedTimeStop") : -1;
                                if (endTime <= 0)
                                {
                                    return true;
                                }
                            }
                        }
                    }
                }
            }

            return false;
        }

        private const uint WM_CAP_START = 1024;
        private const uint WM_CAP_DRIVER_CONNECT = WM_CAP_START + 10;
        private const uint WM_CAP_DRIVER_DISCONNECT = WM_CAP_START + 11;

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        public static bool IsWebcamInUse()
        {
            IntPtr hCaptureWnd = SendMessage(IntPtr.Zero, WM_CAP_DRIVER_CONNECT, IntPtr.Zero, IntPtr.Zero);
            if (hCaptureWnd != IntPtr.Zero)
            {
                SendMessage(hCaptureWnd, WM_CAP_DRIVER_DISCONNECT, IntPtr.Zero, IntPtr.Zero);
                return true;
            }
            return false;
        }


        #region Abstracted COM interfaces from Windows CoreAudio API
        /***
        [ComImport]
        [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
        internal class MMDeviceEnumerator
        {
        }

        internal enum DEVICE_STATE
        {
            ACTIVE = 0x00000001,
            DISABLED = 0x00000002,
            NOTPRESENT = 0x00000004,
            UNPLUGGED = 0x00000008,
            MASK_ALL = 0x0000000f
        }
        internal enum EDataFlow1
        {
            eRender,
            eCapture,
            eAll,
            EDataFlow_enum_count
        }

        internal enum ERole
        {
            eConsole,
            eMultimedia,
            eCommunications,
            ERole_enum_count
        }


        // Interface representing a collection of multimedia device (MMDevice) objects.
        [Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface IMMDeviceCollection
        {
            [PreserveSig]
            int GetCount(out int numDevices);
            [PreserveSig]
            int Item(int deviceNumber, out IMMDevice device);
        }

        [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface IMMDeviceEnumerator
        {
            int NotImpl1();

            [PreserveSig]
            int GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice ppDevice);

          
            [PreserveSig]
            int EnumAudioEndpoints(
            [In] [MarshalAs(UnmanagedType.I4)] EDataFlow dataFlow,
            [In] [MarshalAs(UnmanagedType.U4)] int stateMask,
            [Out] [MarshalAs(UnmanagedType.Interface)] out IMMDeviceCollection devices);

            [PreserveSig]
            int EnumAudioEndpoints(EDataFlow eCapture, DEVICE_STATE aCTIVE, out IMMDeviceCollection devices);


            // the rest is not implemented
        }

        [Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface IMMDevice
        {
            [PreserveSig]
            int Activate(ref Guid iid, int dwClsCtx, IntPtr pActivationParams, [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);

            // the rest is not implemented
        }

        [Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface IAudioSessionManager2
        {
            int NotImpl1();
            int NotImpl2();

            [PreserveSig]
            int GetSessionEnumerator(out IAudioSessionEnumerator SessionEnum);

            // the rest is not implemented
        }

        [Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface IAudioSessionEnumerator
        {
            [PreserveSig]
            int GetCount(out int SessionCount);

            [PreserveSig]
            int GetSession(int SessionCount, out IAudioSessionControl2 Session);

            [PreserveSig]
            int GetSession(int SessionCount, out IAudioSessionControl Session);
        }

        [Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface ISimpleAudioVolume
        {
            [PreserveSig]
            int SetMasterVolume(float fLevel, ref Guid EventContext);

            [PreserveSig]
            int GetMasterVolume(out float pfLevel);

            [PreserveSig]
            int SetMute(bool bMute, ref Guid EventContext);

            [PreserveSig]
            int GetMute(out bool pbMute);
        }

        internal enum AudioSessionState
        {
            Inactive = 0,
            Active = 1,
            Expired = 2
        }

        [Guid("F4B1A599-7266-4319-A8CA-E70ACB11E8CD"),
         InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface IAudioSessionControl
        {
            void NotNeeded(); // Unused
            void NotNeeded2(); // Unused
            void NotNeeded3(); // Unused
            void GetState(out AudioSessionState state);

            [PreserveSig]
            int GetProcessId(out int pRetVal);
        }

        [Guid("BFA971F1-4D5E-40BB-935E-967039BFBEE4"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface IAudioSessionManager
        {
            void GetAudioSessionControl(IntPtr AudioSessionGuid, int StreamFlags, out IAudioSessionControl SessionControl);
            void GetSessionState(out AudioSessionState state);
            void NotNeeded(); // Unused
            void NotNeeded2(); // Unused

        }


        [Guid("bfb7ff88-7239-4fc9-8fa2-07c950be9c6d"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface IAudioSessionControl2
        {
            // IAudioSessionControl
            [PreserveSig]
            int NotImpl0();

            [PreserveSig]
            int GetDisplayName([MarshalAs(UnmanagedType.LPWStr)] out string pRetVal);

            [PreserveSig]
            int SetDisplayName([MarshalAs(UnmanagedType.LPWStr)]string Value, [MarshalAs(UnmanagedType.LPStruct)] Guid EventContext);

            [PreserveSig]
            int GetIconPath([MarshalAs(UnmanagedType.LPWStr)] out string pRetVal);

            [PreserveSig]
            int SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string Value, [MarshalAs(UnmanagedType.LPStruct)] Guid EventContext);

            [PreserveSig]
            int GetGroupingParam(out Guid pRetVal);

            [PreserveSig]
            int SetGroupingParam([MarshalAs(UnmanagedType.LPStruct)] Guid Override, [MarshalAs(UnmanagedType.LPStruct)] Guid EventContext);

            [PreserveSig]
            int NotImpl1();

            [PreserveSig]
            int NotImpl2();

            // IAudioSessionControl2
            [PreserveSig]
            int GetSessionIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string pRetVal);

            [PreserveSig]
            int GetSessionInstanceIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string pRetVal);

            [PreserveSig]
            int GetProcessId(out int pRetVal);

            [PreserveSig]
            int IsSystemSoundsSession();

            [PreserveSig]
            int SetDuckingPreference(bool optOut);
        }

        // http://netcoreaudio.codeplex.com/SourceControl/latest#trunk/Code/CoreAudio/Interfaces/IAudioEndpointVolume.cs
        [Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IAudioEndpointVolume
        {
            [PreserveSig]
            int NotImpl1();

            [PreserveSig]
            int NotImpl2();

            /// <summary>
            /// Gets a count of the channels in the audio stream.
            /// </summary>
            /// <param name="channelCount">The number of channels.</param>
            /// <returns>An HRESULT code indicating whether the operation passed of failed.</returns>
            [PreserveSig]
            int GetChannelCount(
                [Out] [MarshalAs(UnmanagedType.U4)] out UInt32 channelCount);

            /// <summary>
            /// Sets the master volume level of the audio stream, in decibels.
            /// </summary>
            /// <param name="level">The new master volume level in decibels.</param>
            /// <param name="eventContext">A user context value that is passed to the notification callback.</param>
            /// <returns>An HRESULT code indicating whether the operation passed of failed.</returns>
            [PreserveSig]
            int SetMasterVolumeLevel(
                [In] [MarshalAs(UnmanagedType.R4)] float level,
                [In] [MarshalAs(UnmanagedType.LPStruct)] Guid eventContext);

            /// <summary>
            /// Sets the master volume level, expressed as a normalized, audio-tapered value.
            /// </summary>
            /// <param name="level">The new master volume level expressed as a normalized value between 0.0 and 1.0.</param>
            /// <param name="eventContext">A user context value that is passed to the notification callback.</param>
            /// <returns>An HRESULT code indicating whether the operation passed of failed.</returns>
            [PreserveSig]
            int SetMasterVolumeLevelScalar(
                [In] [MarshalAs(UnmanagedType.R4)] float level,
                [In] [MarshalAs(UnmanagedType.LPStruct)] Guid eventContext);

            /// <summary>
            /// Gets the master volume level of the audio stream, in decibels.
            /// </summary>
            /// <param name="level">The volume level in decibels.</param>
            /// <returns>An HRESULT code indicating whether the operation passed of failed.</returns>
            [PreserveSig]
            int GetMasterVolumeLevel1(
                [Out] [MarshalAs(UnmanagedType.R4)] out float level);

            /// <summary>
            /// Gets the master volume level, expressed as a normalized, audio-tapered value.
            /// </summary>
            /// <param name="level">The volume level expressed as a normalized value between 0.0 and 1.0.</param>
            /// <returns>An HRESULT code indicating whether the operation passed of failed.</returns>
            [PreserveSig]
            int GetMasterVolumeLevelScalar1(
                [Out] [MarshalAs(UnmanagedType.R4)] out float level);

            /// <summary>
            /// Sets the volume level, in decibels, of the specified channel of the audio stream.
            /// </summary>
            /// <param name="channelNumber">The channel number.</param>
            /// <param name="level">The new volume level in decibels.</param>
            /// <param name="eventContext">A user context value that is passed to the notification callback.</param>
            /// <returns>An HRESULT code indicating whether the operation passed of failed.</returns>
            [PreserveSig]
            int SetChannelVolumeLevel(
                [In] [MarshalAs(UnmanagedType.U4)] UInt32 channelNumber,
                [In] [MarshalAs(UnmanagedType.R4)] float level,
                [In] [MarshalAs(UnmanagedType.LPStruct)] Guid eventContext);

            /// <summary>
            /// Sets the normalized, audio-tapered volume level of the specified channel in the audio stream.
            /// </summary>
            /// <param name="channelNumber">The channel number.</param>
            /// <param name="level">The new master volume level expressed as a normalized value between 0.0 and 1.0.</param>
            /// <param name="eventContext">A user context value that is passed to the notification callback.</param>
            /// <returns>An HRESULT code indicating whether the operation passed of failed.</returns>
            [PreserveSig]
            int SetChannelVolumeLevelScalar(
                [In] [MarshalAs(UnmanagedType.U4)] UInt32 channelNumber,
                [In] [MarshalAs(UnmanagedType.R4)] float level,
                [In] [MarshalAs(UnmanagedType.LPStruct)] Guid eventContext);

            /// <summary>
            /// Gets the volume level, in decibels, of the specified channel in the audio stream.
            /// </summary>
            /// <param name="channelNumber">The zero-based channel number.</param>
            /// <param name="level">The volume level in decibels.</param>
            /// <returns>An HRESULT code indicating whether the operation passed of failed.</returns>
            [PreserveSig]
            int GetChannelVolumeLevel(
                [In] [MarshalAs(UnmanagedType.U4)] UInt32 channelNumber,
                [Out] [MarshalAs(UnmanagedType.R4)] out float level);

            /// <summary>
            /// Gets the normalized, audio-tapered volume level of the specified channel of the audio stream.
            /// </summary>
            /// <param name="channelNumber">The zero-based channel number.</param>
            /// <param name="level">The volume level expressed as a normalized value between 0.0 and 1.0.</param>
            /// <returns>An HRESULT code indicating whether the operation passed of failed.</returns>
            [PreserveSig]
            int GetChannelVolumeLevelScalar(
                [In] [MarshalAs(UnmanagedType.U4)] UInt32 channelNumber,
                [Out] [MarshalAs(UnmanagedType.R4)] out float level);

            /// <summary>
            /// Sets the muting state of the audio stream.
            /// </summary>
            /// <param name="isMuted">True to mute the stream, or false to unmute the stream.</param>
            /// <param name="eventContext">A user context value that is passed to the notification callback.</param>
            /// <returns>An HRESULT code indicating whether the operation passed of failed.</returns>
            [PreserveSig]
            int SetMute(
                [In] [MarshalAs(UnmanagedType.Bool)] Boolean isMuted,
                [In] [MarshalAs(UnmanagedType.LPStruct)] Guid eventContext);

            /// <summary>
            /// Gets the muting state of the audio stream.
            /// </summary>
            /// <param name="isMuted">The muting state. True if the stream is muted, false otherwise.</param>
            /// <returns>An HRESULT code indicating whether the operation passed of failed.</returns>
            [PreserveSig]
            int GetMute1(
                [Out] [MarshalAs(UnmanagedType.Bool)] out Boolean isMuted);

            /// <summary>
            /// Gets information about the current step in the volume range.
            /// </summary>
            /// <param name="step">The current zero-based step index.</param>
            /// <param name="stepCount">The total number of steps in the volume range.</param>
            /// <returns>An HRESULT code indicating whether the operation passed of failed.</returns>
            [PreserveSig]
            int GetVolumeStepInfo1(
                [Out] [MarshalAs(UnmanagedType.U4)] out UInt32 step,
                [Out] [MarshalAs(UnmanagedType.U4)] out UInt32 stepCount);

            /// <summary>
            /// Increases the volume level by one step.
            /// </summary>
            /// <param name="eventContext">A user context value that is passed to the notification callback.</param>
            /// <returns>An HRESULT code indicating whether the operation passed of failed.</returns>
            [PreserveSig]
            int VolumeStepUp(
                [In] [MarshalAs(UnmanagedType.LPStruct)] Guid eventContext);

            /// <summary>
            /// Decreases the volume level by one step.
            /// </summary>
            /// <param name="eventContext">A user context value that is passed to the notification callback.</param>
            /// <returns>An HRESULT code indicating whether the operation passed of failed.</returns>
            [PreserveSig]
            int VolumeStepDown(
                [In] [MarshalAs(UnmanagedType.LPStruct)] Guid eventContext);

            /// <summary>
            /// Queries the audio endpoint device for its hardware-supported functions.
            /// </summary>
            /// <param name="hardwareSupportMask">A hardware support mask that indicates the capabilities of the endpoint.</param>
            /// <returns>An HRESULT code indicating whether the operation passed of failed.</returns>
            [PreserveSig]
            int QueryHardwareSupport(
                [Out] [MarshalAs(UnmanagedType.U4)] out UInt32 hardwareSupportMask);

            /// <summary>
            /// Gets the volume range of the audio stream, in decibels.
            /// </summary>
            /// <param name="volumeMin">The minimum volume level in decibels.</param>
            /// <param name="volumeMax">The maximum volume level in decibels.</param>
            /// <param name="volumeStep">The volume increment level in decibels.</param>
            /// <returns>An HRESULT code indicating whether the operation passed of failed.</returns>
            [PreserveSig]
            int GetVolumeRange(
                [Out] [MarshalAs(UnmanagedType.R4)] out float volumeMin,
                [Out] [MarshalAs(UnmanagedType.R4)] out float volumeMax,
                [Out] [MarshalAs(UnmanagedType.R4)] out float volumeStep);
        }
        ***/
        #endregion
    }
}

//https://learn.microsoft.com/en-us/answers/questions/214055/is-it-possible-to-get-status-or-event-when-device C++ check this