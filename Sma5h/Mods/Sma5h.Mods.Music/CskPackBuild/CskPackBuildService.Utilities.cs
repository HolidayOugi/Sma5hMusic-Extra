using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Sma5h.Mods.Music.Helpers;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Sma5h.Mods.Music.CskPackBuild
{
    public partial class CskPackBuildService
    {
        #region JSON

        private JObject LoadJsonObject(string path)
        {
            if (!File.Exists(path))
                return null;

            _logger.LogInformation("Loading {Path}", path);
            return JObject.Parse(File.ReadAllText(path));
        }

        private static JObject EnsureObject(JObject parent, string key)
        {
            var value = parent[key] as JObject;
            if (value != null)
                return value;

            value = new JObject();
            parent[key] = value;
            return value;
        }

        private static JObject MergeObjects(JObject baseObject, JObject overrideObject)
        {
            var output = baseObject != null ? (JObject)baseObject.DeepClone() : new JObject();
            OverlayProperties(output, overrideObject);
            return output;
        }

        private static void OverlayProperties(JObject target, JObject source)
        {
            if (target == null || source == null)
                return;

            foreach (var property in source.Properties())
                target[property.Name] = property.Value.DeepClone();
        }

        #endregion

        #region Messages

        private const string GameTextTagOpenMarker = "{{";
        private const string GameTextTagCloseMarker = "}}";
        private const string GameTextTagOpen = "\u000e\u0000\u0002\u0002P";
        private const string GameTextTagClose = "\u000e\u0000\u0002\u0002d";

        private static string MakeEntry(string label, string text)
        {
            if (ContainsGameTextTagMarker(text) || MsbtRichTextColorHelper.ContainsColorMarkup(text))
                return $"<entry label=\"{label}\" base64=\"true\">\r\n<text><![CDATA[{EncodeGameTextAsBase64(text)}]]></text>\r\n</entry>";

            return $"<entry label=\"{label}\">\r\n<text>{EscapeXml(text)}</text>\r\n</entry>";
        }

        private static bool HasMessageEntry(List<string> entries, string label)
        {
            if (string.IsNullOrEmpty(label))
                return false;

            var pattern = $"<entry label=\"{label}\"";
            return entries.Any(p => p.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static void AddUniqueMessage(List<string> entries, string label, string text)
        {
            if (string.IsNullOrEmpty(text) || HasMessageEntry(entries, label))
                return;

            entries.Add(MakeEntry(label, text));
        }

        private static void WriteXmsbt(string path, IEnumerable<string> entries)
        {
            var content = new StringBuilder();
            content.Append("<?xml version=\"1.0\" encoding=\"utf-16\"?>\n<xmsbt>\n");
            foreach (var entry in entries)
                content.Append(entry).Append("\n");
            content.Append("</xmsbt>");
            File.WriteAllText(path, content.ToString(), Encoding.Unicode);
        }

        private static void WriteCombinedXmsbt(string path, IEnumerable<string> entries)
        {
            WriteXmsbt(path, entries.Where(p => !string.IsNullOrEmpty(p)).Distinct(StringComparer.Ordinal));
        }

        private static string EscapeXml(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            return text
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("'", "&apos;")
                .Replace("\"", "&quot;");
        }

        private static bool ContainsGameTextTagMarker(string text)
        {
            return !string.IsNullOrEmpty(text) &&
                text.Contains(GameTextTagOpenMarker) &&
                text.Contains(GameTextTagCloseMarker);
        }

        private static string EncodeGameTextAsBase64(string text)
        {
            return Convert.ToBase64String(EncodeGameTextBytes(text));
        }

        private static byte[] EncodeGameTextBytes(string text)
        {
            var bytes = new List<byte>();
            for (var index = 0; index < text.Length;)
            {
                //opening brackets for small font
                if (text.IndexOf(GameTextTagOpenMarker, index, StringComparison.Ordinal) == index)
                {
                    bytes.AddRange(Encoding.Unicode.GetBytes(GameTextTagOpen));
                    index += GameTextTagOpenMarker.Length;
                    continue;
                }

                //closing brackets for small font
                if (text.IndexOf(GameTextTagCloseMarker, index, StringComparison.Ordinal) == index)
                {
                    bytes.AddRange(Encoding.Unicode.GetBytes(GameTextTagClose));
                    index += GameTextTagCloseMarker.Length;
                    continue;
                }

                //color closing tag
                if (text.IndexOf(MsbtRichTextColorHelper.ColorCloseTag, index, StringComparison.OrdinalIgnoreCase) == index)
                {
                    AddDefaultColorMarkerBytes(bytes);
                    index += MsbtRichTextColorHelper.ColorCloseTag.Length;
                    continue;
                }

                if (text.IndexOf(MsbtRichTextColorHelper.ColorCloseTagAlt, index, StringComparison.OrdinalIgnoreCase) == index)
                {
                    AddDefaultColorMarkerBytes(bytes);
                    index += MsbtRichTextColorHelper.ColorCloseTagAlt.Length;
                    continue;
                }

                //color opening tag
                if (text.IndexOf(MsbtRichTextColorHelper.ColorOpenPrefix, index, StringComparison.OrdinalIgnoreCase) == index)
                {
                    var tagEnd = text.IndexOf('>', index);
                    if (tagEnd > index)
                    {
                        var colorId = text.Substring(index + MsbtRichTextColorHelper.ColorOpenPrefix.Length, tagEnd - index - MsbtRichTextColorHelper.ColorOpenPrefix.Length).Trim();
                        var color = MsbtRichTextColorHelper.GetColor(colorId);
                        if (color != null)
                        {
                            if (color.IsDefault)
                                AddDefaultColorMarkerBytes(bytes);
                            else
                                AddColorMarkerBytes(bytes, color);
                            index = tagEnd + 1;
                            continue;
                        }
                    }
                }

                bytes.AddRange(Encoding.Unicode.GetBytes(text[index].ToString()));
                index++;
            }

            return bytes.ToArray();
        }

        private static void AddColorMarkerBytes(List<byte> bytes, MsbtTextColor color)
        {
            bytes.AddRange(new byte[] { 0x0E, 0x00, 0x00, 0x00, 0x03, 0x00, 0x04, 0x00 });
            bytes.Add(color.Red);
            bytes.Add(color.Green);
            bytes.Add(color.Blue);
            bytes.Add(0xFF);
        }

        private static void AddDefaultColorMarkerBytes(List<byte> bytes)
        {
            //transparent bytes
            //a closing tag is not available
            bytes.AddRange(new byte[] { 0x0E, 0x00, 0x00, 0x00, 0x03, 0x00, 0x04, 0x00, 0x00, 0x00, 0x00, 0xFF });
        }

        #endregion

        #region Accessors

        private static string GetString(JToken token, string key, string fallback = "")
        {
            var value = GetChildValue(token, key);
            if (value == null || value.Type == JTokenType.Null)
                return fallback;

            return value.ToString();
        }

        private string GetLocalizedString(JToken localizedText, string fallback = "")
        {
            if (localizedText == null)
                return fallback;

            foreach (var locale in GetCskTextLocales())
            {
                var text = GetString(localizedText, locale);
                if (!string.IsNullOrWhiteSpace(text))
                    return text;
            }

            return fallback;
        }

        private IEnumerable<string> GetCskTextLocales()
        {
            var configuredLocales = new[]
            {
                _currentBuildLocale.Value,
                _config.CurrentValue.Sma5hMusicGUI?.DefaultMSBTLocale,
                _config.CurrentValue.Sma5hMusic?.DefaultLocale,
                _config.CurrentValue.Sma5hMusicGUI?.DefaultGUILocale,
                "us_en",
                "eu_en"
            };

            return configuredLocales
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Distinct(StringComparer.OrdinalIgnoreCase);
        }

        private static int GetInt(JToken token, string key, int fallback)
        {
            var value = GetChildValue(token, key);
            if (value == null || value.Type == JTokenType.Null)
                return fallback;

            int output;
            return int.TryParse(value.ToString(), out output) ? output : fallback;
        }

        private static float GetFloat(JToken token, string key, float fallback)
        {
            var value = GetChildValue(token, key);
            if (value == null || value.Type == JTokenType.Null)
                return fallback;

            if (value.Type == JTokenType.Float || value.Type == JTokenType.Integer)
                return value.Value<float>();

            float output;
            if (float.TryParse(value.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out output))
                return output;

            return float.TryParse(value.ToString(), NumberStyles.Float, CultureInfo.CurrentCulture, out output) ? output : fallback;
        }

        private static bool GetBool(JToken token, string key, bool fallback)
        {
            var value = GetChildValue(token, key);
            if (value == null || value.Type == JTokenType.Null)
                return fallback;

            bool output;
            if (bool.TryParse(value.ToString(), out output))
                return output;

            int numericOutput;
            return int.TryParse(value.ToString(), out numericOutput) ? numericOutput != 0 : fallback;
        }

        private static JArray GetArray(JToken token, string key)
        {
            return GetChildValue(token, key) as JArray ?? new JArray();
        }

        private static JToken GetChildValue(JToken token, string key)
        {
            var obj = token as JObject;
            return obj == null ? null : obj[key];
        }

        #endregion

        #region Paths

        private string SanitizePathSegment(string value, string fallback, string context)
        {
            var sanitized = CskPathSanitizer.SanitizePathSegment(value, fallback);

            if (!string.Equals(value, sanitized, StringComparison.Ordinal))
                _logger.LogWarning("[CSK] Sanitized {Context}: '{Original}' -> '{Sanitized}'", context, value, sanitized);

            return sanitized;
        }

        #endregion

    }
}
