using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Sma5h.Mods.Music.Helpers
{
    public static class MsbtRichTextColorHelper
    {
        public const string ColorCloseTag = "<\\color>";
        public const string ColorCloseTagAlt = "</color>";
        public const string ColorOpenPrefix = "<color=";
        public const int MaxDisplayNameLength = 30;

        public static readonly IReadOnlyList<MsbtTextColorSetting> BuiltInDefaultColors = new List<MsbtTextColorSetting>
        {
            new MsbtTextColorSetting("#FFFFFF", "White"),
            new MsbtTextColorSetting("#111111", "Black"),
            new MsbtTextColorSetting("#FF0000", "Red"),
            new MsbtTextColorSetting("#FFA500", "Orange"),
            new MsbtTextColorSetting("#FFFF00", "Yellow"),
            new MsbtTextColorSetting("#008000", "Green"),
            new MsbtTextColorSetting("#00FF00", "Lime"),
            new MsbtTextColorSetting("#00FFFF", "Cyan"),
            new MsbtTextColorSetting("#0000FF", "Blue"),
            new MsbtTextColorSetting("#800080", "Purple"),
            new MsbtTextColorSetting("#FF69B4", "Pink"),
            new MsbtTextColorSetting("#808080", "Gray")
        };

        //kept legacy colors for backwards compatibility with Extra 2.0-2.0.1
        private static readonly IReadOnlyList<MsbtTextColor> LegacyColors = new List<MsbtTextColor>
        {
            new MsbtTextColor("white", "White", 0xFF, 0xFF, 0xFF),
            new MsbtTextColor("black", "Black", 0x11, 0x11, 0x11),
            new MsbtTextColor("red", "Red", 0xFF, 0x00, 0x00),
            new MsbtTextColor("orange", "Orange", 0xFF, 0xA5, 0x00),
            new MsbtTextColor("yellow", "Yellow", 0xFF, 0xFF, 0x00),
            new MsbtTextColor("green", "Green", 0x00, 0x80, 0x00),
            new MsbtTextColor("lime", "Lime", 0x00, 0xFF, 0x00),
            new MsbtTextColor("cyan", "Cyan", 0x00, 0xFF, 0xFF),
            new MsbtTextColor("blue", "Blue", 0x00, 0x00, 0xFF),
            new MsbtTextColor("purple", "Purple", 0x80, 0x00, 0x80),
            new MsbtTextColor("pink", "Pink", 0xFF, 0x69, 0xB4),
            new MsbtTextColor("gray", "Gray", 0x80, 0x80, 0x80)
        };

        //default + custom selector always present
        public static readonly MsbtTextColor DefaultColor = new MsbtTextColor("default", "Default", 0xFF, 0xFF, 0xFF, true);
        public static readonly MsbtTextColor CustomColorSelector = new MsbtTextColor("custom", "Custom...", 0x00, 0x00, 0x00, false, true);

        private static IReadOnlyList<MsbtTextColor> _colors = CreateColorList(BuiltInDefaultColors);

        public static IReadOnlyList<MsbtTextColor> Colors => _colors;
        public static event Action DefaultColorsChanged;

        public static void ConfigureDefaultColors(IEnumerable<MsbtTextColorSetting> settings)
        {
            _colors = CreateColorList(settings ?? BuiltInDefaultColors);
            DefaultColorsChanged?.Invoke();
        }

        //clean color list if duplicates or invalid colors are present
        public static List<MsbtTextColorSetting> NormalizeDefaultColorSettings(
            IEnumerable<MsbtTextColorSetting> settings)
        {
            var normalized = new List<MsbtTextColorSetting>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var setting in settings ?? Enumerable.Empty<MsbtTextColorSetting>())
            {
                if (setting == null || !TryParseHexColor(setting.Hex, out var color) || !seen.Add(color.Hex))
                    continue;

                if (string.IsNullOrWhiteSpace(setting.DisplayName))
                    continue;

                var displayName = setting.DisplayName.Trim();
                if (displayName.Length > MaxDisplayNameLength)
                    displayName = displayName.Substring(0, MaxDisplayNameLength);
                normalized.Add(new MsbtTextColorSetting(color.Hex, displayName));
            }

            return normalized;
        }

        public static bool ContainsColorMarkup(string text)
        {
            return !string.IsNullOrEmpty(text) &&
                text.IndexOf(ColorOpenPrefix, StringComparison.OrdinalIgnoreCase) >= 0 &&
                (text.IndexOf(ColorCloseTag, StringComparison.OrdinalIgnoreCase) >= 0 ||
                 text.IndexOf(ColorCloseTagAlt, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        public static List<MsbtRichTextSpan> Parse(string value)
        {
            var spans = new List<MsbtRichTextSpan>();
            if (string.IsNullOrEmpty(value))
                return spans;

            var currentColor = DefaultColor.Id;
            var textStart = 0;
            var index = 0;
            while (index < value.Length)
            {
                //save text with current color
                if (StartsWith(value, index, ColorCloseTag) || StartsWith(value, index, ColorCloseTagAlt))
                {
                    AddSpan(spans, value.Substring(textStart, index - textStart), currentColor);
                    currentColor = DefaultColor.Id;
                    index += StartsWith(value, index, ColorCloseTag) ? ColorCloseTag.Length : ColorCloseTagAlt.Length;
                    textStart = index;
                    continue;
                }

                //change color for current text
                if (StartsWith(value, index, ColorOpenPrefix))
                {
                    var tagEnd = value.IndexOf('>', index);
                    if (tagEnd > index)
                    {
                        var colorId = value.Substring(index + ColorOpenPrefix.Length, tagEnd - index - ColorOpenPrefix.Length).Trim();
                        var color = GetColor(colorId);
                        if (color != null)
                        {
                            AddSpan(spans, value.Substring(textStart, index - textStart), currentColor);
                            currentColor = color.Id;
                            index = tagEnd + 1;
                            textStart = index;
                            continue;
                        }
                    }
                }

                index++;
            }

            AddSpan(spans, value.Substring(textStart), currentColor);
            return Normalize(spans);
        }

        public static string ToPlainText(IEnumerable<MsbtRichTextSpan> spans)
        {
            return string.Concat((spans ?? Enumerable.Empty<MsbtRichTextSpan>()).Select(p => p.Text));
        }

        //serialize for metadata
        public static string Serialize(IEnumerable<MsbtRichTextSpan> spans)
        {
            var normalized = Normalize(spans ?? Enumerable.Empty<MsbtRichTextSpan>());
            if (!normalized.Any(p => !IsDefaultColor(p.ColorId)))
                return ToPlainText(normalized);

            var builder = new StringBuilder();
            var activeColor = DefaultColor.Id;
            foreach (var span in normalized)
            {
                var colorId = NormalizeColorId(span.ColorId);
                if (!string.Equals(colorId, activeColor, StringComparison.OrdinalIgnoreCase))
                {
                    if (!IsDefaultColor(activeColor))
                        builder.Append(ColorCloseTag);

                    if (!IsDefaultColor(colorId))
                        builder.Append(ColorOpenPrefix).Append(colorId).Append(">");

                    activeColor = colorId;
                }

                builder.Append(span.Text);
            }

            if (!IsDefaultColor(activeColor))
                builder.Append(ColorCloseTag);

            return builder.ToString();
        }

        public static MsbtTextColor GetColor(string colorId)
        {
            if (string.IsNullOrWhiteSpace(colorId))
                return DefaultColor;

            if (colorId.StartsWith("#") && TryParseHexColor(colorId, out var hexColor))
                return hexColor;

            //handle legacy colors for backwards compatibility with Extra 2.0-2.0.1
            var legacyColor = LegacyColors.FirstOrDefault(p =>
                string.Equals(p.Id, colorId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(p.Label, colorId, StringComparison.OrdinalIgnoreCase));
            if (legacyColor != null)
                return TryParseHexColor(legacyColor.Hex, out var migratedColor) ? migratedColor : null;

            return Colors.FirstOrDefault(p =>
                string.Equals(p.Id, colorId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(p.Label, colorId, StringComparison.OrdinalIgnoreCase));
        }

        public static string NormalizeColorId(string colorId)
        {
            var color = GetColor(colorId);
            return color == null ? DefaultColor.Id : color.Id;
        }

        public static bool IsDefaultColor(string colorId)
        {
            return string.IsNullOrEmpty(colorId) ||
                (GetColor(colorId)?.IsDefault ?? false);
        }

        private static List<MsbtRichTextSpan> Normalize(IEnumerable<MsbtRichTextSpan> spans)
        {
            var output = new List<MsbtRichTextSpan>();
            foreach (var span in spans)
            {
                if (span == null || string.IsNullOrEmpty(span.Text))
                    continue;

                var colorId = NormalizeColorId(span.ColorId);
                var last = output.LastOrDefault();
                if (last != null && string.Equals(last.ColorId, colorId, StringComparison.OrdinalIgnoreCase))
                    last.Text += span.Text;
                else
                    output.Add(new MsbtRichTextSpan(span.Text, colorId));
            }

            return output;
        }

        private static void AddSpan(List<MsbtRichTextSpan> spans, string text, string colorId)
        {
            if (!string.IsNullOrEmpty(text))
                spans.Add(new MsbtRichTextSpan(text, NormalizeColorId(colorId)));
        }

        private static bool StartsWith(string value, int index, string pattern)
        {
            return value.IndexOf(pattern, index, StringComparison.OrdinalIgnoreCase) == index;
        }

        private static IReadOnlyList<MsbtTextColor> CreateColorList(IEnumerable<MsbtTextColorSetting> settings)
        {
            var colors = new List<MsbtTextColor> { DefaultColor };
            foreach (var setting in NormalizeDefaultColorSettings(settings))
            {
                if (TryParseHexColor(setting.Hex, out var color))
                {
                    colors.Add(new MsbtTextColor(
                        color.Id,
                        setting.DisplayName,
                        color.Red,
                        color.Green,
                        color.Blue));
                }
            }

            return colors;
        }

        private static bool TryParseHexColor(string value, out MsbtTextColor color)
        {
            color = null;
            //accept double hex for easy copy-paste
            var hex = value.TrimStart('#');
            if (hex.Length != 6 && hex.Length != 8)
                return false;

            var rgbHex = hex.Length == 8 ? hex.Substring(0, 6) : hex;
            if (!uint.TryParse(rgbHex, System.Globalization.NumberStyles.HexNumber, null, out var parsed))
                return false;

            var red = (byte)((parsed >> 16) & 0xFF);
            var green = (byte)((parsed >> 8) & 0xFF);
            var blue = (byte)(parsed & 0xFF);

            var normalizedHex = "#" + rgbHex.ToUpperInvariant();
            color = new MsbtTextColor(normalizedHex.ToLowerInvariant(), normalizedHex, red, green, blue);
            return true;
        }
    }

    public class MsbtTextColorSetting
    {
        public MsbtTextColorSetting()
        {
        }

        public MsbtTextColorSetting(string hex, string displayName)
        {
            Hex = hex;
            DisplayName = displayName;
        }

        public string Hex { get; set; }
        public string DisplayName { get; set; }
    }

    public class MsbtTextColor
    {
        public MsbtTextColor(string id, string label, byte red, byte green, byte blue, bool isDefault = false, bool isCustomSelector = false)
        {
            Id = id;
            Label = label;
            Red = red;
            Green = green;
            Blue = blue;
            IsDefault = isDefault;
            IsCustomSelector = isCustomSelector;
            Hex = isDefault || isCustomSelector ? "Transparent" : $"#{red:X2}{green:X2}{blue:X2}";
        }

        public string Id { get; }
        public string Label { get; }
        public byte Red { get; }
        public byte Green { get; }
        public byte Blue { get; }
        public bool IsDefault { get; }
        public bool IsCustomSelector { get; }
        public string Hex { get; }
    }

    public class MsbtRichTextSpan
    {
        public MsbtRichTextSpan(string text, string colorId)
        {
            Text = text ?? string.Empty;
            ColorId = MsbtRichTextColorHelper.NormalizeColorId(colorId);
        }

        public string Text { get; set; }
        public string ColorId { get; set; }
    }
}
