using Focus_Bear.Utils;
using System;
using System.Collections.Generic;
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

namespace Focus_Bear.Habits
{
    /// <summary>
    /// Interaction logic for StartBreakActivity.xaml
    /// </summary>
    public partial class StartBreakActivity : Page
    {
        AppSettings appSettings = AppSettings.GetInstance;
        private HabitWindow _parentWindow;
        public StartBreakActivity()
        {

        }
        public StartBreakActivity(HabitWindow parentWindow) : this()
        {
       
            InitializeComponent();
            _parentWindow = parentWindow;
            /*
            "activityInfo":{

                "startBreakActivityTitleText":"It’s time for a break. Do <TIME> of <ACTIVITY> to earn more screentime.",
            */

            String activityName = "";
            int seconds = 0;
            breakActivity currentActivity = new breakActivity();

            if (appSettings.isMandatoryActivityGoing)
            {
                currentActivity = appSettings.arrActivitiesIncludeInEveryBreak[appSettings.nHabitIndex];
            }
            else
            {
                if (appSettings.nHabitIndex != -1)
                {
                    currentActivity = appSettings.arrActivitiesExcludeInEveryBreak[appSettings.nHabitIndex];
                }
            }


            if (!currentActivity.isRandomChoice)
            {
                activityName = currentActivity.name;
            }
            else
            { 
                //random choice
                Random rnd = new Random();
                int nRandom = rnd.Next(0, currentActivity.choices.Count() - 1);
                appSettings.currentActivityChoice = currentActivity.choices[nRandom]; //currentActivityChoice is the choice that we need to perform as the first activity.

                if (currentActivity.choices.Count() != 1) //There should at least be 2
                {
                    //We need to check that the random choice selected now is not the one we did last.
                    foreach (LastShownActivityChoice activity in appSettings.arrLastShownRandomChoice)
                    {
                        if (activity.ActivityId == currentActivity.id)
                        {
                            while (activity.ChoiceId == appSettings.currentActivityChoice.id)
                            {
                                Random rnd1 = new Random();
                                int nRandom1 = rnd1.Next(0, currentActivity.choices.Count() - 1);
                                appSettings.currentActivityChoice = currentActivity.choices[nRandom1];
                            }
                        }
                    }

                    int index = appSettings.arrLastShownRandomChoice.FindIndex(s => s.ActivityId == currentActivity.id);

                    if (index != -1) //Replace this one as the one done last
                        appSettings.arrLastShownRandomChoice[index] = new LastShownActivityChoice(currentActivity.id, appSettings.currentActivityChoice.id);
                    else
                        appSettings.arrLastShownRandomChoice.Add(new LastShownActivityChoice(currentActivity.id, appSettings.currentActivityChoice.id));

                }  
                 
                activityName = appSettings.currentActivityChoice.name;
            }

            seconds = currentActivity.durationSeconds;

            string sHeading = appSettings.ConfigFileStrings["activityInfo"]["startBreakActivityTitleText"].ToString();
           
            string sButtonTitle = appSettings.ConfigFileStrings["activityInfo"]["startActivityButtonText"].ToString();
            
            if (currentActivity.useTimer)
            {
                sHeading = sHeading.Replace("<ACTIVITY>", activityName);
                Heading.Text = sHeading.Replace("<TIME>", appSettings.SecondsToText(seconds));

                sButtonTitle = sButtonTitle.Replace("<NAME>", activityName);
                btnStartNextActivityTXT.Text = sButtonTitle.Replace("<TIME>", appSettings.SecondsToText(seconds));
            }
            else
            {
                sHeading = appSettings.ConfigFileStrings["activityInfo"]["startNonTimerBreakActivityTitleText"].ToString();
                sHeading = sHeading.Replace("<ACTIVITY>", activityName);
                Heading.Text = sHeading;

                sButtonTitle = appSettings.ConfigFileStrings["activityInfo"]["startNonTimerActivityButtonText"].ToString();
                sButtonTitle = sButtonTitle.Replace("<NAME>", activityName);
                btnStartNextActivityTXT.Text = sButtonTitle;
            }

            /// addLog(text: "start-break-activity")//complete break activity//





            //if (appSettings.isMandatoryActivityGoing)
            //{
            //    breakActivity currentActivity = appSettings.arrActivitiesIncludeInEveryBreak[appSettings.currentActivityIndex];


            //     if (!currentActivity.isRandomChoice)
            //    {
            //        ///timerViewcontrollerObj?.startTimerActivity(durationInSeconds: currentActivity.durationSeconds, name: currentActivity.name!, url: currentActivity.urlsArray.randomElement(), logQuantityQuestion: currentActivity.logQuantityQuestion, currentActivity: currentActivity, currentChoice: nil, logQuantity: currentActivity.logQuantity, logQuantityType: currentActivity.logQuantityType)
            //    }
            //    else
            //    {
            //        //random choice
            //        if let randomChoice = thirdVcObj?.currentActivityChoice
            //                {//currentActivity.choices.randomElement(){
            //            timerViewcontrollerObj?.startTimerActivity(durationInSeconds: currentActivity.durationSeconds, name: randomChoice.name, url: randomChoice.urlsArray.randomElement(), logQuantityQuestion: randomChoice.logQuantityQuestion, currentActivity: currentActivity, currentChoice: randomChoice, logQuantity: randomChoice.logQuantity, logQuantityType: randomChoice.logQuantityType)
            //        }else if let randomChoice = currentActivity.choices.randomElement(){
            //            timerViewcontrollerObj?.startTimerActivity(durationInSeconds: currentActivity.durationSeconds, name: randomChoice.name, url: randomChoice.urlsArray.randomElement(), logQuantityQuestion: randomChoice.logQuantityQuestion, currentActivity: currentActivity, currentChoice: randomChoice, logQuantity: randomChoice.logQuantity, logQuantityType: randomChoice.logQuantityType)
            //     }
            //    }


            //}
            //else
            //{
            //    breakActivity currentActivity = appSettings.arrActivitiesExcludeInEveryBreak[appSettings.currentActivityIndex];
            //   if (!currentActivity.isRandomChoice){
            //        timerViewcontrollerObj?.startTimerActivity(durationInSeconds: currentActivity.durationSeconds, name: currentActivity.name!, url: currentActivity.urlsArray.randomElement(), logQuantityQuestion: currentActivity.logQuantityQuestion, currentActivity: currentActivity, currentChoice: nil, logQuantity: currentActivity.logQuantity, logQuantityType: currentActivity.logQuantityType)
            //     }
            //    else
            //    {//random choice
            //        if let randomChoice = thirdVcObj?.currentActivityChoice
            //         {//currentActivity.choices.randomElement(){
            //            timerViewcontrollerObj?.startTimerActivity(durationInSeconds: currentActivity.durationSeconds, name: randomChoice.name, url: randomChoice.urlsArray.randomElement(), logQuantityQuestion: randomChoice.logQuantityQuestion, currentActivity: currentActivity, currentChoice: randomChoice, logQuantity: randomChoice.logQuantity, logQuantityType: randomChoice.logQuantityType)
            //        }
            //        else if let randomChoice = currentActivity.choices.randomElement()
            //         {
            //            timerViewcontrollerObj?.startTimerActivity(durationInSeconds: currentActivity.durationSeconds, name: randomChoice.name, url: randomChoice.urlsArray.randomElement(), logQuantityQuestion: randomChoice.logQuantityQuestion, currentActivity: currentActivity, currentChoice: randomChoice, logQuantity: randomChoice.logQuantity, logQuantityType: randomChoice.logQuantityType)
            //        }
            //    }

            //}



        }

        private void btnStartNextActivity_Click(object sender, RoutedEventArgs e)
        {
            _parentWindow.ShowActivityStartScreen();
        }
    }
}
