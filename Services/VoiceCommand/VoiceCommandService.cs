using Focus_Bear.API;
using Focus_Bear.Utils;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Focus_Bear.Services.VoiceCommand
{
    /// <summary>
    /// Turns a recorded voice command into an <see cref="IntentResponse"/> via the Focus Bear backend
    /// (Focus-Bear/backend voice module): POST the recording to /voice/transcribe, then the transcript
    /// to /voice/intent. The backend owns the model, prompt and API key, so nothing secret ships in
    /// the app. Failures surface as <see cref="VoiceCommandException"/>.
    /// </summary>
    public static class VoiceCommandService
    {
        // The backend validates clientDatetime against exactly this format.
        private const string ClientDatetimeFormat = "yyyy-MM-dd HH:mm:ss";

        // Bounds enforced by the backend; exceeding them 413s / 400s the whole command, so they are
        // checked here first.
        private const long MaxRecordingBytes = 10L * 1024 * 1024;
        private const int MaxHabitNames = 100;
        private const int MaxHabitNameLength = 200;

        public static async Task<IntentResponse> ProcessVoiceCommand(string wavFile)
        {
            LogWriter.LogWrite("VoiceCommandService ProcessVoiceCommand()");
            EnsureRecordingIsSendable(wavFile);

            var api = new FocusBearAPIClient();
            VoiceTranscription transcription = await api.TranscribeVoice(wavFile);
            string transcript = transcription.Text?.Trim();
            if (string.IsNullOrEmpty(transcript))
            {
                throw new VoiceCommandException(VoiceCommandFailure.NothingHeard, "Transcription was empty");
            }

            IntentResponse intent = await api.ClassifyVoiceIntent(new VoiceIntentRequest
            {
                Transcript = transcript,
                Habits = CurrentRoutineHabitNames(),
                ClientDatetime = DateTime.Now.ToString(ClientDatetimeFormat, CultureInfo.InvariantCulture),
            });
            intent.transcription = transcript;
            return intent;
        }

        private static void EnsureRecordingIsSendable(string wavFile)
        {
            var recording = new FileInfo(wavFile);
            if (!recording.Exists)
            {
                throw new VoiceCommandException(VoiceCommandFailure.Failed, "Recording not found: " + wavFile);
            }
            if (recording.Length > MaxRecordingBytes)
            {
                throw new VoiceCommandException(VoiceCommandFailure.TooLong, "Recording is " + recording.Length + " bytes");
            }
        }

        // Names of the habits in the routine that is running now, so the backend can match a spoken
        // "start <habit>" to a real habit.
        private static List<string> CurrentRoutineHabitNames()
        {
            AppSettings appSettings = AppSettings.GetInstance;
            IEnumerable<habit> habits;
            switch (appSettings.nCurrentActivityType)
            {
                case CurrentActivityType.MORNING_HABITS:
                    habits = appSettings.arrAllMorningHabits;
                    break;
                case CurrentActivityType.EVENING_HABITS:
                    habits = appSettings.arrAllEveningHabits;
                    break;
                default:
                    return new List<string>();
            }

            return habits
                .Select(routineHabit => routineHabit.nameWithoutIcon?.Trim())
                .Where(name => !string.IsNullOrEmpty(name) && name.Length <= MaxHabitNameLength)
                .Take(MaxHabitNames)
                .ToList();
        }
    }
}
