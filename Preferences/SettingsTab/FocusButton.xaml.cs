using Focus_Bear.Models;
using Focus_Bear.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.IO.Compression;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using static Focus_Bear.Services.FocusButton.FocusButtonService;
using Path = System.IO.Path;
using static System.Windows.Forms.AxHost;
using System.Threading;
using EspFlasherWpf;
using System.IO.Ports;
using EspDotNet.Config;
using EspDotNet.Communication;
using EspDotNet.Tools;
using EspDotNet;
using EspDotNet.Tools.Firmware;

namespace Focus_Bear.Preferences.SettingsTab
{
    /// <summary>
    /// Interaction logic for FocusButton.xaml
    /// </summary>
    public partial class FocusButton : UserControl
    {
        AppSettings appSettings = AppSettings.GetInstance;
        public static event Action OnFirmwareDownloadFinished;
        FocusBearMainWindow ParentWindow;
        string sFirmwarePath = "";
        private CancellationTokenSource _cts;
        public static void RaiseFirmwareDownloadFinished()
        {
            OnFirmwareDownloadFinished?.Invoke();
        }
        public FocusButton()
        {
        }
        public FocusButton(FocusBearMainWindow _parentWindow)
        {
            InitializeComponent();
            ParentWindow = _parentWindow;
            try
            {
                //todo win texts in entire class

                FocusButtonHead.Text = "Focus Button";
                FocusButtonDesc.Text = "The FocusButton device connects to your computer using a USB cable and works together with the app to help manage focus sessions and actions. Once connected, the app automatically detects the device and establishes communication with it.";
                ConnectionText.Text = "CONNECTED";

                btnConnectTXT.Text = "Connect";

                DoNotHaveDevice.Text = "Do not have a Focus Button Device?";
                btnGetFocusButtonDeviceTXT.Text = "Get a Focus Button Device";

                Installed.Text = "INSTALLED FIRMWARE";
                Version.Text = "Version: ";
                VersionVal.Text = appSettings.sFirmwareVersion;
                btnCheckForUpdatesTXT.Text = "Check for Updates";
                UpdateInfo.Text = "A new version is available. Click Flash Firmware to download and install the latest Firmware on the device";
                btnFlashFirmwareTXT.Text = "Flash Firmware";

                OnFirmwareDownloadFinished += PrepareForFirmwareInstall;

            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("FocusButton text setting ex -" + ex.Message);
            }
        }

        private void btnGetFocusButtonDevice_Click(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("btnGetFocusButtonDevice_Click");
            try
            {
                System.Diagnostics.Process.Start("https://button.focusbear.io/");
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("btnGetFocusButtonDevice_Click ex - " + ex.Message);
            }
        }

        private async void btnConnect_Click(object sender, RoutedEventArgs e)
        {
            App.FocusButton.Start();

            await Task.Delay(1);

            SetConnectedState();
        }

        private void btnCheckForUpdates_Click(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("Focus button btnGetFocusButtonDevice_Click");
            Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
            CheckForUpdate();
            Mouse.OverrideCursor = null;

        }

        private void UserControl_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if(IsVisible)
            {
                VersionVal.Text = appSettings.sFirmwareVersion;

                SetConnectedState();
            }

        }

        private void SetConnectedState()
        {
            if (App.FocusButton._state == DeviceState.Running || App.FocusButton._state == DeviceState.Idle)
            {
                ConnectionBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DFF5E5"));
                ConnectionBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#34C759"));
                ConnectionDot.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#34C759"));
                ConnectionText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#228B22"));
                ConnectionText.Text = "CONNECTED";
                VersionVal.Text = appSettings.sFirmwareVersion;

                btnConnect.IsEnabled = false;
                InstalledFirmwareSection.Visibility = Visibility.Visible;

                NoDeviceSection.Visibility = Visibility.Collapsed;
            }
            else if (App.FocusButton._state == DeviceState.Disconnected)
            {
                ConnectionBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FDEAEA"));
                ConnectionBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E74C3C"));
                ConnectionDot.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E74C3C"));
                ConnectionText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#C0392B"));
                ConnectionText.Text = "DISCONNECTED";

                btnConnect.IsEnabled = true;
                InstalledFirmwareSection.Visibility = Visibility.Collapsed;

                NoDeviceSection.Visibility = Visibility.Visible;
            }
        }
        public async void CheckForUpdate()
        {
            LogWriter.LogWrite("CheckForUpdate()");

            await Task.Factory.StartNew(async () =>
            {
                try
                {
                    //check for focus button firmware update
                    var updater = new AppUpdater();

                    var status = await updater.CheckForUpdate(true);
                    if (status)
                    {
                        await Dispatcher.BeginInvoke((Action)(() =>
                        {
                            //Waiting if existing dialoge is showing
                            LogWriter.LogWrite("A new version of Focus Button Firmware is available");
                            {
                                string displayMsg = "A new version of Focus Button Firmware is available. Do you wish to download the latest version of Focus Button Firmware?";
                                FBMessageBox ff = new FBMessageBox(displayMsg, appSettings.ConfigFileStrings["WindowsSpecific"]["newUpdateAvailable"].ToString(), MessageBoxButton.YesNo);
                                ff.Owner = ParentWindow;
                                ff.ShowDialog();
                                //if user has cancelled the update process
                                if (ff.btnClicked == MessageBoxResult.No)
                                {
                                    return;
                                }
                            }

                            UpdateWin winUpdate = new UpdateWin(ref updater);
                            winUpdate.Topmost = true;
                            winUpdate.Owner = ((App)Application.Current).wndFocusBearMainWindow;
                            winUpdate.Show();
                            winUpdate.Activate();
                            winUpdate.Focus();
                        }));
                    }
                    else
                    {
                        await Dispatcher.BeginInvoke((Action)(() =>
                        {
                            string displayMsg = "You are already running the latest version of Focus Button Firmwre.";  
                            FBMessageBox ff = new FBMessageBox(displayMsg, appSettings.ConfigFileStrings["WindowsSpecific"]["noNewUpdateAvailable"].ToString());
                            ff.Owner = ParentWindow;
                            ff.ShowDialog();

                        }));
                    }
                }
                catch (Exception ex)
                {
                    LogWriter.LogWrite("Focus Button CheckForUpdate() ex - " + ex.Message);
                }
            });
        }

