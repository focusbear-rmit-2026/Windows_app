using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Media;
using System.Text;
using System.Threading.Tasks;

namespace Focus_Bear.Utils
{
    public static class Sounds
    {
        private static AppSettings appSettings = AppSettings.GetInstance;

        public static void PlayHabitFinishedSound()
        {
            if (appSettings.bDisableNotificationSounds) return;
            /*Completing_a_Habit_4
                 * grunz__success_low
                 */
            try
            {
                Random rnd = new Random();
                int nRandom = rnd.Next(1, 3);

                Stream str = Focus_Bear.Properties.Resources.Completing_a_Habit_4;

                if (nRandom == 2)
                {
                    str = Focus_Bear.Properties.Resources.grunz__success_low;
                }
               
                SoundPlayer player = new System.Media.SoundPlayer(str);
                player.Play();
            }
            catch (Exception ex)
            {
               LogWriter.LogWrite("PlayHabitFinishedSound() ex - " + ex.Message );
            }
        }
        public static void PlayRoutineStartSound()
        {
            if (appSettings.bDisableNotificationSounds) return;
            /*StartingMorningEveningRoutine_2
             StartingMorningEveningRoutine_4
             StartingMorningEveningRoutine_5*/
            try
            {
                Random rnd = new Random();
                int nRandom = rnd.Next(1, 3);

                Stream str = Focus_Bear.Properties.Resources.StartingMorningEveningRoutine_2;

                if (nRandom == 2)
                {
                    str = Focus_Bear.Properties.Resources.StartingMorningEveningRoutine_4;
                }
                else if (nRandom == 3)
                {
                    str = Focus_Bear.Properties.Resources.StartingMorningEveningRoutine_5;
                }
               
                SoundPlayer player = new System.Media.SoundPlayer(str);
                player.Play();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("PlayHabitFinishedSound() ex - " + ex.Message);
            }
        }
        public static void PlayMorningRoutineFinishedSound()
        {
            if (appSettings.bDisableNotificationSounds) return;
            /*CompletingWholeMorningRoutine_2
             CompletingWholeMorningRoutine_3*/
          
            try
            {
                Random rnd = new Random();
                int nRandom = rnd.Next(1, 3);

                Stream str = Focus_Bear.Properties.Resources.CompletingWholeMorningRoutine_2;

                if (nRandom == 2)
                {
                    str = Focus_Bear.Properties.Resources.CompletingWholeMorningRoutine_3;
                }

                SoundPlayer player = new System.Media.SoundPlayer(str);
                player.Play();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("PlayMorningRoutineFinishedSound() ex - " + ex.Message);
            }
        }

        public static void PlayEveningRoutineFinishedSound()
        {
            if (appSettings.bDisableNotificationSounds) return;
            /*CompletingWholeEveningRoutine_4
             CompletingWholeEveningRoutine_5
             CompletingWholeEveningRoutine_7*/
            try
            {
                Random rnd = new Random();
                int nRandom = rnd.Next(1, 3);

                Stream str = Focus_Bear.Properties.Resources.CompletingWholeEveningRoutine_4;

                if (nRandom == 2)
                {
                    str = Focus_Bear.Properties.Resources.CompletingWholeEveningRoutine_5;
                }
                else if (nRandom == 3)
                {
                    str = Focus_Bear.Properties.Resources.CompletingWholeEveningRoutine_7;
                }

                SoundPlayer player = new System.Media.SoundPlayer(str);
                player.Play();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("PlayEveningRoutineFinishedSound() ex - " + ex.Message);
            }
        }

        public static void PlayFocusModeStartSound()
        {
            if (appSettings.bDisableNotificationSounds) return;
            /*StartFocus_1
            StartFocus_2*/
            try
            {
                Random rnd = new Random();
                int nRandom = rnd.Next(1, 3);

                Stream str = Focus_Bear.Properties.Resources.StartFocus_1;

                if (nRandom == 2)
                {
                    str = Focus_Bear.Properties.Resources.StartFocus_2;
                }

                SoundPlayer player = new System.Media.SoundPlayer(str);
                player.Play();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("PlayFocusModeStartSound() ex - " + ex.Message);
            }
        }
        public static void PlayFocusModeEndSound()
        {
            PlayHabitFinishedSound();
        }
    }
}
