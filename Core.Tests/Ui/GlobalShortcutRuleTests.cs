using FocusBear.Core.Ui;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.Ui
{
    /// <summary>
    /// The shortcut recorder accepted any modifier, so Shift + B was saved and registered as a system-wide hotkey:
    /// typing a capital B anywhere opened the brain dump instead. A global shortcut now needs Ctrl, Alt or Win,
    /// except on a function key.
    /// </summary>
    [TestClass]
    public class GlobalShortcutRuleTests
    {
        private const uint Alt = 0x1, Ctrl = 0x2, Shift = 0x4, Win = 0x8;
        private const uint VkB = 0x42, VkF5 = 0x74, VkF24 = 0x87, Vk1 = 0x31;

        [TestMethod]
        public void ShiftAloneWouldEatTyping()
        {
            Assert.IsFalse(GlobalShortcutRule.IsAcceptable(Shift, VkB));
            Assert.IsFalse(GlobalShortcutRule.IsAcceptable(Shift, Vk1), "Shift + 1 is '!'");
            Assert.IsFalse(GlobalShortcutRule.IsAcceptable(0, VkB));
        }

        [TestMethod]
        public void CtrlAltOrWinMakeItAShortcut()
        {
            Assert.IsTrue(GlobalShortcutRule.IsAcceptable(Ctrl | Alt, VkB));
            Assert.IsTrue(GlobalShortcutRule.IsAcceptable(Ctrl | Shift, VkB));
            Assert.IsTrue(GlobalShortcutRule.IsAcceptable(Win, VkB));
            Assert.IsTrue(GlobalShortcutRule.IsAcceptable(Alt, Vk1));
        }

        [TestMethod]
        public void AFunctionKeyNeedsNoRealModifier()
        {
            Assert.IsTrue(GlobalShortcutRule.IsAcceptable(Shift, VkF5));
            Assert.IsTrue(GlobalShortcutRule.IsAcceptable(Shift, VkF24));
        }

        [TestMethod]
        public void NoKeyIsNoShortcut()
        {
            Assert.IsFalse(GlobalShortcutRule.IsAcceptable(Ctrl, 0));
        }

        [TestMethod]
        public void RegistrationAddsNoRepeat()
        {
            Assert.AreEqual(Ctrl | Alt | 0x4000u, GlobalShortcutRule.RegisterModifiers(Ctrl | Alt));
        }
    }
}
