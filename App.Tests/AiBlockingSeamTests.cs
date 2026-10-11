using System.Collections.Generic;
using Focus_Bear.Utils;
using FocusBear.Core.Blocking;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using WpfBlockingSchedule = Focus_Bear.Models.CustomBlockingSchedule;

namespace FocusBear.App.Tests
{
    /// <summary>
    /// The ladder itself is covered by Core's AiBlockingDecisionTests. These cover what Core cannot
    /// see: that the app finds the right focus mode, reads <c>is_ai_enabled</c> out of the raw server
    /// blob, resolves the schedule's block list by id, and treats a paused mode as not running.
    /// </summary>
    [TestClass]
    public class AiBlockingSeamTests
    {
        private const string BlockListId = "block-list-1";

        private static FocusMode Mode(string id, bool isOn, string metadataJson = null, bool isAllowedListApproach = true)
        {
            return new FocusMode
            {
                id = id,
                isOn = isOn,
                isAllowedListApproach = isAllowedListApproach,
                serverMetadata = metadataJson == null ? null : JObject.Parse(metadataJson),
            };
        }

        private static WpfBlockingSchedule Schedule(string focusModeId, bool ownAiFlag)
        {
            return new WpfBlockingSchedule { SelectedFocusModeId = focusModeId, bAIBlockingEnabled = ownAiFlag };
        }

        private static bool Decide(AiBlocking.AppState state)
        {
            return AiBlockingDecision.IsAiBlockingEnabled(AiBlocking.ToInputs(state));
        }

        [TestMethod]
        public void TheRunningModesAiSwitchVetoesTheGlobalToggle()
        {
            var state = new AiBlocking.AppState
            {
                FocusModeRunning = true,
                GlobalAiCheckUrlEnabled = true,
                FocusModes = new List<FocusMode>
                {
                    Mode("other", isOn: false),
                    Mode("running", isOn: true, metadataJson: "{\"is_ai_enabled\":false}"),
                },
            };

            Assert.IsFalse(Decide(state), "AI turned off for this block list on the Mac must stay off on Windows");
        }

        [TestMethod]
        public void OnlyTheModeThatIsOnIsConsulted()
        {
            var state = new AiBlocking.AppState
            {
                FocusModeRunning = true,
                GlobalAiCheckUrlEnabled = true,
                FocusModes = new List<FocusMode>
                {
                    Mode("off", isOn: false, metadataJson: "{\"is_ai_enabled\":false}"),
                    Mode("running", isOn: true),
                },
            };

            Assert.IsTrue(Decide(state), "a veto on a mode that is not running must not apply");
        }

        [TestMethod]
        public void APausedModeIsNotTheRunningContext()
        {
            var state = new AiBlocking.AppState
            {
                FocusModeRunning = true,
                FocusModePaused = true,
                GlobalAiCheckUrlEnabled = true,
                FocusModes = new List<FocusMode> { Mode("running", isOn: true, metadataJson: "{\"is_ai_enabled\":false}") },
            };

            Assert.IsTrue(Decide(state));
        }

        [TestMethod]
        public void OnlyABooleanCountsAsTheSwitch()
        {
            // A string is not what any client writes. Reading it as absent (= on) is the safe
            // failure: it cannot silently turn AI off.
            var state = new AiBlocking.AppState
            {
                FocusModeRunning = true,
                GlobalAiCheckUrlEnabled = true,
                FocusModes = new List<FocusMode> { Mode("running", isOn: true, metadataJson: "{\"is_ai_enabled\":\"false\"}") },
            };

            Assert.IsTrue(Decide(state));
        }

        [TestMethod]
        public void AScheduleIsDecidedByTheBlockListItNames()
        {
            var state = new AiBlocking.AppState
            {
                GlobalAiCheckUrlEnabled = false,
                FocusModes = new List<FocusMode> { Mode(BlockListId, isOn: false) },
                ActiveSchedule = Schedule(BlockListId, ownAiFlag: false),
            };

            Assert.IsTrue(Decide(state), "an allow list with no is_ai_enabled key reads as on, and the global toggle is not consulted");
        }

        [TestMethod]
        public void AScheduleRunningABlacklistNeverRunsAi()
        {
            var state = new AiBlocking.AppState
            {
                GlobalAiCheckUrlEnabled = true,
                FocusModes = new List<FocusMode> { Mode(BlockListId, isOn: false, isAllowedListApproach: false) },
                ActiveSchedule = Schedule(BlockListId, ownAiFlag: true),
            };

            Assert.IsFalse(Decide(state));
        }

        [TestMethod]
        public void TheSchedulesOwnFlagAnswersOnlyWhenItsBlockListIsMissing()
        {
            var state = new AiBlocking.AppState
            {
                FocusModes = new List<FocusMode> { Mode("unrelated", isOn: false, isAllowedListApproach: false) },
                ActiveSchedule = Schedule("deleted-or-unsynced", ownAiFlag: true),
            };

            Assert.IsTrue(Decide(state));

            state.ActiveSchedule = Schedule("deleted-or-unsynced", ownAiFlag: false);
            Assert.IsFalse(Decide(state));
        }

        [TestMethod]
        public void NoFocusModesAtAllFallsBackToTheGlobalToggle()
        {
            // arrFocusModes is null until the first sync.
            var state = new AiBlocking.AppState { FocusModeRunning = true, GlobalAiCheckUrlEnabled = true };

            Assert.IsTrue(Decide(state));
        }
    }
}
