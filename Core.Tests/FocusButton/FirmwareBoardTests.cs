using System;
using FocusBear.Core.FocusButton;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.FocusButton
{
    /// <summary>
    /// The firmware INI serves one image, Firmware-M5Dial.zip, and the app offered it to any Focus Button whose
    /// version was older - a VIEWE C3 (C30) included - and flashed it without checking the image against the
    /// detected chip (the screen claimed it did). The update is now offered only when the INI has an image for
    /// the device's board, and a .bin whose header names another chip is never written.
    /// </summary>
    [TestClass]
    public class FirmwareBoardTests
    {
        private const string LiveIni =
            "[application]\r\nprogram_version=1.8.1\r\nprogram_url=https://downloads.focusbear.io/update/focusbutton/Firmware-M5Dial.zip\r\nprogram_size=545448\r\n";

        [TestMethod]
        public void TheLiveIni_IsOfferedToTheM5DialOnly()
        {
            var ini = FirmwareBoard.ParseIni(LiveIni);

            FirmwareRelease m5 = FirmwareBoard.ReleaseFor(ini, "M50");
            Assert.IsNotNull(m5);
            Assert.AreEqual(new Version(1, 8, 1), m5.Version);
            StringAssert.EndsWith(m5.Url, "Firmware-M5Dial.zip");
            Assert.AreEqual("M50", m5.HardwareCode);

            Assert.IsNull(FirmwareBoard.ReleaseFor(ini, "C30"), "no VIEWE C3 image: the C3 is not offered the M5Dial build");
        }

        [TestMethod]
        public void APerBoardSectionServesItsBoard()
        {
            var ini = FirmwareBoard.ParseIni(LiveIni +
                "[C30]\nprogram_version=1.8.2\nprogram_url=https://downloads.focusbear.io/update/focusbutton/Firmware-VIEWE-C3.zip\n");
            FirmwareRelease c3 = FirmwareBoard.ReleaseFor(ini, "C30");
            Assert.AreEqual(new Version(1, 8, 2), c3.Version);
            StringAssert.EndsWith(c3.Url, "Firmware-VIEWE-C3.zip");
            Assert.AreEqual(new Version(1, 8, 1), FirmwareBoard.ReleaseFor(ini, "M50").Version);
        }

        [TestMethod]
        public void AnExplicitBoardKeyWins_AndASectionForTheWrongBoardIsRefused()
        {
            var ini = FirmwareBoard.ParseIni("[application]\nprogram_version=2.0\nprogram_url=https://x/fw.zip\nprogram_board=C30\n");
            Assert.IsNotNull(FirmwareBoard.ReleaseFor(ini, "C30"));
            Assert.IsNull(FirmwareBoard.ReleaseFor(ini, "M50"));

            // A [C30] section that points at the M5Dial zip is a publishing mistake: refuse it.
            ini = FirmwareBoard.ParseIni("[C30]\nprogram_version=2.0\nprogram_url=https://x/Firmware-M5Dial.zip\n");
            Assert.IsNull(FirmwareBoard.ReleaseFor(ini, "C30"));
        }

        [TestMethod]
        public void AnImageOfUnknownBoard_IsOfferedToNoOne()
        {
            var ini = FirmwareBoard.ParseIni("[application]\nprogram_version=2.0\nprogram_url=https://x/firmware.zip\n");
            Assert.IsNull(FirmwareBoard.ReleaseFor(ini, "M50"));
            Assert.IsNull(FirmwareBoard.ReleaseFor(ini, "C30"));
        }

        [TestMethod]
        public void ADeviceThatDidNotSayItsBoard_GetsTheImageButTheFlashChecksTheChip()
        {
            // A blank device reports no FIRMWAREVER; it may still be offered an image, and ImageFitsChip is the gate.
            var ini = FirmwareBoard.ParseIni(LiveIni);
            Assert.IsNotNull(FirmwareBoard.ReleaseFor(ini, null));
            Assert.IsNull(FirmwareBoard.ReleaseFor(ini, "X99"), "a board we don't know is never guessed");
        }

        [TestMethod]
        public void BrokenInis_OfferNothing()
        {
            Assert.IsNull(FirmwareBoard.ReleaseFor(FirmwareBoard.ParseIni(""), "M50"));
            Assert.IsNull(FirmwareBoard.ReleaseFor(FirmwareBoard.ParseIni("[application]\nprogram_version=soon\nprogram_url=https://x/Firmware-M5Dial.zip\n"), "M50"));
            Assert.IsNull(FirmwareBoard.ReleaseFor(FirmwareBoard.ParseIni("[application]\nprogram_version=1.9\n"), "M50"));
            Assert.IsNull(FirmwareBoard.ReleaseFor(null, "M50"));
        }

        private static byte[] Image(ushort chipId, byte magic = 0xE9)
        {
            var b = new byte[32];
            b[0] = magic;
            b[12] = (byte)(chipId & 0xFF);
            b[13] = (byte)(chipId >> 8);
            return b;
        }

        [TestMethod]
        public void TheImageHeaderNamesItsChip()
        {
            Assert.AreEqual("ESP32s3", FirmwareBoard.ImageChip(Image(0x0009)));
            Assert.AreEqual("ESP32c3", FirmwareBoard.ImageChip(Image(0x0005)));
            Assert.IsNull(FirmwareBoard.ImageChip(Image(0x0009, magic: 0x00)), "not an ESP app image");
            Assert.IsNull(FirmwareBoard.ImageChip(new byte[4]));
            Assert.IsNull(FirmwareBoard.ImageChip(null));
        }

        [TestMethod]
        public void OnlyAnImageForTheDetectedChipIsFlashed()
        {
            Assert.IsTrue(FirmwareBoard.ImageFitsChip(Image(0x0009), "ESP32s3"));
            Assert.IsFalse(FirmwareBoard.ImageFitsChip(Image(0x0009), "ESP32c3"), "M5Dial firmware on the VIEWE C3");
            Assert.IsFalse(FirmwareBoard.ImageFitsChip(Image(0x0005), "ESP32s3"));
            Assert.IsFalse(FirmwareBoard.ImageFitsChip(Image(0x0009, magic: 0), "ESP32s3"), "an unreadable header is not flashed");
            Assert.IsFalse(FirmwareBoard.ImageFitsChip(Image(0x0009), "Unknown"));
        }

        [TestMethod]
        public void HardwareCodesAndBoards()
        {
            Assert.AreEqual("M5Dial", FirmwareBoard.BoardName("M50"));
            Assert.AreEqual("VIEWE-C3", FirmwareBoard.BoardName("c30"));
            Assert.AreEqual("ESP32s3", FirmwareBoard.ChipFor("M50"));
            Assert.AreEqual("ESP32c3", FirmwareBoard.ChipFor("C30"));
            Assert.IsNull(FirmwareBoard.BoardName("X99"));
        }
    }
}