        public void PrepareForFirmwareInstall()
        {
            LogWriter.LogWrite("Focus Button PrepareForFirmwareInstall()");

            string zipPath = Path.Combine(AppSettings.AppDataDirPath, "FirmwareUpdate.zip");
            string extractPath = Path.Combine(AppSettings.AppDataDirPath, "FirmwareUpdate");
            try
            {
                // Delete existing extraction folder
                if (Directory.Exists(extractPath))
                {
                    Directory.Delete(extractPath, true);
                }

                // Create fresh folder
                Directory.CreateDirectory(extractPath);

                // Extract ZIP
                ZipFile.ExtractToDirectory(zipPath, extractPath);

                //Once extracted we need to look for the firmware.bin file in that folder and if found, update the message and enable the flash button
                if (Directory.Exists(extractPath))
                {
                    sFirmwarePath = Path.Combine(extractPath, "firmware.bin");

                    if (!File.Exists(sFirmwarePath))
                    {
                        LogWriter.LogWrite("PrepareForFirmwareInstall() firmware.bin was not found in the extracted firmware package");
                        return;                        
                    }
                    else
                    {
                        UpdateInfo.Text = "New Firmware updates downloaded. Click on \"Flash Firmware\" button to install the latest firmware on the device. Flashing reboots the device into its bootloader, verifies the image matches the chip, and restarts — settings and habits in NVS are kept. The serial connection is released during flashing and restored afterwards.";
                        FlashRequiredSection.Visibility = Visibility.Visible;
                        TxtFlashStatus.Text = "";
                    }
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("PrepareForFirmwareInstall() ex -  " + ex.Message);
            }
        }

        private async void btnFlashFirmware_Click(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("btnFlashFirmware_Click()");

            if (!File.Exists(sFirmwarePath))
            {
                LogWriter.LogWrite("btnFlashFirmware_Click() firmware.bin was not found in the extracted firmware package");
                return;
            }

            uint offset = 0x10000;


            // The flasher needs exclusive access to the COM port.
            bool wasConnected = App.FocusButton._state != DeviceState.Disconnected;
            if (wasConnected) App.FocusButton.Disconnect();

            btnFlashFirmware.IsEnabled = false;
            btnCheckForUpdates.IsEnabled = false;
            PbFlash.Value = 0;
            var progress = new Progress<float>(p => PbFlash.Value = p * 100);
            var status = new Progress<string>(s => { TxtFlashStatus.Text = s; });
            _cts = new CancellationTokenSource();

            try
            {
                await FlashFirmwareAsync(App.FocusButton.sPortName, sFirmwarePath, offset, progress, status, _cts.Token).ConfigureAwait(true);
                LogWriter.LogWrite("btnFlashFirmware_Click() - Done. Device reset and booting new firmware.");

                TxtFlashStatus.Text = "Flash complete — device is rebooting.";

                if (wasConnected)
                {
                    await System.Threading.Tasks.Task.Delay(2000);  // let USB re-enumerate

                    App.FocusButton.Connect(App.FocusButton.sPortName);  // re-runs the handshake incl. GET|FIRMWAREVER
                }
            }
            catch (OperationCanceledException)
            {
                LogWriter.LogWrite("btnFlashFirmware_Click() - Upload cancelled.");
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite($"btnFlashFirmware_Click() - ERROR: {ex.Message}");
                //MessageBox.Show(this, ex.Message, "Upload failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _cts.Dispose();
                _cts = null;
                btnFlashFirmware.IsEnabled = true;
                btnCheckForUpdates.IsEnabled = true;
                PbFlash.Value = 0;

                FlashRequiredSection.Visibility = Visibility.Collapsed;
            }
        }

        private async Task FlashFirmwareAsync(string portName, string firmwarePath, uint offset, IProgress<float> progress = null, IProgress<string> status = null, CancellationToken token = default)
        {
            // Bundled device definitions + bootloader/reset pin sequences.
            var config = ConfigProvider.LoadDefaultConfig();

            using (var port = new SerialPort(portName, 115200))
            {
                port.Open();
                var communicator = new Communicator(port)
                {
                   // LogProgress = new Progress<string>(Log)
                };

                var bootloader = await EnterBootloaderAsync(communicator, token).ConfigureAwait(true);
                status.Report("Detecting chip...");
                LogWriter.LogWrite("FlashFirmwareAsync - Detecting chip...");
                var chipDetectTool = new ChipTypeDetectTool(bootloader, config);
                var deviceConfig = await chipDetectTool.DetectAndGetDeviceConfigAsync(token).ConfigureAwait(true);
                status.Report($"Detected: {deviceConfig.ChipType}");
                LogWriter.LogWrite($"FlashFirmwareAsync - Detected: {deviceConfig.ChipType}");

                // This sample targets ESP32-C3 / ESP32-S3, but any chip with a bundled stub will work.
                if (deviceConfig.ChipType != ChipTypes.ESP32c3 && deviceConfig.ChipType != ChipTypes.ESP32s3)
                    LogWriter.LogWrite($"FlashFirmwareAsync -  Warning: this sample was written for ESP32-C3/S3, continuing anyway with {deviceConfig.ChipType}.");

                status.Report("Uploading stub loader to RAM...");
                LogWriter.LogWrite("FlashFirmwareAsync - Uploading stub loader to RAM...");
                var stub = DefaultFirmwareProviders.GetSoftloaderForDevice(deviceConfig.ChipType);
                var ramUploadTool = new RamUploadTool(bootloader, deviceConfig);
                var softLoaderTool = new SoftLoaderTool(communicator, ramUploadTool);
                var softLoader = await softLoaderTool.StartAsync(stub, token).ConfigureAwait(true);

                status.Report($"Flashing {Path.GetFileName(firmwarePath)} at 0x{offset:X}...");
                LogWriter.LogWrite($"FlashFirmwareAsync - Flashing {Path.GetFileName(firmwarePath)} at 0x{offset:X}...");

                byte[] firmwareBytes = File.ReadAllBytes(firmwarePath);
                var firmwareProvider = new FirmwareProvider(
                    entryPoint: 0,
                    segments: new List<IFirmwareSegmentProvider>
                    {
                        new FirmwareSegmentProvider(offset, firmwareBytes)
                    });

                var uploadTool = new FlashUploadDeflatedTool(softLoader, deviceConfig);
                var firmwareUploadTool = new FirmwareUploadTool(uploadTool)
                {
                    Progress = new Progress<float>(p => PbFlash.Value = p * 100)
                };
                await firmwareUploadTool.UploadFirmwareAsync(firmwareProvider, token).ConfigureAwait(true);

                status.Report("Resetting device...");
                LogWriter.LogWrite("FlashFirmwareAsync - Resetting device...");
                var resetTool = new ResetDeviceTool(communicator, config.ResetSequence);
                await resetTool.ResetAsync(token).ConfigureAwait(true);
            }
        }

        // Some boards' DTR/RTS-to-reset mapping isn't known in advance (e.g. ESP32-C3/S3 native
        // USB-Serial-JTAG peripherals latch download-mode intent from the DTR/RTS edge sequence
        // itself, differently from the classic external-bridge auto-reset circuit). Try each known
        // candidate in turn - toggling these control lines is harmless - and keep whichever works.
        private async Task<EspDotNet.Loaders.ESP32BootLoader.ESP32BootLoader> EnterBootloaderAsync(Communicator communicator, CancellationToken token)
        {
            Exception lastError = null;

            foreach (var (name, sequence) in BootloaderResetSequences.Candidates)
            {
                LogWriter.LogWrite($"Entering ROM bootloader using '{name}' reset sequence...");
                var bootloaderTool = new BootloaderTool(communicator, sequence)
                {
                    //LogProgress = new Progress<string>(Log)
                };

                try
                {
                    return await bootloaderTool.StartBootloaderAsync(token).ConfigureAwait(true);
                }
                catch (Exception ex) when (!(ex is OperationCanceledException))
                {
                    LogWriter.LogWrite($"'{name}' did not work, trying next candidate if any...");
                    lastError = ex;
                }
            }

            throw lastError;
        }

    }
}
