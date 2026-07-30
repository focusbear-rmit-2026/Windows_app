using Focus_Bear.Services;
using Focus_Bear.Services.VoiceCommand;
using Focus_Bear.Utils;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Focus_Bear.Focus
{
    /// <summary>
    /// In-app microphone voice-command window (opened from the tray-menu mic button).
    /// Mirrors the Mac "VoiceView": recording starts immediately, the user speaks their command
    /// (e.g. "remind me to call mom tomorrow at 9am"), then Done transcribes it, classifies the
    /// intent and executes it via the shared <see cref="VoiceIntentDispatcher"/>. Supports the full
    /// intent set (create task, start/stop focus, start habit, ...), same as the Focus Button.
    /// </summary>
    public partial class VoiceCommandWindow : Window
    {
        private enum VoiceState { Listening, Processing, Done, Error }

        private readonly AppSettings appSettings = AppSettings.GetInstance;
        private AudioRecorder _recorder;
        private VoiceState _state;
        private bool _busy;
        private Storyboard _spinnerStoryboard;

        public VoiceCommandWindow()
        {
            InitializeComponent();
            Page_Theme();
            App.ThemeChanged += Page_Theme;
            Loaded += VoiceCommandWindow_Loaded;
        }

        private void VoiceCommandWindow_Loaded(object sender, RoutedEventArgs e)
        {
            StartListening();
        }

        private void StartListening()
        {
            try
            {
                ResultText.Visibility = Visibility.Collapsed;
                TranscriptText.Visibility = Visibility.Collapsed;
                SetState(VoiceState.Listening);

                _recorder = new AudioRecorder();
                _recorder.StartRecording();
                LogWriter.LogWrite("VoiceCommandWindow - started recording");
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("VoiceCommandWindow StartListening ex - " + ex.Message);
                ShowError("Couldn't access the microphone.");
            }
        }

        // Done while listening → stop recording, transcribe, classify and dispatch.
        private async void FinishAndProcess()
        {
            if (_busy) return;
            _busy = true;

            try
            {
                SetState(VoiceState.Processing);

                if (_recorder != null && _recorder.IsRecording)
                    await _recorder.StopRecording();

                IntentResponse result = await OpenAIService.ProcessVoiceCommand(
                    OpenAIService.OpenAIApiKey, appSettings.RecordingPath);

                if (result != null && !string.IsNullOrWhiteSpace(result.transcription))
                {
                    TranscriptText.Text = "“" + result.transcription.Trim() + "”";
                    TranscriptText.Visibility = Visibility.Visible;
                }

                if (result == null || result.confidence == "low")
                {
                    ShowError("Sorry, I didn't catch that. Please try again.");
                    return;
                }

                // Menu path is not the physical device, so pause/resume is echoed to the device.
                VoiceDispatchResult disp = await VoiceIntentDispatcher.DispatchAsync(result, fromPhysicalDevice: false);

                // Command not understood → keep the popup open so the user can try again.
                if (!disp.Recognized)
                {
                    ShowError(string.IsNullOrEmpty(disp.Message)
                        ? "Sorry, I didn't understand that. Please try again."
                        : disp.Message);
                    return;
                }

                // Understood (valid action) → show a brief confirmation, then dismiss automatically.
                if (!string.IsNullOrEmpty(disp.Message))
                {
                    ResultText.Text = disp.Message;
                    ResultText.Foreground = disp.Success
                        ? new SolidColorBrush(Color.FromRgb(0x2a, 0x9d, 0x3a))
                        : new SolidColorBrush(Color.FromRgb(0xc0, 0x39, 0x2b));
                    ResultText.Visibility = Visibility.Visible;
                }

                SetState(VoiceState.Done);
                await Task.Delay(1400);
                Close();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("VoiceCommandWindow FinishAndProcess ex - " + ex.Message);
                ShowError("Something went wrong. Please try again.");
            }
            finally
            {
                _busy = false;
            }
        }

        private void ShowError(string message)
        {
            ResultText.Text = message;
            ResultText.Foreground = new SolidColorBrush(Color.FromRgb(0xc0, 0x39, 0x2b));
            ResultText.Visibility = Visibility.Visible;
            SetState(VoiceState.Error);
        }

        private void SetState(VoiceState state)
        {
            _state = state;
            switch (state)
            {
                case VoiceState.Listening:
                    PromptText.Text = "Tell me your plans for the day…";
                    MicGlyph.Visibility = Visibility.Visible;
                    StopSpinner();
                    StartPulse();
                    btnPrimary.Content = "Done";
                    btnPrimary.Visibility = Visibility.Visible;
                    btnSecondary.Content = "Cancel";
                    btnSecondary.Visibility = Visibility.Visible;
                    break;

                case VoiceState.Processing:
                    PromptText.Text = "Thinking…";
                    StopPulse();
                    MicGlyph.Visibility = Visibility.Collapsed;
                    StartSpinner();
                    btnPrimary.Visibility = Visibility.Collapsed;
                    btnSecondary.Content = "Cancel";
                    btnSecondary.Visibility = Visibility.Visible;
                    break;

                case VoiceState.Done:
                    PromptText.Text = "Got it!";
                    StopSpinner();
                    StopPulse();
                    MicGlyph.Visibility = Visibility.Visible;
                    btnPrimary.Content = "Done";
                    btnPrimary.Visibility = Visibility.Visible;
                    btnSecondary.Visibility = Visibility.Collapsed;
                    break;

                case VoiceState.Error:
                    PromptText.Text = "Hmm…";
                    StopSpinner();
                    StopPulse();
                    MicGlyph.Visibility = Visibility.Visible;
                    btnPrimary.Content = "Try again";
                    btnPrimary.Visibility = Visibility.Visible;
                    btnSecondary.Content = "Close";
                    btnSecondary.Visibility = Visibility.Visible;
                    break;
            }
        }

        private void btnPrimary_Click(object sender, RoutedEventArgs e)
        {
            switch (_state)
            {
                case VoiceState.Listening:
                    FinishAndProcess();
                    break;
                case VoiceState.Error:
                    StartListening();
                    break;
                case VoiceState.Done:
                default:
                    Close();
                    break;
            }
        }

        private async void btnSecondary_Click(object sender, RoutedEventArgs e)
        {
            // Cancel / Close — discard any in-progress recording and close.
            try
            {
                if (_recorder != null && _recorder.IsRecording)
                    await _recorder.StopRecording();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("VoiceCommandWindow cancel ex - " + ex.Message);
            }
            Close();
        }

        private void Window_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try { DragMove(); } catch { }
        }

        // ── Animations ────────────────────────────────────────────────────────

        private void StartPulse()
        {
            PulseOuter.Visibility = Visibility.Visible;
            PulseInner.Visibility = Visibility.Visible;
        }

        private void StopPulse()
        {
            PulseOuter.Visibility = Visibility.Collapsed;
            PulseInner.Visibility = Visibility.Collapsed;
        }

        private void StartSpinner()
        {
            Spinner.Visibility = Visibility.Visible;
            var anim = new DoubleAnimation(0, 360, new Duration(TimeSpan.FromSeconds(1)))
            {
                RepeatBehavior = RepeatBehavior.Forever
            };
            _spinnerStoryboard = new Storyboard();
            _spinnerStoryboard.Children.Add(anim);
            Storyboard.SetTarget(anim, Spinner);
            Storyboard.SetTargetProperty(anim,
                new PropertyPath("(UIElement.RenderTransform).(RotateTransform.Angle)"));
            _spinnerStoryboard.Begin();
        }

        private void StopSpinner()
        {
            _spinnerStoryboard?.Stop();
            _spinnerStoryboard = null;
            Spinner.Visibility = Visibility.Collapsed;
        }

        private void Page_Theme()
        {
            try
            {
                bool isDark = appSettings.sTheme == "Dark";
                if (isDark)
                {
                    if (Application.Current.Resources["DarkBackground"] != null)
                        RootBorder.Background = Application.Current.Resources["DarkBackground"] as Brush;
                    else
                        RootBorder.Background = new SolidColorBrush(Color.FromRgb(0x2b, 0x2b, 0x2b));
                    PromptText.Foreground = Brushes.White;
                    TranscriptText.Foreground = new SolidColorBrush(Color.FromRgb(0xcc, 0xcc, 0xcc));
                }
                else
                {
                    RootBorder.Background = Brushes.White;
                    PromptText.Foreground = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33));
                    TranscriptText.Foreground = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55));
                }
            }
            catch { }
        }

        protected override void OnClosed(EventArgs e)
        {
            App.ThemeChanged -= Page_Theme;
            StopSpinner();
            try { _recorder?.Dispose(); } catch { }
            _recorder = null;
            base.OnClosed(e);
        }
    }
}
