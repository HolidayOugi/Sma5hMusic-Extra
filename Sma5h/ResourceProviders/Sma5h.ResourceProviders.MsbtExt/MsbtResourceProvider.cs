using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MsbtEditor;
using Sma5h.Attributes;
using Sma5h.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Sma5h.ResourceProviders
{
    [ResourceProviderMatch(".msbt")]
    public class MsbtResourceProvider : BaseResourceProvider
    {
        private readonly ILogger _logger;
        private const string GameTextTagOpenMarker = "{{";
        private const string GameTextTagCloseMarker = "}}";
        private const string GameTextTagOpen = "\u000e\u0000\u0002\u0002P";
        private const string GameTextTagClose = "\u000e\u0000\u0002\u0002d";
        private const string ColorCloseTag = "<\\color>";
        private const string ColorCloseTagAlt = "</color>";
        private const string ColorOpenPrefix = "<color=";

        private static readonly IReadOnlyDictionary<string, MsbtTextColor> ColorMap =
            new List<MsbtTextColor>
            {
                new MsbtTextColor("default", 0x00, 0x00, 0x00, true),
                new MsbtTextColor("white", 0xFF, 0xFF, 0xFF),
                new MsbtTextColor("black", 0x11, 0x11, 0x11),
                new MsbtTextColor("red", 0xFF, 0x00, 0x00),
                new MsbtTextColor("orange", 0xFF, 0xA5, 0x00),
                new MsbtTextColor("yellow", 0xFF, 0xFF, 0x00),
                new MsbtTextColor("green", 0x00, 0x80, 0x00),
                new MsbtTextColor("lime", 0x00, 0xFF, 0x00),
                new MsbtTextColor("cyan", 0x00, 0xFF, 0xFF),
                new MsbtTextColor("blue", 0x00, 0x00, 0xFF),
                new MsbtTextColor("purple", 0x80, 0x00, 0x80),
                new MsbtTextColor("pink", 0xFF, 0x69, 0xB4),
                new MsbtTextColor("gray", 0x80, 0x80, 0x80)
            }.ToDictionary(p => p.Id, StringComparer.OrdinalIgnoreCase);

        public MsbtResourceProvider(IOptionsMonitor<Sma5hOptions> config, ILogger<MsbtResourceProvider> logger)
            : base(config)
        {
            _logger = logger;
        }

        public override T ReadFile<T>(string inputFile)
        {
            if (!typeof(T).IsAssignableFrom(typeof(MsbtDatabase)))
                throw new Exception($"Tried to use MsbtResourceProvider with wrong mapping type '{nameof(MsbtDatabase)}'");

            try
            {
                _logger.LogDebug("Reading msbt file {InputFile}", inputFile);

                var output = new MsbtDatabase() { Entries = new Dictionary<string, string>() };
                var msbtFile = new MSBT(inputFile);

                foreach (var msbtEntry in msbtFile.LBL1.Labels)
                {
                    var value = msbtFile.TXT2.OriginalStrings.FirstOrDefault(p => p.Index == msbtEntry.Index);
                    output.Entries.Add(((Label)msbtEntry).Name, Encoding.Unicode.GetString(value.Value).TrimEnd('\0'));
                }

                return (T)(object)output;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error while reading prc file {InputFile}", inputFile);
                return default;
            }
        }

        public override bool WriteFile<T>(string inputFile, string outputFile, T inputObj)
        {
            if (!typeof(T).IsAssignableFrom(typeof(MsbtDatabase)))
                throw new Exception($"Tried to used MsbtResourceProvider with wrong mapping type '{nameof(MsbtDatabase)}'");

            try
            {
                var msbtDb = (MsbtDatabase)(object)inputObj;

                _logger.LogDebug("MSBT: {NrbEntries} entries, InputFile: {InputFile}, OutputFile: {OutputFile}", msbtDb.Entries.Count, inputFile, outputFile);
                File.Copy(inputFile, outputFile);

                var msbtFile = new MSBT(outputFile);

                //Clean everything
                var labels = msbtFile.LBL1.Labels.Select(p => (Label)p).ToList();
                foreach (var label in labels)
                    msbtFile.RemoveLabel(label);

                //Add everything
                foreach (var newMsbtEntry in msbtDb.Entries)
                {
                    var newEntry = msbtFile.AddLabel(newMsbtEntry.Key);
                    var valueStr = newMsbtEntry.Value;
                    if (string.IsNullOrEmpty(newMsbtEntry.Value))
                        valueStr = "MISSING";
                    newEntry.Value = EncodeMsbtText(valueStr);
                }
                msbtFile.Save();
            }
            catch (Exception e)
            {
                _logger.LogError(e, "MSBT Generation error");
            }

            return true;
        }

        private static byte[] EncodeMsbtText(string text)
        {
            //same as CSK
            var bytes = new List<byte>();
            for (var index = 0; index < text.Length;)
            {
                if (text.IndexOf(GameTextTagOpenMarker, index, StringComparison.Ordinal) == index)
                {
                    bytes.AddRange(Encoding.Unicode.GetBytes(GameTextTagOpen));
                    index += GameTextTagOpenMarker.Length;
                    continue;
                }

                if (text.IndexOf(GameTextTagCloseMarker, index, StringComparison.Ordinal) == index)
                {
                    bytes.AddRange(Encoding.Unicode.GetBytes(GameTextTagClose));
                    index += GameTextTagCloseMarker.Length;
                    continue;
                }

                if (text.IndexOf(ColorCloseTag, index, StringComparison.OrdinalIgnoreCase) == index)
                {
                    AddColorMarkerBytes(bytes, ColorMap["default"]);
                    index += ColorCloseTag.Length;
                    continue;
                }

                if (text.IndexOf(ColorCloseTagAlt, index, StringComparison.OrdinalIgnoreCase) == index)
                {
                    AddColorMarkerBytes(bytes, ColorMap["default"]);
                    index += ColorCloseTagAlt.Length;
                    continue;
                }

                if (text.IndexOf(ColorOpenPrefix, index, StringComparison.OrdinalIgnoreCase) == index)
                {
                    var tagEnd = text.IndexOf('>', index);
                    if (tagEnd > index)
                    {
                        var colorId = text.Substring(index + ColorOpenPrefix.Length, tagEnd - index - ColorOpenPrefix.Length).Trim();
                        if (TryGetColor(colorId, out var color))
                        {
                            AddColorMarkerBytes(bytes, color);
                            index = tagEnd + 1;
                            continue;
                        }
                    }
                }

                bytes.AddRange(Encoding.Unicode.GetBytes(text[index].ToString()));
                index++;
            }

            bytes.AddRange(new byte[] { 0x00, 0x00 });
            return bytes.ToArray();
        }

        private static bool TryGetColor(string colorId, out MsbtTextColor color)
        {
            color = null;
            if (string.IsNullOrWhiteSpace(colorId))
                return false;

            if (ColorMap.TryGetValue(colorId, out color))
                return true;

            var hex = colorId.TrimStart('#');
            if (colorId.StartsWith("#") && (hex.Length == 6 || hex.Length == 8) &&
                uint.TryParse(hex.Length == 8 ? hex.Substring(0, 6) : hex, System.Globalization.NumberStyles.HexNumber, null, out var parsed))
            {
                var red = (byte)((parsed >> 16) & 0xFF);
                var green = (byte)((parsed >> 8) & 0xFF);
                var blue = (byte)(parsed & 0xFF);

                color = new MsbtTextColor(colorId, red, green, blue);
                return true;
            }

            return false;
        }

        private static void AddColorMarkerBytes(List<byte> bytes, MsbtTextColor color)
        {
            bytes.AddRange(new byte[] { 0x0E, 0x00, 0x00, 0x00, 0x03, 0x00, 0x04, 0x00 });
            bytes.Add(color.Red);
            bytes.Add(color.Green);
            bytes.Add(color.Blue);
            bytes.Add(0xFF);
        }

        private class MsbtTextColor
        {
            public MsbtTextColor(string id, byte red, byte green, byte blue, bool isDefault = false)
            {
                Id = id;
                Red = red;
                Green = green;
                Blue = blue;
                IsDefault = isDefault;
            }

            public string Id { get; }
            public byte Red { get; }
            public byte Green { get; }
            public byte Blue { get; }
            public bool IsDefault { get; }
        }

        /*
         * public bool GenerateNewEntries(List<MsbtNewEntryModel> newMsbtEntries, string inputMsbtFile, string outputMsbtFile)
        {
            try
            {
                _logger.LogDebug("MSBT: {NrbEntries} entries, InputFile: {InputFile}, OutputFile: {OutputFile}", newMsbtEntries.Count, inputMsbtFile, outputMsbtFile);
                File.Copy(inputMsbtFile, outputMsbtFile);
                var msbtFile = new MSBT(outputMsbtFile);
                foreach (var newMsbtEntry in newMsbtEntries)
                {
                    _logger.LogDebug("MSBT: Adding {Label}:{Value}", newMsbtEntry.Label, newMsbtEntry.Value);
                    var newEntry = msbtFile.AddLabel(newMsbtEntry.Label);
                    newEntry.Value = Encoding.Unicode.GetBytes(newMsbtEntry.Value + "\0");
                }
                msbtFile.Save();
            }
            catch(Exception e)
            {
                _logger.LogError(e, "MSBT Generation error");
            }

            return true;
        }*/
    }
}
