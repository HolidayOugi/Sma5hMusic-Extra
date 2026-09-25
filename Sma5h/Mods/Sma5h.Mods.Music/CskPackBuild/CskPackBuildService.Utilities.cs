using Microsoft.Extensions.Logging;
using Sma5h.Mods.Music.Helpers;
using Sma5h.Mods.Music.Models;
using Sma5h.Mods.Music.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Sma5h.Mods.Music.CskPackBuild
{
    public partial class CskPackBuildService
    {
        #region Paths

        private const string GameTextTagOpenMarker = "{{";
        private const string GameTextTagCloseMarker = "}}";
        private const string GameTextTagOpen = "\u000e\u0000\u0002\u0002P";
        private const string GameTextTagClose = "\u000e\u0000\u0002\u0002d";

        private string PrepareOutputRoot()
        {
            var configuredPath = _config.CurrentValue.OutputPath;
            if (string.IsNullOrWhiteSpace(configuredPath))
                throw new InvalidOperationException("Output path is not configured.");
            var outputRoot = Path.GetFullPath(configuredPath);
            if (string.Equals(outputRoot, Path.GetPathRoot(outputRoot), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Refusing to clear the drive root: {outputRoot}");
            _logger.LogInformation("[CSK] Clearing output folder {OutputPath}", outputRoot);
            ClearDirectory(outputRoot);
            return outputRoot;
        }

        private static void ClearDirectory(string path)
        {
            Directory.CreateDirectory(path);
            foreach (var file in Directory.GetFiles(path))
                File.Delete(file);
            foreach (var directory in Directory.GetDirectories(path))
                Directory.Delete(directory, true);
        }

        private void MoveGeneratedBgmFiles(string nameId, string packFolderName, string outputRoot, string generatedBgmFolder)
        {
            if (string.IsNullOrEmpty(generatedBgmFolder))
                return;
            var destination = Path.Combine(outputRoot, packFolderName, "stream;", "sound", "bgm");
            Directory.CreateDirectory(destination);
            foreach (var extension in new[] { "nus3audio", "nus3bank" })
            {
                var source = Path.Combine(generatedBgmFolder, $"bgm_{nameId}.{extension}");
                MoveIfExists(source, Path.Combine(destination, Path.GetFileName(source)));
            }
        }

        private void CopyCoreVolumeBanks(string seriesName, string packFolderName, string outputRoot, string generatedBgmFolder, CskBuildState state)
        {
            if (string.IsNullOrEmpty(generatedBgmFolder) || !Directory.Exists(generatedBgmFolder))
                return;
            var entries = state.CoreVolumeChanges.Where(item => string.Equals(item.SeriesName, seriesName, StringComparison.OrdinalIgnoreCase)).ToList();
            if (entries.Count == 0)
                return;
            var destination = Path.Combine(outputRoot, packFolderName, "stream;", "sound", "bgm");
            foreach (var entry in entries)
            {
                var source = Path.Combine(generatedBgmFolder, $"bgm_{entry.NameId}.nus3bank");
                var audio = Path.Combine(generatedBgmFolder, $"bgm_{entry.NameId}.nus3audio");
                if (File.Exists(source) && !File.Exists(audio))
                    CopyIfExists(source, Path.Combine(destination, Path.GetFileName(source)));
            }
        }

        private bool CopySeriesIcon(SeriesEntry series, string packRoot)
        {
            var iconFile = GetSeriesIconPath(series);
            if (string.IsNullOrEmpty(iconFile))
                return false;
            var iconName = Path.GetFileNameWithoutExtension(iconFile);
            var prefix = $"{MusicConstants.InternalIds.SERIES_ICON_VARIANT_PRIMARY}_";
            if (!iconName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return false;
            var seriesIconName = iconName.Substring(prefix.Length);
            var replaceFolder = MusicConstants.DLC_SERIES.Contains(series.UiSeriesId, StringComparer.OrdinalIgnoreCase) ? "replace_patch" : "replace";
            CopySeriesIconVariant(iconFile, packRoot, replaceFolder, seriesIconName, MusicConstants.InternalIds.SERIES_ICON_VARIANT_PRIMARY);
            if (_config.CurrentValue.Sma5hMusicGUI?.BuildSeries1 == true)
                CopySeriesIconVariant(iconFile, packRoot, replaceFolder, seriesIconName, MusicConstants.InternalIds.SERIES_ICON_VARIANT_SECONDARY);
            return true;
        }

        private void CopySeriesIconVariant(string source, string packRoot, string replaceFolder, string seriesIconName, string variant)
        {
            var destinationFolder = Path.Combine(packRoot, "ui", replaceFolder, "series", variant);
            Directory.CreateDirectory(destinationFolder);
            var destination = Path.Combine(destinationFolder, $"{variant}_{seriesIconName}{Path.GetExtension(source)}");
            if (string.Equals(variant, MusicConstants.InternalIds.SERIES_ICON_VARIANT_PRIMARY, StringComparison.OrdinalIgnoreCase))
            {
                var icon = SeriesIconBntxCodec.LoadRgbaFromBntx(source, SeriesIconBntxCodec.MusicIconSize);
                if (icon.SourceWidth != SeriesIconBntxCodec.MusicIconSize || icon.SourceHeight != SeriesIconBntxCodec.MusicIconSize)
                {
                    SeriesIconBntxCodec.WriteBc7BntxFromRgba(icon.Rgba, Path.Combine(_config.CurrentValue.ResourcesPath, "music_icon_template.bntx"), destination);
                    _logger.LogInformation("[CSK] Resized series icon {IconFile} from {Width}x{Height} to 256x256", source, icon.SourceWidth, icon.SourceHeight);
                    _logger.LogInformation("[CSK] Copied series icon {IconFile} to {Destination}", source, destination);
                    return;
                }
            }
            File.Copy(source, destination, true);
        }

        private string GetSeriesIconPath(SeriesEntry series)
        {
            var iconFolder = GetMusicIconsFolder();
            if (!Directory.Exists(iconFolder))
                return null;
            foreach (var value in new[] { series.NameId, series.UiSeriesId })
            {
                var name = GetSeriesIconNamePart(value);
                if (string.IsNullOrEmpty(name))
                    continue;
                var path = Path.Combine(iconFolder, $"{MusicConstants.InternalIds.SERIES_ICON_VARIANT_PRIMARY}_{name}.bntx");
                if (File.Exists(path))
                    return path;
            }
            return null;
        }

        private string GetMusicIconsFolder()
        {
            var fullModPath = Path.GetFullPath(_config.CurrentValue.Sma5hMusic.ModPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            return Path.Combine(Path.GetDirectoryName(fullModPath) ?? Path.GetFullPath("Mods"), "MusicIcons");
        }

        private static string GetSeriesIconNamePart(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;
            var name = value.StartsWith(MusicConstants.InternalIds.SERIES_ID_PREFIX, StringComparison.OrdinalIgnoreCase)
                ? value.Substring(MusicConstants.InternalIds.SERIES_ID_PREFIX.Length)
                : value;
            return Regex.Replace(name, @"[^a-zA-Z0-9_]", string.Empty).ToLowerInvariant();
        }

        private void MoveIfExists(string source, string destination)
        {
            if (!File.Exists(source))
            {
                _logger.LogWarning("[CSK] File missing: {Source}", source);
                return;
            }
            if (File.Exists(destination))
                File.Delete(destination);
            File.Move(source, destination);
        }

        private void CopyIfExists(string source, string destination)
        {
            if (!File.Exists(source))
            {
                _logger.LogWarning("[CSK] File missing: {Source}", source);
                return;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            File.Copy(source, destination, true);
            _logger.LogInformation("[CSK] Copied series icon {IconFile} to {Destination}", source, destination);
        }

        #endregion

        #region Messages

        private static string MakeEntry(string label, string text)
        {
            //convert to base64 if contains game text tag or color markup
            if (ContainsGameTextTagMarker(text) || MsbtRichTextColorHelper.ContainsColorMarkup(text))
                return $"<entry label=\"{label}\" base64=\"true\">\r\n<text><![CDATA[{EncodeGameTextAsBase64(text)}]]></text>\r\n</entry>";
            return $"<entry label=\"{label}\">\r\n<text>{EscapeXml(text)}</text>\r\n</entry>";
        }

        private static bool HasMessageEntry(List<string> entries, string label)
        {
            var pattern = $"<entry label=\"{label}\"";
            return !string.IsNullOrEmpty(label) && entries.Any(entry => entry.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static void AddUniqueMessage(List<string> entries, string label, string text)
        {
            if (!string.IsNullOrEmpty(text) && !HasMessageEntry(entries, label))
                entries.Add(MakeEntry(label, text));
        }

        private static void AddOrReplaceMessage(List<string> entries, string label, string text)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(label))
                return;
            var pattern = $"<entry label=\"{label}\"";
            entries.RemoveAll(entry => entry.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0);
            entries.Add(MakeEntry(label, text));
        }

        private static void WriteXmsbt(string path, IEnumerable<string> entries)
        {
            var content = new StringBuilder("<?xml version=\"1.0\" encoding=\"utf-16\"?>\n<xmsbt>\n");
            foreach (var entry in entries)
                content.Append(entry).Append('\n');
            content.Append("</xmsbt>");
            File.WriteAllText(path, content.ToString(), Encoding.Unicode);
        }

        private static void WriteCombinedXmsbt(string path, IEnumerable<string> entries)
        {
            WriteXmsbt(path, entries.Where(entry => !string.IsNullOrEmpty(entry)).Distinct(StringComparer.Ordinal));
        }

        #region Accessors

        private string GetLocalizedString(Dictionary<string, string> localizedText, string fallback = "")
        {
            if (localizedText != null)
                foreach (var locale in GetCskTextLocales())
                    if (localizedText.TryGetValue(locale, out var text) && !string.IsNullOrWhiteSpace(text))
                        return text;
            return fallback;
        }

        private IEnumerable<string> GetCskTextLocales()
        {
            //priority GUI -> default -> us_en -> eu_en
            return new[]
            {
                _currentBuildLocale.Value, _config.CurrentValue.Sma5hMusicGUI?.DefaultMSBTLocale,
                _config.CurrentValue.Sma5hMusic?.DefaultLocale, _config.CurrentValue.Sma5hMusicGUI?.DefaultGUILocale,
                "us_en", "eu_en"
            }.Where(locale => !string.IsNullOrWhiteSpace(locale)).Distinct(StringComparer.OrdinalIgnoreCase);
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

        private static string EscapeXml(string text)
        {
            return string.IsNullOrEmpty(text) ? string.Empty : text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("'", "&apos;").Replace("\"", "&quot;");
        }

        private static bool ContainsGameTextTagMarker(string text)
        {
            return !string.IsNullOrEmpty(text) && text.Contains(GameTextTagOpenMarker) && text.Contains(GameTextTagCloseMarker);
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
                    bytes.AddRange(Encoding.Unicode.GetBytes(GameTextTagOpen)); index += GameTextTagOpenMarker.Length; continue;
                }
                //closing brackets for small font
                if (text.IndexOf(GameTextTagCloseMarker, index, StringComparison.Ordinal) == index)
                {
                    bytes.AddRange(Encoding.Unicode.GetBytes(GameTextTagClose)); index += GameTextTagCloseMarker.Length; continue;
                }
                //color closing tag
                if (text.IndexOf(MsbtRichTextColorHelper.ColorCloseTag, index, StringComparison.OrdinalIgnoreCase) == index)
                {
                    AddDefaultColorMarkerBytes(bytes); index += MsbtRichTextColorHelper.ColorCloseTag.Length; continue;
                }
                if (text.IndexOf(MsbtRichTextColorHelper.ColorCloseTagAlt, index, StringComparison.OrdinalIgnoreCase) == index)
                {
                    AddDefaultColorMarkerBytes(bytes); index += MsbtRichTextColorHelper.ColorCloseTagAlt.Length; continue;
                }
                //color opening tag
                if (text.IndexOf(MsbtRichTextColorHelper.ColorOpenPrefix, index, StringComparison.OrdinalIgnoreCase) == index)
                {
                    var end = text.IndexOf('>', index);
                    if (end > index)
                    {
                        var color = MsbtRichTextColorHelper.GetColor(text.Substring(index + MsbtRichTextColorHelper.ColorOpenPrefix.Length, end - index - MsbtRichTextColorHelper.ColorOpenPrefix.Length).Trim());
                        if (color != null)
                        {
                            if (color.IsDefault) AddDefaultColorMarkerBytes(bytes); else AddColorMarkerBytes(bytes, color);
                            index = end + 1; continue;
                        }
                    }
                }
                bytes.AddRange(Encoding.Unicode.GetBytes(text[index].ToString())); index++;
            }
            return bytes.ToArray();
        }

        private static void AddColorMarkerBytes(List<byte> bytes, MsbtTextColor color)
        {
            bytes.AddRange(new byte[] { 0x0E, 0x00, 0x00, 0x00, 0x03, 0x00, 0x04, 0x00, color.Red, color.Green, color.Blue, 0xFF });
        }

        private static void AddDefaultColorMarkerBytes(List<byte> bytes)
        {
            //black bytes for default text
            //a closing tag is not available
            bytes.AddRange(new byte[] { 0x0E, 0x00, 0x00, 0x00, 0x03, 0x00, 0x04, 0x00, 0x00, 0x00, 0x00, 0xFF });
        }

        #endregion
    }
}
