using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using FocusBear.Core.Updates;

namespace FocusBear.Core.FocusButton
{
    /// <summary>A firmware image the update INI offers for one board.</summary>
    public sealed class FirmwareRelease
    {
        public Version Version { get; set; }
        public string Url { get; set; }
        /// <summary>The board it is built for, as the device's hardware code (M50, C30).</summary>
        public string HardwareCode { get; set; }
    }

    /// <summary>
    /// Which Focus Button firmware fits which device. The device names its board in GET|FIRMWAREVER
    /// ("M50-1.8.1": M50 = M5Dial / ESP32-S3, C30 = VIEWE C3 / ESP32-C3, per the firmware's BOARD_HW_PREFIX), the
    /// release zips are Firmware-&lt;board&gt;.zip (Focus-Button build-release.yml), and an ESP app image carries its
    /// chip in its header (magic 0xE9, chip_id at bytes 12-13) - the check the Focus-Button repo's own
    /// FirmwareUpdater makes before writing.
    ///
    /// The app offered the one published image (Firmware-M5Dial.zip) to any device with an older version, the C3
    /// included, and flashed it without looking at the header. Now an image is offered only to its own board, and
    /// never written onto another chip.
    /// </summary>
    public static class FirmwareBoard
    {
        private sealed class Board
        {
            public string Code, Name, Chip;
        }

        private static readonly Board[] Boards =
        {
            new Board { Code = "M50", Name = "M5Dial", Chip = "ESP32s3" },
            new Board { Code = "C30", Name = "VIEWE-C3", Chip = "ESP32c3" },
        };

        // esp_image_header_t.chip_id -> EspDotNet ChipTypes name.
        private static readonly Dictionary<ushort, string> ImageChipIds = new Dictionary<ushort, string>
        {
            [0x0000] = "ESP32",
            [0x0002] = "ESP32s2",
            [0x0005] = "ESP32c3",
            [0x0009] = "ESP32s3",
            [0x000C] = "ESP32c2",
            [0x000D] = "ESP32c6",
            [0x0010] = "ESP32h2",
        };

        private static readonly Regex ZipName = new Regex(@"Firmware-([A-Za-z0-9_-]+)\.zip$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        public static string BoardName(string hardwareCode) => Find(hardwareCode)?.Name;

        public static string ChipFor(string hardwareCode) => Find(hardwareCode)?.Chip;

        /// <summary>
        /// The image for a device reporting <paramref name="hardwareCode"/>, or null when the INI has none for that
        /// board. A section named after the board ([C30] or [VIEWE-C3]) comes first, then [application]. An image's
        /// board is its program_board key, or the Firmware-&lt;board&gt;.zip in its URL; an image whose board can't be
        /// told is offered to no one. A device that hasn't said its board (null: blank, never flashed) may get the
        /// image - the flash then refuses it unless the header matches the chip (<see cref="ImageFitsChip"/>).
        /// </summary>
        public static FirmwareRelease ReleaseFor(IDictionary<string, IDictionary<string, string>> ini, string hardwareCode)
        {
            if (ini == null)
                return null;
            Board device = null;
            if (!string.IsNullOrWhiteSpace(hardwareCode))
            {
                device = Find(hardwareCode);
                if (device == null)
                    return null; // a board we don't know: never guess
            }

            var sections = new List<string>();
            if (device != null)
            {
                sections.Add(device.Code);
                sections.Add(device.Name);
            }
            sections.Add("application");

            foreach (string name in sections)
            {
                if (!ini.TryGetValue(name, out IDictionary<string, string> section))
                    continue;
                FirmwareRelease release = Read(section);
                if (release == null)
                    continue;
                if (device == null || string.Equals(release.HardwareCode, device.Code, StringComparison.OrdinalIgnoreCase))
                    return release;
            }
            return null;
        }

        /// <summary>The chip an ESP app image is built for, from its header; null when it isn't a readable image.</summary>
        public static string ImageChip(byte[] image)
        {
            if (image == null || image.Length < 14 || image[0] != 0xE9)
                return null;
            ushort id = (ushort)(image[12] | image[13] << 8);
            return ImageChipIds.TryGetValue(id, out string chip) ? chip : null;
        }

        /// <summary>Whether <paramref name="image"/> may be written to a device detected as <paramref name="detectedChip"/>.</summary>
        public static bool ImageFitsChip(byte[] image, string detectedChip)
        {
            string chip = ImageChip(image);
            return chip != null && string.Equals(chip, detectedChip, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>A small INI reader: sections, key=value, ';' or '#' comments. Keys and sections are case-insensitive.</summary>
        public static IDictionary<string, IDictionary<string, string>> ParseIni(string text)
        {
            var result = new Dictionary<string, IDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            IDictionary<string, string> current = null;
            foreach (string raw in (text ?? "").Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith(";") || line.StartsWith("#"))
                    continue;
                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    string name = line.Substring(1, line.Length - 2).Trim();
                    if (!result.TryGetValue(name, out current))
                    {
                        current = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        result[name] = current;
                    }
                    continue;
                }
                int eq = line.IndexOf('=');
                if (eq <= 0 || current == null)
                    continue;
                current[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
            }
            return result;
        }

        private static FirmwareRelease Read(IDictionary<string, string> section)
        {
            section.TryGetValue("program_version", out string versionText);
            section.TryGetValue("program_url", out string url);
            Version version = VersionCompare.Parse(versionText);
            if (version == null || string.IsNullOrWhiteSpace(url))
                return null;

            Board board = null;
            if (section.TryGetValue("program_board", out string declared) && !string.IsNullOrWhiteSpace(declared))
                board = Find(declared);
            else
            {
                Match m = ZipName.Match(url.Split('?')[0]);
                if (m.Success)
                    board = Find(m.Groups[1].Value);
            }
            if (board == null)
                return null;
            return new FirmwareRelease { Version = version, Url = url, HardwareCode = board.Code };
        }

        private static Board Find(string codeOrName)
        {
            if (string.IsNullOrWhiteSpace(codeOrName))
                return null;
            string key = codeOrName.Trim();
            foreach (Board b in Boards)
                if (string.Equals(b.Code, key, StringComparison.OrdinalIgnoreCase) || string.Equals(b.Name, key, StringComparison.OrdinalIgnoreCase))
                    return b;
            return null;
        }
    }
}
