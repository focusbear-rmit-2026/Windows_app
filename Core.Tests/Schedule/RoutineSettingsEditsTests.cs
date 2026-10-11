using System;
using System.Collections.Generic;
using System.Linq;
using FocusBear.Core.Schedule;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

namespace FocusBear.Core.Tests.Schedule
{
    /// <summary>
    /// The Day Plan's "Edit routine timing" and habit "Edit" writers (report 2026-09-25_09-05-05:
    /// "add the same additional actions for day plan (edit, shorten) and ability to edit routine
    /// timing that we have on mac"). Ported rules from the Mac's DayPlanRoutineDraft.
    /// </summary>
    [TestClass]
    public class RoutineSettingsEditsTests
    {
        private static JObject Settings()
        {
            return JObject.Parse(@"{
                ""startup_time"": ""06:30"",
                ""shutdown_time"": ""21:00"",
                ""cutoff_time_for_non_high_priority_activities"": ""22:30"",
                ""unrelated"": 42,
                ""morning_activities"": [
                    { ""id"": ""m1"", ""name"": ""Yoga"", ""habit_icon"": ""🧘"", ""duration_seconds"": 300,
                      ""days_of_week"": [""weekdays""], ""video_urls"": [""https://youtu.be/x""] },
                    { ""id"": ""m2"", ""name"": ""Log energy"", ""completion_requirements"": ""Enter a number"" }
                ],
                ""evening_activities"": [
                    { ""id"": ""e1"", ""name"": ""Read"", ""duration_seconds"": 900, ""priority"": ""STANDARD"",
                      ""days_of_week"": [""MON"", ""WED""] }
                ]
            }");
        }

        // MARK: - Time strings

        [TestMethod]
        public void TimeStringsRoundTrip()
        {
            Assert.AreEqual(6 * 60 + 30, RoutineTiming.MinutesFromTimeString("06:30"));
            Assert.AreEqual(21 * 60 + 5, RoutineTiming.MinutesFromTimeString("21:05:00"));
            Assert.AreEqual("06:05", RoutineTiming.TimeStringFromMinutes(6 * 60 + 5));
            Assert.AreEqual("00:00", RoutineTiming.TimeStringFromMinutes(0));
        }

        [TestMethod]
        public void UnreadableTimesAreNull()
        {
            Assert.IsNull(RoutineTiming.MinutesFromTimeString(""));
            Assert.IsNull(RoutineTiming.MinutesFromTimeString(null));
            Assert.IsNull(RoutineTiming.MinutesFromTimeString("7am"));
            Assert.IsNull(RoutineTiming.MinutesFromTimeString("25:00"));
            Assert.IsNull(RoutineTiming.MinutesFromTimeString("-1:30"));
        }

        [TestMethod]
        public void TypedTimesReadTheWayPeopleWriteThem()
        {
            Assert.AreEqual(7 * 60 + 30, RoutineTiming.ParseTypedTime("7:30"));
            Assert.AreEqual(7 * 60 + 30, RoutineTiming.ParseTypedTime("07:30"));
            Assert.AreEqual(19 * 60 + 30, RoutineTiming.ParseTypedTime("19:30"));
            Assert.AreEqual(19 * 60 + 30, RoutineTiming.ParseTypedTime("7:30pm"));
            Assert.AreEqual(19 * 60 + 30, RoutineTiming.ParseTypedTime(" 7:30 PM "));
            Assert.AreEqual(19 * 60, RoutineTiming.ParseTypedTime("7pm"));
            Assert.AreEqual(7 * 60 + 5, RoutineTiming.ParseTypedTime("7.05am"));
            Assert.AreEqual(0, RoutineTiming.ParseTypedTime("12am"), "12am is midnight");
            Assert.AreEqual(12 * 60, RoutineTiming.ParseTypedTime("12pm"), "12pm is noon");
            Assert.AreEqual(9 * 60, RoutineTiming.ParseTypedTime("9"));
            Assert.AreEqual(21 * 60 + 15, RoutineTiming.ParseTypedTime("9:15 p.m."));
        }

        [TestMethod]
        public void NonsenseIsNotATime()
        {
            foreach (string text in new[] { "", "  ", "abc", "25:00", "7:60", "13pm", "0am", "7:5", "1:2:3", "123" })
                Assert.IsNull(RoutineTiming.ParseTypedTime(text), text);
        }

        // MARK: - Evening cutoff validity (Mac isEveningCutoffValid)

        [TestMethod]
        public void CutoffMustFallInsideTheEveningWindowAcrossMidnight()
        {
            int evening = 21 * 60, morning = 6 * 60 + 30;
            Assert.IsTrue(RoutineTiming.IsEveningCutoffValid(null, evening, morning), "no cutoff is fine");
            Assert.IsTrue(RoutineTiming.IsEveningCutoffValid(22 * 60 + 30, evening, morning));
            Assert.IsTrue(RoutineTiming.IsEveningCutoffValid(1 * 60, evening, morning), "after midnight, before morning");
            Assert.IsTrue(RoutineTiming.IsEveningCutoffValid(morning, evening, morning), "the morning start itself");
            Assert.IsFalse(RoutineTiming.IsEveningCutoffValid(8 * 60, evening, morning), "after the morning starts");
            Assert.IsFalse(RoutineTiming.IsEveningCutoffValid(20 * 60, evening, morning), "before the evening starts");
        }

        [TestMethod]
        public void CutoffWithNoKnownMorningOnlyNeedsToBeATimeOfDay()
        {
            Assert.IsTrue(RoutineTiming.IsEveningCutoffValid(8 * 60, 21 * 60, null));
            Assert.IsFalse(RoutineTiming.IsEveningCutoffValid(24 * 60, 21 * 60, null));
        }

        [TestMethod]
        public void EveningStartingAtTheMorningStartHasNoWindowForACutoff()
        {
            Assert.IsFalse(RoutineTiming.IsEveningCutoffValid(22 * 60, 6 * 60, 6 * 60));
        }

        // MARK: - Applying timing

        [TestMethod]
        public void MorningTimingWritesOnlyTheStartupTime()
        {
            JObject s = Settings();
            Assert.IsTrue(RoutineTiming.Apply(s, RoutineSettingsSection.Morning, 7 * 60 + 15));
            Assert.AreEqual("07:15", (string)s["startup_time"]);
            Assert.AreEqual("21:00", (string)s["shutdown_time"]);
            Assert.AreEqual("22:30", (string)s["cutoff_time_for_non_high_priority_activities"]);
            Assert.AreEqual(42, (int)s["unrelated"]);
        }

        [TestMethod]
        public void EveningTimingWritesStartAndCutoff()
        {
            JObject s = Settings();
            Assert.IsTrue(RoutineTiming.Apply(s, RoutineSettingsSection.Evening, 20 * 60, 23 * 60));
            Assert.AreEqual("20:00", (string)s["shutdown_time"]);
            Assert.AreEqual("23:00", (string)s["cutoff_time_for_non_high_priority_activities"]);
        }

        [TestMethod]
        public void ClearingTheEveningCutoffRemovesTheKey()
        {
            JObject s = Settings();
            Assert.IsTrue(RoutineTiming.Apply(s, RoutineSettingsSection.Evening, 20 * 60, null));
            Assert.IsNull(s["cutoff_time_for_non_high_priority_activities"]);
            Assert.IsNull(RoutineTiming.EveningCutoffMinutes(s));
        }

        [TestMethod]
        public void InvalidEveningTimingChangesNothing()
        {
            JObject s = Settings();
            string before = s.ToString();
            Assert.IsFalse(RoutineTiming.Apply(s, RoutineSettingsSection.Evening, 20 * 60, 9 * 60),
                           "a cutoff after the next morning starts is refused");
            Assert.IsFalse(RoutineTiming.Apply(s, RoutineSettingsSection.Morning, 24 * 60));
            Assert.IsFalse(RoutineTiming.Apply(s, RoutineSettingsSection.Custom, 9 * 60));
            Assert.AreEqual(before, s.ToString());
        }

        [TestMethod]
        public void ReadsTheCurrentTiming()
        {
            JObject s = Settings();
            Assert.AreEqual(6 * 60 + 30, RoutineTiming.StartMinutes(s, RoutineSettingsSection.Morning));
            Assert.AreEqual(21 * 60, RoutineTiming.StartMinutes(s, RoutineSettingsSection.Evening));
            Assert.AreEqual(22 * 60 + 30, RoutineTiming.EveningCutoffMinutes(s));
        }

        // MARK: - Habit edit

        [TestMethod]
        public void LocatesAHabitInEitherRoutine()
        {
            JObject s = Settings();
            Assert.AreEqual(RoutineSettingsSection.Morning, HabitSettingsEdit.Locate(s, "m2").Item1);
            Assert.AreEqual(RoutineSettingsSection.Evening, HabitSettingsEdit.Locate(s, "e1").Item1);
            Assert.IsNull(HabitSettingsEdit.Locate(s, "nope"));
            Assert.IsNull(HabitSettingsEdit.Locate(s, null));
        }

        [TestMethod]
        public void ReadsTheEditorsFields()
        {
            JObject s = Settings();
            HabitEditValues yoga = HabitSettingsEdit.Read(HabitSettingsEdit.Locate(s, "m1").Item2);
            Assert.AreEqual("Yoga", yoga.Name);
            Assert.IsTrue(yoga.UsesTimer);
            Assert.AreEqual(300, yoga.DurationSeconds);
            CollectionAssert.AreEquivalent(new[] { "MON", "TUE", "WED", "THU", "FRI" }, yoga.DaysOfWeek.ToList());

            HabitEditValues log = HabitSettingsEdit.Read(HabitSettingsEdit.Locate(s, "m2").Item2);
            Assert.IsFalse(log.UsesTimer, "a completion requirement makes it an \"I'm done\" habit");
            Assert.AreEqual(7, log.DaysOfWeek.Count, "no day list means every day");

            HabitEditValues read = HabitSettingsEdit.Read(HabitSettingsEdit.Locate(s, "e1").Item2);
            Assert.IsFalse(read.IsHighPriority);
        }

        [TestMethod]
        public void WritingKeepsKeysTheEditorDoesNotShow()
        {
            JObject s = Settings();
            JObject entry = HabitSettingsEdit.Locate(s, "m1").Item2;
            HabitEditValues v = HabitSettingsEdit.Read(entry);
            v.Name = "  Morning yoga ";
            v.DurationSeconds = 600;
            HabitSettingsEdit.Write(entry, RoutineSettingsSection.Morning, "🧘", v);

            Assert.AreEqual("Morning yoga", (string)entry["name"]);
            Assert.AreEqual(600, (int)entry["duration_seconds"]);
            Assert.AreEqual("https://youtu.be/x", (string)entry["video_urls"][0]);
            Assert.IsNull(entry["priority"], "morning habits don't carry a priority");
            CollectionAssert.AreEqual(new[] { "weekdays" }, entry["days_of_week"].Select(d => (string)d).ToList(),
                                      "unchanged days keep their stored token");
        }

        [TestMethod]
        public void ChangedDaysAreWrittenAsOrderedCodes()
        {
            JObject s = Settings();
            JObject entry = HabitSettingsEdit.Locate(s, "e1").Item2;
            HabitEditValues v = HabitSettingsEdit.Read(entry);
            v.DaysOfWeek = new HashSet<string> { "SUN", "MON" };
            v.IsHighPriority = true;
            HabitSettingsEdit.Write(entry, RoutineSettingsSection.Evening, "", v);

            CollectionAssert.AreEqual(new[] { "MON", "SUN" }, entry["days_of_week"].Select(d => (string)d).ToList());
            Assert.AreEqual("HIGH", (string)entry["priority"]);
        }

        [TestMethod]
        public void SwitchingToATimerDropsTheCompletionRequirement()
        {
            JObject s = Settings();
            JObject entry = HabitSettingsEdit.Locate(s, "m2").Item2;
            HabitEditValues v = HabitSettingsEdit.Read(entry);
            v.UsesTimer = true;
            v.DurationSeconds = 120;
            HabitSettingsEdit.Write(entry, RoutineSettingsSection.Morning, "", v);

            Assert.IsNull(entry["completion_requirements"]);
            Assert.AreEqual(120, (int)entry["duration_seconds"]);
            Assert.IsTrue(HabitSettingsEdit.Read(entry).UsesTimer);
        }

        [TestMethod]
        public void DisplayNameMatchesTheAppsParser()
        {
            Assert.AreEqual("🧘 Yoga", HabitSettingsEdit.DisplayName("🧘", "Yoga"));
            Assert.AreEqual("Yoga", HabitSettingsEdit.DisplayName("", " Yoga "));
        }
        // MARK: - The full editor (every field the Mac editor offers)

        private static JObject Rich()
        {
            return JObject.Parse(@"{
                ""evening_activities"": [ {
                    ""id"": ""e9"", ""name"": ""Wind down"", ""habit_icon"": ""🌙"", ""duration_seconds"": 600,
                    ""priority"": ""HIGH"", ""cutoff_time_for_doing_activity"": ""23:15"",
                    ""video_urls"": [ ""https://youtu.be/a"" ],
                    ""allowed_apps"": [ ""Kindle"" ], ""allowed_urls"": [ ""example.com"" ], ""run_micro_breaks"": true,
                    ""show_saved_distracting_websites"": true,
                    ""log_quantity"": true, ""log_summary_type"": ""AVG"",
                    ""log_quantity_questions"": [
                        { ""id"": ""q1"", ""question"": ""Mood"", ""min_value"": 0, ""max_value"": 10, ""min_value_description"": ""low"", ""log_summary_type"": ""AVG"" },
                        { ""id"": ""q2"", ""question"": ""Pages"", ""min_value"": 0, ""max_value"": 0, ""log_summary_type"": ""AVG"" } ],
                    ""take_notes"": ""END_OF_ACTIVITY"",
                    ""choice_type"": ""check_list"", ""check_list"": [ ""Dim lights"", ""Tea"" ],
                    ""text_instructions"": ""Slow down"",
                    ""image_urls"": [ { ""url"": ""https://img/1.png"", ""file_path"": ""x/1.png"" } ],
                    ""tags"": [ ""Sleep"" ]
                } ]
            }");
        }

        [TestMethod]
        public void ReadsEveryField()
        {
            HabitEditValues v = HabitSettingsEdit.Read(HabitSettingsEdit.Locate(Rich(), "e9").Item2);
            Assert.AreEqual("🌙", v.Icon);
            Assert.IsTrue(v.CutoffEnabled);
            Assert.AreEqual(23 * 60 + 15, v.CutoffMinutes);
            CollectionAssert.AreEqual(new[] { "https://youtu.be/a" }, v.VideoUrls.ToList());
            CollectionAssert.AreEqual(new[] { "Kindle" }, v.AllowedApps.ToList());
            CollectionAssert.AreEqual(new[] { "example.com" }, v.AllowedUrls.ToList());
            Assert.IsTrue(v.RunMicroBreaks);
            Assert.IsTrue(v.ShowSavedDistractingWebsites);
            Assert.IsTrue(v.LogQuantity);
            Assert.AreEqual("AVG", v.LogSummaryType);
            Assert.AreEqual(2, v.LogQuestions.Count);
            Assert.AreEqual("10", v.LogQuestions[0].MaxValue);
            Assert.AreEqual("", v.LogQuestions[1].MinValue, "0/0 is 'unset' and reads blank");
            Assert.AreEqual(HabitNotes.End, v.TakeNotes);
            CollectionAssert.AreEqual(new[] { "Dim lights", "Tea" }, v.Checklist.ToList());
            Assert.AreEqual("Slow down", v.TextInstructions);
            CollectionAssert.AreEqual(new[] { "https://img/1.png" }, v.ImageUrls.ToList());
            CollectionAssert.AreEqual(new[] { "Sleep" }, v.Tags.ToList());
            Assert.AreEqual(0, v.RandomChoiceCount);
        }

        [TestMethod]
        public void AnUntouchedReadWriteRoundTripKeepsTheEntry()
        {
            JObject s = Rich();
            JObject entry = HabitSettingsEdit.Locate(s, "e9").Item2;
            string before = entry.ToString();
            HabitEditValues v = HabitSettingsEdit.Read(entry);
            HabitSettingsEdit.Write(entry, RoutineSettingsSection.Evening, v.Icon, v);
            Assert.IsTrue(JToken.DeepEquals(JObject.Parse(before), entry), "before:\n" + before + "\nafter:\n" + entry);
        }

        [TestMethod]
        public void WritesEveryField()
        {
            JObject entry = HabitSettingsEdit.Locate(Rich(), "e9").Item2;
            HabitEditValues v = HabitSettingsEdit.Read(entry);
            v.CutoffEnabled = false;
            v.VideoUrls = new List<string> { " https://vimeo.com/1 ", "" };
            v.AllowedUrls = new List<string> { "https://news.site/x" };
            v.LogQuestions[0].Question = " Energy ";
            v.LogSummaryType = HabitNotes.SummarySum;
            v.TakeNotes = HabitNotes.During;
            v.Checklist = new List<string>();
            v.TextInstructions = "Breathe";
            v.ImageUrls = new List<string> { "https://img/1.png", "https://img/2.png" };
            v.Tags = new List<string> { "Sleep", "Calm" };
            HabitSettingsEdit.Write(entry, RoutineSettingsSection.Evening, "🌙", v);

            Assert.IsNull(entry["cutoff_time_for_doing_activity"], "cutoff off removes the key");
            CollectionAssert.AreEqual(new[] { "https://vimeo.com/1" }, Strings(entry["video_urls"]));
            CollectionAssert.AreEqual(new[] { "news.site/x" }, Strings(entry["allowed_urls"]), "the scheme is stripped");
            Assert.AreEqual("Energy", (string)entry["log_quantity_questions"][0]["question"]);
            Assert.AreEqual("low", (string)entry["log_quantity_questions"][0]["min_value_description"], "unshown keys survive");
            Assert.AreEqual("SUM", (string)entry["log_quantity_questions"][0]["log_summary_type"]);
            Assert.AreEqual("DURING_ACTIVITY", (string)entry["take_notes"]);
            Assert.IsNull(entry["check_list"]);
            Assert.IsNull(entry["choice_type"], "an emptied checklist drops its choice type");
            Assert.AreEqual("Breathe", (string)entry["text_instructions"]);
            Assert.AreEqual("x/1.png", (string)entry["image_urls"][0]["file_path"], "a kept image keeps its file path");
            Assert.AreEqual("https://img/2.png", (string)entry["image_urls"][1]["url"]);
            CollectionAssert.AreEqual(new[] { "Sleep", "Calm" }, Strings(entry["tags"]));
        }

        [TestMethod]
        public void AFocusModeReplacesAllowedAppsAndMicroBreaks()
        {
            JObject entry = HabitSettingsEdit.Locate(Rich(), "e9").Item2;
            HabitEditValues v = HabitSettingsEdit.Read(entry);
            v.SetUsesFocusMode(true, "fm-1");
            Assert.IsFalse(v.CanRunMicroBreaks);
            HabitSettingsEdit.Write(entry, RoutineSettingsSection.Evening, "", v);
            Assert.AreEqual("fm-1", (string)entry["allowed_focus_mode_id"]);
            Assert.AreEqual(0, ((JArray)entry["allowed_apps"]).Count);
            Assert.AreEqual(0, ((JArray)entry["allowed_urls"]).Count);
            Assert.IsFalse((bool)entry["run_micro_breaks"]);

            v.SetUsesFocusMode(false, null);
            HabitSettingsEdit.Write(entry, RoutineSettingsSection.Evening, "", v);
            Assert.IsNull(entry["allowed_focus_mode_id"], "no focus mode removes the key (the API wants a UUID or nothing)");
        }

        [TestMethod]
        public void LogDataOffClearsTheQuestions()
        {
            JObject entry = HabitSettingsEdit.Locate(Rich(), "e9").Item2;
            HabitEditValues v = HabitSettingsEdit.Read(entry);
            v.LogQuantity = false;
            HabitSettingsEdit.Write(entry, RoutineSettingsSection.Evening, "", v);
            Assert.IsFalse((bool)entry["log_quantity"]);
            Assert.AreEqual(0, ((JArray)entry["log_quantity_questions"]).Count);
        }

        [TestMethod]
        public void RandomChoiceHabitsKeepTheirLogKeysAndChoices()
        {
            JObject s = JObject.Parse(@"{ ""morning_activities"": [ { ""id"": ""r"", ""name"": ""Pick"", ""duration_seconds"": 60,
                ""choice_type"": ""random"", ""choices"": [ { ""name"": ""a"" }, { ""name"": ""b"" } ], ""log_quantity"": false,
                ""log_summary_type"": ""SUM"" } ] }");
            JObject entry = HabitSettingsEdit.Locate(s, "r").Item2;
            HabitEditValues v = HabitSettingsEdit.Read(entry);
            Assert.AreEqual(2, v.RandomChoiceCount);
            v.LogQuantity = true;
            v.LogSummaryType = HabitNotes.SummaryAverage;
            HabitSettingsEdit.Write(entry, RoutineSettingsSection.Morning, "", v);
            Assert.AreEqual("random", (string)entry["choice_type"]);
            Assert.AreEqual(2, ((JArray)entry["choices"]).Count);
            Assert.IsFalse((bool)entry["log_quantity"]);
            Assert.AreEqual("SUM", (string)entry["log_summary_type"]);
        }

        [TestMethod]
        public void MorningHabitsNeverGetACutoff()
        {
            JObject s = Settings();
            JObject entry = HabitSettingsEdit.Locate(s, "m1").Item2;
            HabitEditValues v = HabitSettingsEdit.Read(entry);
            v.CutoffEnabled = true;
            HabitSettingsEdit.Write(entry, RoutineSettingsSection.Morning, "", v);
            Assert.IsNull(entry["cutoff_time_for_doing_activity"]);
        }

        [TestMethod]
        public void DeletingRemovesJustThatHabit()
        {
            JObject s = Settings();
            Assert.IsTrue(HabitSettingsEdit.Delete(s, "m1"));
            Assert.IsNull(HabitSettingsEdit.Locate(s, "m1"));
            Assert.IsNotNull(HabitSettingsEdit.Locate(s, "m2"));
            Assert.AreEqual(1, ((JArray)s["morning_activities"]).Count);
            Assert.IsFalse(HabitSettingsEdit.Delete(s, "m1"));
        }

        [TestMethod]
        public void TheValidatorCatchesTheNewFields()
        {
            HabitEditValues v = HabitSettingsEdit.Read(HabitSettingsEdit.Locate(Rich(), "e9").Item2);
            Assert.IsNull(HabitEditValidator.Validate(v, RoutineSettingsSection.Evening));
            v.VideoUrls = new List<string> { "https://example.com/v" };
            Assert.AreEqual(HabitValidationFailure.Video, HabitEditValidator.Validate(v, RoutineSettingsSection.Evening));
            v.VideoUrls = new List<string>();
            v.LogQuestions[0].MinValue = "5";
            v.LogQuestions[0].MaxValue = "2";
            Assert.AreEqual(HabitValidationFailure.Questions, HabitEditValidator.Validate(v, RoutineSettingsSection.Evening));
            v.LogQuestions[0].MaxValue = "9";
            v.Tags = new List<string> { "9lives" };
            Assert.AreEqual(HabitValidationFailure.Tags, HabitEditValidator.Validate(v, RoutineSettingsSection.Evening));
        }

        private static List<string> Strings(JToken t) => ((JArray)t).Select(x => (string)x).ToList();
    }
}
