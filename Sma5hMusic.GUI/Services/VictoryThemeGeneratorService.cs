using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Sma5h.Mods.Music;
using Sma5h.Mods.Music.Helpers;
using Sma5h.Mods.Music.Interfaces;
using Sma5h.Mods.Music.Models;
using Sma5hMusic.GUI.Interfaces;
using Sma5hMusic.GUI.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Sma5hMusic.GUI.Services
{
    public class VictoryThemeGeneratorService : IVictoryThemeGeneratorService
    {
        private const string CloneBgmId = "ui_bgm_a29_ppm_medley";
        private static readonly Regex ToneIdRegex = new Regex(@"^[a-z0-9_]+$", RegexOptions.Compiled);

        private readonly IOptionsMonitor<ApplicationSettings> _config;
        private readonly INus3AudioService _nus3AudioService;
        private readonly IAudioImportService _audioImportService;
        private readonly IAudioMetadataService _audioMetadataService;
        private readonly ILogger _logger;

        public VictoryThemeGeneratorService(
            IOptionsMonitor<ApplicationSettings> config,
            INus3AudioService nus3AudioService,
            IAudioImportService audioImportService,
            IAudioMetadataService audioMetadataService,
            ILogger<VictoryThemeGeneratorService> logger)
        {
            _config = config;
            _nus3AudioService = nus3AudioService;
            _audioImportService = audioImportService;
            _audioMetadataService = audioMetadataService;
            _logger = logger;
        }

        public Task<string> Generate(IReadOnlyCollection<VictoryThemeGenerationEntry> entries, Action<int, int, string> normalizationProgress = null)
        {
            return Task.Run(() => GenerateInternal(entries, normalizationProgress));
        }

        private string GenerateInternal(IReadOnlyCollection<VictoryThemeGenerationEntry> entries, Action<int, int, string> normalizationProgress)
        {
            if (entries == null || entries.Count == 0)
                throw new InvalidOperationException("Add at least one victory theme entry.");

            //normalize entries
            var normalizedEntries = entries.Select(NormalizeEntry).ToList();
            var shouldWriteFighterJinglePatch = normalizedEntries.Any(p => p.PatchFighterJingle);

            //check duplicates
            var duplicateCharacters = normalizedEntries
                .GroupBy(p => p.CharaId, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(p => p.Count() > 1);
            if (duplicateCharacters != null)
                throw new InvalidOperationException($"Character '{duplicateCharacters.Key}' was added more than once.");

            //folders
            var outputRoot = Path.GetFullPath(Path.Combine(_config.CurrentValue.OutputPath, "Victory Themes"));
            var databaseOutputFolder = Path.Combine(outputRoot, "database");
            var bgmOutputFolder = Path.Combine(outputRoot, "stream;", "sound", "bgm");
            var tempRoot = Path.GetFullPath(Path.Combine(_config.CurrentValue.TempPath, "VictoryThemes"));

            EnsureDirectoriesDoNotOverlap(outputRoot, tempRoot, "output", "temporary");
            _logger.LogInformation("Generating victory themes. Output: {OutputRoot}; BGM output: {BgmOutputFolder}; Temp: {TempRoot}", outputRoot, bgmOutputFolder, tempRoot);

            ClearDirectory(outputRoot);
            ClearDirectory(tempRoot);
            Directory.CreateDirectory(bgmOutputFolder);
            if (shouldWriteFighterJinglePatch)
            {
                Directory.CreateDirectory(databaseOutputFolder);
            }
            Directory.CreateDirectory(tempRoot);

            try
            {
                var songData = CreateSongData();
                var normalizedCount = 0;
                var normalizationTotal = normalizedEntries.Count(p => p.ApplyNormalization);

                _nus3AudioService.ResetGeneratedNus3BankIds();

                foreach (var entry in normalizedEntries)
                {
                    var nus3AudioOutputFile = Path.Combine(bgmOutputFolder, string.Format(MusicConstants.GameResources.NUS3AUDIO_FILE, entry.ToneId));
                    var nus3BankOutputFile = Path.Combine(bgmOutputFolder, string.Format(MusicConstants.GameResources.NUS3BANK_FILE, entry.ToneId));
                    if (entry.ApplyNormalization)
                        normalizationProgress?.Invoke(++normalizedCount, normalizationTotal, entry.ToneId);

                    //generate nus3audio
                    var sourceNus3Audio = PrepareNus3Audio(entry, tempRoot);

                    //copy nus3audio and generate nus3bank
                    File.Copy(sourceNus3Audio, nus3AudioOutputFile, true);
                    _nus3AudioService.GenerateNus3Bank(entry.ToneId, entry.Volume, nus3BankOutputFile);
                    EnsureFileExists(nus3AudioOutputFile, entry.ToneId, "NUS3AUDIO");
                    EnsureFileExists(nus3BankOutputFile, entry.ToneId, "NUS3BANK");

                    //if we need to write JSON for custom victory themes
                    if (entry.PatchFighterJingle)
                    {
                        //add victory theme entry to JSON
                        AddVictoryJsonEntries(songData, entry.ToneId, nus3AudioOutputFile);
                        //assign victory theme to character in fighter_jingle section
                        AddFighterJingleJsonEntry(songData, entry.CharaId, entry.ToneId);
                    }
                }

                if (shouldWriteFighterJinglePatch)
                {
                    //save JSON to output
                    var jsonOutputFile = Path.Combine(databaseOutputFolder, "victory.json");
                    File.WriteAllText(jsonOutputFile, songData.ToString(Formatting.Indented));
                }

                _logger.LogInformation("Generated victory themes output at {OutputRoot}", outputRoot);
                return outputRoot;
            }
            finally
            {
                DeleteDirectoryIfExists(tempRoot, outputRoot);
            }
        }

        private PreparedVictoryThemeEntry NormalizeEntry(VictoryThemeGenerationEntry entry)
        {
            if (entry == null)
                throw new InvalidOperationException("Victory theme entry is empty.");

            var charaName = SanitizeIdPart(entry.CharaName);
            if (string.IsNullOrWhiteSpace(charaName))
                throw new InvalidOperationException("Every entry must have a character name.");

            if (string.IsNullOrWhiteSpace(entry.SourceFile) || !File.Exists(entry.SourceFile))
                throw new InvalidOperationException($"Audio file for '{charaName}' does not exist.");

            var toneId = SanitizeToneId(entry.ToneId);
            if (string.IsNullOrWhiteSpace(toneId))
                toneId = SanitizeToneId(Path.GetFileNameWithoutExtension(entry.SourceFile));

            ValidateToneId(toneId);

            return new PreparedVictoryThemeEntry
            {
                CharaId = $"ui_chara_{charaName}",
                ToneId = toneId,
                SourceFile = entry.SourceFile,
                PatchFighterJingle = entry.PatchFighterJingle,
                ApplyNormalization = entry.ApplyNormalization,
                Volume = RoundVolume(entry.Volume)
            };
        }

        private string PrepareNus3Audio(PreparedVictoryThemeEntry entry, string tempRoot)
        {
            if (_audioImportService.IsNus3Audio(entry.SourceFile))
            {
                if (entry.ApplyNormalization)
                    return _audioImportService.NormalizeNus3Audio(entry.ToneId, entry.SourceFile, tempRoot).GetAwaiter().GetResult();

                return entry.SourceFile;
            }

            //convert if necessary
            if (_audioImportService.RequiresConversion(entry.SourceFile))
            {
                var info = _audioImportService.GetAudioInfo(entry.SourceFile).GetAwaiter().GetResult();
                return _audioImportService.ConvertToNus3Audio(
                    entry.ToneId,
                    entry.SourceFile,
                    tempRoot,
                    0,
                    info.TotalSamples,
                    entry.ApplyNormalization).GetAwaiter().GetResult();
            }

            var tempOutputFile = Path.Combine(tempRoot, $"{entry.ToneId}{(entry.ApplyNormalization ? "_source" : string.Empty)}.nus3audio");
            if (!_nus3AudioService.GenerateNus3Audio(entry.ToneId, entry.SourceFile, tempOutputFile) || !File.Exists(tempOutputFile))
                throw new InvalidOperationException($"Could not create NUS3AUDIO for '{entry.ToneId}'.");

            if (entry.ApplyNormalization)
            {
                var normalizedRoot = Path.Combine(tempRoot, "normalized");
                return _audioImportService.NormalizeNus3Audio(entry.ToneId, tempOutputFile, normalizedRoot).GetAwaiter().GetResult();
            }

            return tempOutputFile;
        }

        private void AddVictoryJsonEntries(JObject songData, string toneId, string nus3AudioOutputFile)
        {
            var uiBgmId = $"ui_bgm_{toneId}";
            var streamSetId = $"set_{toneId}";
            var infoId = $"info_{toneId}";
            var streamId = $"stream_{toneId}";

            AddUnique(GetArray(songData, "bgm_database_entries"), "ui_bgm_id", new JObject
            {
                ["ui_bgm_id"] = uiBgmId,
                ["clone_from_ui_bgm_id"] = CloneBgmId,
                ["stream_set_id"] = streamSetId,
                ["name_id"] = toneId,
                ["ui_gametitle_id"] = MusicConstants.InternalIds.GAME_TITLE_ID_DEFAULT,
                ["test_disp_order"] = -1,
                ["record_type"] = MusicConstants.InternalIds.RECORD_TYPE_DEFAULT
            });

            var streamSetEntry = new JObject
            {
                ["stream_set_id"] = streamSetId,
                ["info0"] = infoId
            };
            AddUnique(GetArray(songData, "stream_set_entries"), "stream_set_id", streamSetEntry);

            AddUnique(GetArray(songData, "assigned_info_entries"), "info_id", new JObject
            {
                ["info_id"] = infoId,
                ["stream_id"] = streamId,
                ["condition"] = "sound_condition_none",
                ["condition_process"] = "sound_condition_process_add",
                ["change_fadeout_frame"] = 60,
                ["menu_change_fadeout_frame"] = 60
            });

            AddUnique(GetArray(songData, "stream_property_entries"), "stream_id", new JObject
            {
                ["stream_id"] = streamId,
                ["data_name0"] = toneId
            });

            AddUnique(GetArray(songData, "bgm_property_entries"), "stream_name", CreateBgmPropertyEntry(toneId, nus3AudioOutputFile));
        }

        private JObject CreateBgmPropertyEntry(string toneId, string nus3AudioOutputFile)
        {
            var info = ReadNus3AudioInfo(nus3AudioOutputFile);
            return new JObject
            {
                ["stream_name"] = toneId,
                ["loop_start_ms"] = info.LoopStartMs,
                ["loop_start_sample"] = info.LoopStartSample,
                ["loop_end_ms"] = info.LoopEndMs,
                ["loop_end_sample"] = info.LoopEndSample,
                ["duration_ms"] = info.TotalTimeMs,
                ["duration_sample"] = info.TotalSamples
            };
        }

        private AudioCuePoints ReadNus3AudioInfo(string nus3AudioOutputFile)
        {
            var info = _audioMetadataService.GetCuePoints(nus3AudioOutputFile).GetAwaiter().GetResult();
            if (info == null || info.TotalSamples == 0)
                throw new InvalidOperationException($"Could not read cue points from '{Path.GetFileName(nus3AudioOutputFile)}'.");

            return info;
        }

        private static void AddFighterJingleJsonEntry(JObject songData, string charaId, string toneId)
        {
            if (songData["fighter_jingle"] is not JObject fighterJingle)
            {
                fighterJingle = new JObject();
                songData["fighter_jingle"] = fighterJingle;
            }

            fighterJingle[charaId] = toneId;
        }

        private static JObject CreateSongData()
        {
            return new JObject
            {
                ["bgm_database_entries"] = new JArray(),
                ["stream_set_entries"] = new JArray(),
                ["assigned_info_entries"] = new JArray(),
                ["stream_property_entries"] = new JArray(),
                ["bgm_property_entries"] = new JArray()
            };
        }

        private static JArray GetArray(JObject parent, string name)
        {
            return (JArray)parent[name];
        }

        private static void AddUnique(JArray array, string key, JObject entry)
        {
            var value = (string)entry[key];
            if (!string.IsNullOrWhiteSpace(value) &&
                array.OfType<JObject>().Any(p => string.Equals((string)p[key], value, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            array.Add(entry);
        }

        private static void ValidateToneId(string toneId)
        {
            if (toneId.Length > MusicConstants.GameResources.ToneIdMaximumSize)
                throw new InvalidOperationException($"Tone ID '{toneId}' is too long. Maximum is {MusicConstants.GameResources.ToneIdMaximumSize} characters.");

            if (!ToneIdRegex.IsMatch(toneId))
                throw new InvalidOperationException($"Tone ID '{toneId}' can only contain lowercase letters, digits and underscore.");
        }

        private static string SanitizeToneId(string value)
        {
            return SanitizeIdPart(value);
        }

        private static string SanitizeIdPart(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            return Regex.Replace(value.Replace(" ", "_"), @"[^a-zA-Z0-9_]", string.Empty)
                .ToLower(CultureInfo.InvariantCulture);
        }

        private static float RoundVolume(float volume)
        {
            return (float)Math.Round(Math.Clamp(volume, -20f, 20f), 1, MidpointRounding.AwayFromZero);
        }

        private static void ClearDirectory(string path)
        {
            if (Directory.Exists(path))
                Directory.Delete(path, true);

            Directory.CreateDirectory(path);
        }

        private static void DeleteDirectoryIfExists(string path, string protectedPath)
        {
            if (!Directory.Exists(path))
                return;

            if (PathsOverlap(path, protectedPath))
                return;

            Directory.Delete(path, true);
        }

        private static void EnsureFileExists(string path, string toneId, string fileType)
        {
            if (!File.Exists(path))
                throw new InvalidOperationException($"{fileType} for '{toneId}' was not created:\r\n{path}");
        }

        private static void EnsureDirectoriesDoNotOverlap(string firstPath, string secondPath, string firstName, string secondName)
        {
            if (PathsOverlap(firstPath, secondPath))
                throw new InvalidOperationException($"The {secondName} folder cannot be inside the {firstName} folder, or the other way around:\r\n{firstPath}\r\n{secondPath}");
        }

        private static bool PathsOverlap(string firstPath, string secondPath)
        {
            var first = NormalizeDirectoryPath(firstPath);
            var second = NormalizeDirectoryPath(secondPath);
            return first.Equals(second, StringComparison.OrdinalIgnoreCase)
                   || first.StartsWith(second, StringComparison.OrdinalIgnoreCase)
                   || second.StartsWith(first, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeDirectoryPath(string path)
        {
            var fullPath = Path.GetFullPath(path)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return fullPath + Path.DirectorySeparatorChar;
        }

        private class PreparedVictoryThemeEntry
        {
            public string CharaId { get; set; }
            public string ToneId { get; set; }
            public string SourceFile { get; set; }
            public bool PatchFighterJingle { get; set; }
            public bool ApplyNormalization { get; set; }
            public float Volume { get; set; }
        }
    }
}
