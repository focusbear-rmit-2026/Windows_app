using FocusBear.Core.Ui;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.Blocking
{
    [TestClass]
    public class IconFontsTests
    {
        [TestMethod]
        public void IconFontsAreRecognisedWhereverTheyAreTheFirstChoice()
        {
            Assert.IsTrue(IconFonts.IsIconFont("Segoe MDL2 Assets"));
            Assert.IsTrue(IconFonts.IsIconFont("Segoe Fluent Icons, Segoe MDL2 Assets"));
            Assert.IsTrue(IconFonts.IsIconFont("segoe ui emoji"));
            Assert.IsTrue(IconFonts.IsIconFont("pack://application:,,,/Focus Bear;component/fonts/#Font Awesome 6 Free Solid"));
        }

        [TestMethod]
        public void TextFontsAreNot()
        {
            Assert.IsFalse(IconFonts.IsIconFont("Segoe UI"));
            Assert.IsFalse(IconFonts.IsIconFont("Segoe UI, Segoe MDL2 Assets"), "text falls back to the icon font only for missing glyphs");
            Assert.IsFalse(IconFonts.IsIconFont("./fonts/#OpenDyslexic3 Regular"));
            Assert.IsFalse(IconFonts.IsIconFont(null));
            Assert.IsFalse(IconFonts.IsIconFont(""));
        }
    }
}
