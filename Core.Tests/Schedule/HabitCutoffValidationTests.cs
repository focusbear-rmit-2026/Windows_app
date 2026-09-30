using System.Collections.Generic;
using FocusBear.Core.Schedule;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.Schedule
{
    /// <summary>
    /// Ported from the Mac app's <c>HabitCutoffValidationTests.swift</c>.
    ///
    /// A habit's late cutoff stands on its own: the habit editor must not reject a cutoff for falling
    /// outside the window between the global shutdown/startup times, nor for being earlier than the
    /// global non-high-priority cutoff (bug report 2026-09-09). The editor keeps only the validation
    /// that is genuinely the client's job — name, duration, days, videos, questions, tags.
    ///
    /// The Swift tests set the global <c>startTime</c> / <c>shutOffTime</c> / <c>cutOffTime</c> in
    /// setUp to prove the validator ignores them. This port has no such globals to set — the validator
    /// takes only the form — so the same fact is pinned by accepting a cutoff at every minute of the
    /// day, including the ones those globals used to forbid.
    /// </summary>
    [TestClass]
    public class HabitCutoffValidationTests
    {
        private const int GlobalStartupMinutes = 7 * 60;    // the 07:00 the old rule compared against
        private const int GlobalShutdownMinutes = 21 * 60;  // 21:00
        private const int GlobalCutoffMinutes = 22 * 60;    // 22:00
        private const int MinutesPerDay = 24 * 60;

        /// <summary>A must-do evening habit with a cutoff enabled — the only form that shows the field.</summary>
        private static HabitEditValues EveningModel(int cutoffMinutes)
        {
            HabitEditValues values = HabitEditValues.NewHabit(RoutineSettingsSection.Evening);
            values.Name = "Read";
            values.CutoffEnabled = true;
            values.CutoffMinutes = cutoffMinutes;
            return values;
        }

        private static HabitValidationFailure? Validate(HabitEditValues values)
        {
            return HabitEditValidator.Validate(values, RoutineSettingsSection.Evening);
        }

        [TestMethod]
        public void CutoffBeforeTheGlobalCutoffIsAccepted()
        {
            // 21:30 is inside the evening window but EARLIER than the global 22:00 cutoff — the old rule.
            Assert.IsNull(Validate(EveningModel(21 * 60 + 30)));
            Assert.IsTrue(21 * 60 + 30 < GlobalCutoffMinutes);
        }

        [TestMethod]
        public void CutoffBeforeTheEveningRoutineStartIsAccepted()
        {
            // 18:10 is before the 21:00 shutdown — the exact case from the bug report's screenshot.
            Assert.IsNull(Validate(EveningModel(18 * 60 + 10)));
            Assert.IsTrue(18 * 60 + 10 < GlobalShutdownMinutes);
        }

        [TestMethod]
        public void CutoffAfterTheMorningRoutineStartIsAccepted()
        {
            // 09:00 sits past the 07:00 startup, i.e. outside the overnight window entirely.
            Assert.IsNull(Validate(EveningModel(9 * 60)));
            Assert.IsTrue(9 * 60 > GlobalStartupMinutes);
        }

        [TestMethod]
        public void CutoffIsAcceptedAtEveryMinuteOfTheDay()
        {
            for (int minute = 0; minute < MinutesPerDay; minute += 15)
            {
                Assert.IsNull(Validate(EveningModel(minute)), "cutoff at minute " + minute + " should be allowed");
            }
        }

        /// <summary>Removing the cutoff rule must not weaken the validation that is still the client's job.</summary>
        [TestMethod]
        public void NamelessHabitIsStillRejected()
        {
            HabitEditValues values = HabitEditValues.NewHabit(RoutineSettingsSection.Evening);
            values.Name = "   ";

            Assert.AreEqual(HabitValidationFailure.Name, Validate(values));
        }

        [TestMethod]
        public void OverlongDurationIsStillRejected()
        {
            HabitEditValues values = HabitEditValues.NewHabit(RoutineSettingsSection.Evening);
            values.Name = "Read";
            values.DurationSeconds = HabitEditValues.MaxDurationSeconds + 1;

            Assert.AreEqual(HabitValidationFailure.DurationRange, Validate(values));
        }

        [TestMethod]
        public void AZeroLengthTimedHabitIsStillRejected()
        {
            HabitEditValues values = HabitEditValues.NewHabit(RoutineSettingsSection.Evening);
            values.Name = "Read";
            values.DurationSeconds = 0;

            Assert.AreEqual(HabitValidationFailure.DurationRange, Validate(values));
        }

        [TestMethod]
        public void EmptyWeeklyScheduleIsStillRejected()
        {
            HabitEditValues values = HabitEditValues.NewHabit(RoutineSettingsSection.Evening);
            values.Name = "Read";
            values.DaysOfWeek = new HashSet<string>();

            Assert.AreEqual(HabitValidationFailure.Days, Validate(values));
        }

        /// <summary>A custom routine's weekly schedule lives on the ROUTINE, so an empty set on the
        /// habit is not an error there.</summary>
        [TestMethod]
        public void ACustomRoutineHabitIsNotAskedForDays()
        {
            HabitEditValues values = HabitEditValues.NewHabit(RoutineSettingsSection.Custom);
            values.Name = "Read";
            values.DaysOfWeek = new HashSet<string>();

            Assert.IsNull(HabitEditValidator.Validate(values, RoutineSettingsSection.Custom));
            Assert.IsFalse(HabitEditValidator.ShowsDaysOfWeek(RoutineSettingsSection.Custom));
        }

        [TestMethod]
        public void AHabitWithoutATimerNeedsACompletionRequirement()
        {
            HabitEditValues values = HabitEditValues.NewHabit(RoutineSettingsSection.Evening);
            values.Name = "Read";
            values.UsesTimer = false;

            Assert.AreEqual(HabitValidationFailure.Completion, Validate(values));

            values.CompletionRequirement = "until the chapter ends";
            Assert.IsNull(Validate(values), "and its duration is then irrelevant");
        }

        [TestMethod]
        public void OnlyYouTubeAndVimeoVideosAreAccepted()
        {
            HabitEditValues values = HabitEditValues.NewHabit(RoutineSettingsSection.Evening);
            values.Name = "Stretch";
            values.VideoUrls = new List<string> { "https://www.youtube.com/watch?v=abc", "  " };
            Assert.IsNull(Validate(values), "blank rows are dropped before the check");

            values.VideoUrls = new List<string> { "https://example.com/video" };
            Assert.AreEqual(HabitValidationFailure.Video, Validate(values));
        }

        [TestMethod]
        public void ALogQuestionNeedsEitherNoRangeOrAValidOne()
        {
            HabitEditValues values = HabitEditValues.NewHabit(RoutineSettingsSection.Evening);
            values.Name = "Push-ups";
            values.LogQuantity = true;
            values.LogQuestions = new List<HabitLogQuestionEdit>
            {
                new HabitLogQuestionEdit("q1", "How many?"),
            };
            Assert.IsNull(Validate(values), "blank min AND max is unset, which is valid");

            values.LogQuestions[0].MinValue = "5";
            Assert.AreEqual(HabitValidationFailure.Questions, Validate(values), "a min without a max is not");

            values.LogQuestions[0].MaxValue = "5";
            Assert.AreEqual(HabitValidationFailure.Questions, Validate(values), "and min must be below max");

            values.LogQuestions[0].MaxValue = "50";
            Assert.IsNull(Validate(values));

            // A habit with random sub-habit choices skips the question rule entirely, as on the Mac.
            values.LogQuestions[0].MaxValue = "1";
            values.RandomChoiceCount = 2;
            Assert.IsNull(Validate(values));
        }

        [TestMethod]
        public void TagsMustLookLikeTags()
        {
            HabitEditValues values = HabitEditValues.NewHabit(RoutineSettingsSection.Evening);
            values.Name = "Read";
            values.Tags = new List<string> { "Deep work (focus)", "health & fitness" };
            Assert.IsNull(Validate(values));

            values.Tags = new List<string> { "1st thing" };
            Assert.AreEqual(HabitValidationFailure.Tags, Validate(values), "a tag starts with a letter");
        }

        /// <summary>
        /// Where the cutoff field appears at all: must-do EVENING habits only (web advanced tab). It is
        /// still never validated — the two are separate questions, and conflating them is what produced
        /// the reported rejection.
        /// </summary>
        [TestMethod]
        public void TheCutoffFieldIsOfferedOnlyForMustDoEveningHabits()
        {
            HabitEditValues mustDo = EveningModel(18 * 60);
            Assert.IsTrue(HabitEditValidator.ShowsCutoff(RoutineSettingsSection.Evening, mustDo));

            mustDo.IsHighPriority = false;
            Assert.IsFalse(HabitEditValidator.ShowsCutoff(RoutineSettingsSection.Evening, mustDo));
            Assert.IsFalse(HabitEditValidator.ShowsCutoff(RoutineSettingsSection.Morning, EveningModel(18 * 60)));

            // Priority itself is an evening / custom-routine option; a morning habit is always standard.
            Assert.IsFalse(HabitEditValidator.ShowsPriority(RoutineSettingsSection.Morning));
            Assert.IsTrue(HabitEditValidator.ShowsPriority(RoutineSettingsSection.Evening));
        }

        [TestMethod]
        public void ANewHabitTakesItsRoutinesDefaultLength()
        {
            Assert.AreEqual(300, HabitEditValues.NewHabit(RoutineSettingsSection.Morning).DurationSeconds);
            Assert.AreEqual(600, HabitEditValues.NewHabit(RoutineSettingsSection.Evening).DurationSeconds);
            Assert.AreEqual(60, HabitEditValues.NewHabit(RoutineSettingsSection.Custom).DurationSeconds);
            // And runs every day until the user says otherwise.
            CollectionAssert.AreEquivalent(
                new List<string>(WeekdayCode.All),
                new List<string>(HabitEditValues.NewHabit(RoutineSettingsSection.Morning).DaysOfWeek));
        }
    }
}
