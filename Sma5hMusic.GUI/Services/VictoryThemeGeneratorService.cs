using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Sma5h.Interfaces;
using Sma5h.Mods.Data.Sound.Config;
using Sma5h.Mods.Music;
using Sma5h.Mods.Music.Helpers;
using Sma5h.Mods.Music.Interfaces;
using Sma5h.Mods.Music.Models;
using Sma5h.ResourceProviders;
using Sma5h.ResourceProviders.Constants;
using Sma5h.ResourceProviders.Prc.Helpers;
using Sma5hMusic.GUI.Interfaces;
using Sma5hMusic.GUI.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using PrcBgmFighterJingleBgmEntry = Sma5h.Data.Ui.Param.Database.PrcUiBgmDatabaseModels.PrcBgmFighterJingleBgmEntry;
using PrcUiBgmDatabase = Sma5h.Data.Ui.Param.Database.PrcUiBgmDatabase;

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
        private readonly PrcResourceProvider _prcProvider;
        private readonly BgmPropertyProvider _bgmPropertyProvider;
        private readonly ILogger _logger;

        public VictoryThemeGeneratorService(
            IOptionsMonitor<ApplicationSettings> config,
            INus3AudioService nus3AudioService,
            IAudioImportService audioImportService,
            IAudioMetadataService audioMetadataService,
            IEnumerable<IResourceProvider> resourceProviders,
            ILogger<VictoryThemeGeneratorService> logger)
        {
            _config = config;
            _nus3AudioService = nus3AudioService;
            _audioImportService = audioImportService;
            _audioMetadataService = audioMetadataService;
            _logger = logger;
            _prcProvider = resourceProviders.OfType<PrcResourceProvider>().FirstOrDefault();
            _bgmPropertyProvider = resourceProviders.OfType<BgmPropertyProvider>().FirstOrDefault();
        }

        public Task<string> Generate(IReadOnlyCollection<VictoryThemeGenerationEntry> entries)
        {
            return Task.Run(() => GenerateInternal(entries));
        }

        private string GenerateInternal(IReadOnlyCollection<VictoryThemeGenerationEntry> entries)
        {
            if (entries == null || entries.Count == 0)
                throw new InvalidOperationException("Add at least one victory theme entry.");

            var normalizedEntries = entries.Select(NormalizeEntry).ToList();
            var shouldWriteFighterJinglePatch = normalizedEntries.Any(p => p.PatchFighterJingle);
            if (shouldWriteFighterJinglePatch && _prcProvider == null)
                throw new InvalidOperationException("PRC resource provider is not available.");

            var duplicateCharacters = normalizedEntries
                .GroupBy(p => p.CharaId, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(p => p.Count() > 1);
            if (duplicateCharacters != null)
                throw new InvalidOperationException($"Character '{duplicateCharacters.Key}' was added more than once.");

            var outputRoot = Path.GetFullPath(Path.Combine(_config.CurrentValue.OutputPath, "Victory Themes"));
            var databaseOutputFolder = Path.Combine(outputRoot, "database");
            var bgmOutputFolder = Path.Combine(outputRoot, "stream;", "sound", "bgm");
            var prcOutputFolder = Path.Combine(outputRoot, "ui", "param", "database");
            var tempRoot = Path.GetFullPath(Path.Combine(_config.CurrentValue.TempPath, "VictoryThemes"));

            EnsureDirectoriesDoNotOverlap(outputRoot, tempRoot, "output", "temporary");
            _logger.LogInformation("Generating victory themes. Output: {OutputRoot}; BGM output: {BgmOutputFolder}; Temp: {TempRoot}", outputRoot, bgmOutputFolder, tempRoot);

            ClearDirectory(outputRoot);
            ClearDirectory(tempRoot);
            Directory.CreateDirectory(bgmOutputFolder);
            if (shouldWriteFighterJinglePatch)
            {
                Directory.CreateDirectory(databaseOutputFolder);
                Directory.CreateDirectory(prcOutputFolder);
            }
            Directory.CreateDirectory(tempRoot);

            try
            {
                var coreBgmDb = _prcProvider != null ? ReadCoreBgmDatabase() : null;
                var coreBgmProperties = ReadCoreBgmProperties();
                var songData = CreateSongData();
                var bgmDbPatch = shouldWriteFighterJinglePatch
                    ? CreateFighterJinglePatchDatabase(coreBgmDb)
                    : new VictoryThemeBgmPatchDatabase();

                _nus3AudioService.ResetGeneratedNus3BankIds();

                foreach (var entry in normalizedEntries)
                {
                    var nus3AudioOutputFile = Path.Combine(bgmOutputFolder, string.Format(MusicConstants.GameResources.NUS3AUDIO_FILE, entry.ToneId));
                    var nus3BankOutputFile = Path.Combine(bgmOutputFolder, string.Format(MusicConstants.GameResources.NUS3BANK_FILE, entry.ToneId));
                    var sourceNus3Audio = PrepareNus3Audio(entry, tempRoot);

                    File.Copy(sourceNus3Audio, nus3AudioOutputFile, true);
                    _nus3AudioService.GenerateNus3Bank(entry.ToneId, entry.Volume, nus3BankOutputFile);
                    EnsureFileExists(nus3AudioOutputFile, entry.ToneId, "NUS3AUDIO");
                    EnsureFileExists(nus3BankOutputFile, entry.ToneId, "NUS3BANK");

                    if (entry.PatchFighterJingle)
                    {
                        var coreData = coreBgmDb != null ? FindCoreData(entry.ToneId, coreBgmDb, coreBgmProperties) : null;
                        AddVictoryJsonEntries(songData, entry.ToneId, coreData, nus3AudioOutputFile);
                        AddFighterJinglePatchEntry(bgmDbPatch, entry.CharaId, entry.ToneId);
                    }
                }

                if (shouldWriteFighterJinglePatch)
                {
                    var jsonOutputFile = Path.Combine(databaseOutputFolder, "victory.json");
                    File.WriteAllText(jsonOutputFile, songData.ToString(Formatting.Indented));

                    var sourceBgmDb = GetCoreResourcePath(PrcExtConstants.PRC_UI_BGM_DB_PATH);
                    var prcxOutputFile = Path.Combine(prcOutputFolder, "ui_bgm_db.prcx");
                    if (!_prcProvider.WriteFile(sourceBgmDb, prcxOutputFile, bgmDbPatch))
                        throw new InvalidOperationException("Could not generate ui_bgm_db.prcx.");
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

        private PrcUiBgmDatabase ReadCoreBgmDatabase()
        {
            var sourceBgmDb = GetCoreResourcePath(PrcExtConstants.PRC_UI_BGM_DB_PATH);
            var db = _prcProvider.ReadFile<PrcUiBgmDatabase>(sourceBgmDb, true);
            db.FighterJingleEntries ??= new Dictionary<string, PrcBgmFighterJingleBgmEntry>(StringComparer.OrdinalIgnoreCase);
            return db;
        }

        private Dictionary<string, Sma5h.Mods.Data.Sound.Config.BgmPropertyStructs.BgmPropertyEntry> ReadCoreBgmProperties()
        {
            if (_bgmPropertyProvider == null)
                return new Dictionary<string, Sma5h.Mods.Data.Sound.Config.BgmPropertyStructs.BgmPropertyEntry>(StringComparer.OrdinalIgnoreCase);

            var path = GetCoreResourcePath(BgmPropertyFileConstants.BGM_PROPERTY_PATH);
            if (!File.Exists(path))
                return new Dictionary<string, Sma5h.Mods.Data.Sound.Config.BgmPropertyStructs.BgmPropertyEntry>(StringComparer.OrdinalIgnoreCase);

            return _bgmPropertyProvider.ReadFile<BinBgmProperty>(path)?.Entries
                   ?? new Dictionary<string, Sma5h.Mods.Data.Sound.Config.BgmPropertyStructs.BgmPropertyEntry>(StringComparer.OrdinalIgnoreCase);
        }

        private CoreVictoryBgmData FindCoreData(
            string toneId,
            PrcUiBgmDatabase bgmDb,
            IReadOnlyDictionary<string, Sma5h.Mods.Data.Sound.Config.BgmPropertyStructs.BgmPropertyEntry> bgmProperties)
        {
            var streamProperty = bgmDb.StreamPropertyEntries?.Values
                .FirstOrDefault(p => string.Equals(p.DataName0, toneId, StringComparison.OrdinalIgnoreCase));
            if (streamProperty == null)
                return null;

            var assignedInfo = bgmDb.AssignedInfoEntries?.Values
                .FirstOrDefault(p => string.Equals(p.StreamId, streamProperty.StreamId, StringComparison.OrdinalIgnoreCase));
            if (assignedInfo == null)
                return null;

            var streamSet = bgmDb.StreamSetEntries?.Values
                .FirstOrDefault(p => new[]
                {
                    p.Info0, p.Info1, p.Info2, p.Info3, p.Info4, p.Info5, p.Info6, p.Info7,
                    p.Info8, p.Info9, p.Info10, p.Info11, p.Info12, p.Info13, p.Info14, p.Info15
                }.Any(info => string.Equals(info, assignedInfo.InfoId, StringComparison.OrdinalIgnoreCase)));
            if (streamSet == null)
                return null;

            var dbRoot = bgmDb.DbRootEntries?.Values
                .FirstOrDefault(p => string.Equals(p.StreamSetId, streamSet.StreamSetId, StringComparison.OrdinalIgnoreCase));

            bgmProperties.TryGetValue(toneId, out var bgmProperty);
            return new CoreVictoryBgmData
            {
                DbRoot = dbRoot,
                StreamSet = streamSet,
                AssignedInfo = assignedInfo,
                StreamProperty = streamProperty,
                BgmProperty = bgmProperty
            };
        }

        private void AddVictoryJsonEntries(JObject songData, string toneId, CoreVictoryBgmData coreData, string nus3AudioOutputFile)
        {
            var dbRoot = coreData?.DbRoot;
            var uiBgmId = dbRoot?.UiBgmId ?? $"ui_bgm_{toneId}";
            var streamSetId = coreData?.StreamSet?.StreamSetId ?? $"set_{toneId}";
            var infoId = coreData?.AssignedInfo?.InfoId ?? $"info_{toneId}";
            var streamId = coreData?.StreamProperty?.StreamId ?? $"stream_{toneId}";

            AddUnique(GetArray(songData, "bgm_database_entries"), "ui_bgm_id", new JObject
            {
                ["ui_bgm_id"] = uiBgmId,
                ["clone_from_ui_bgm_id"] = CloneBgmId,
                ["stream_set_id"] = streamSetId,
                ["name_id"] = dbRoot?.NameId ?? toneId,
                ["ui_gametitle_id"] = dbRoot?.UiGameTitleId ?? MusicConstants.InternalIds.GAME_TITLE_ID_DEFAULT,
                ["test_disp_order"] = -1,
                ["record_type"] = MusicConstants.InternalIds.RECORD_TYPE_DEFAULT
            });

            var streamSetEntry = new JObject
            {
                ["stream_set_id"] = streamSetId,
                ["info0"] = infoId
            };
            var specialCategory = coreData?.StreamSet?.SpecialCategory;
            if (!string.IsNullOrWhiteSpace(specialCategory))
                streamSetEntry["special_category"] = specialCategory;
            AddUnique(GetArray(songData, "stream_set_entries"), "stream_set_id", streamSetEntry);

            AddUnique(GetArray(songData, "assigned_info_entries"), "info_id", new JObject
            {
                ["info_id"] = infoId,
                ["stream_id"] = streamId,
                ["condition"] = coreData?.AssignedInfo?.Condition ?? "sound_condition_none",
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

        private static void AddFighterJinglePatchEntry(VictoryThemeBgmPatchDatabase bgmDbPatch, string charaId, string toneId)
        {
            bgmDbPatch.FighterJingleEntries[charaId] = new PrcBgmFighterJingleBgmEntry
            {
                UiCharaId = charaId,
                DataName = toneId
            };
        }

        private static VictoryThemeBgmPatchDatabase CreateFighterJinglePatchDatabase(PrcUiBgmDatabase coreBgmDb)
        {
            return new VictoryThemeBgmPatchDatabase
            {
                FighterJingleEntries = coreBgmDb?.FighterJingleEntries?
                    .ToDictionary(
                        p => p.Key,
                        p => new PrcBgmFighterJingleBgmEntry
                        {
                            UiCharaId = p.Value.UiCharaId,
                            DataName = p.Value.DataName
                        },
                        StringComparer.OrdinalIgnoreCase)
                    ?? new Dictionary<string, PrcBgmFighterJingleBgmEntry>(StringComparer.OrdinalIgnoreCase)
            };
        }

        private string GetCoreResourcePath(string relativePath)
        {
            return Path.Combine(_config.CurrentValue.GameResourcesPath, relativePath);
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

        private static void DeleteIfExists(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
                // Best-effort cleanup for temporary extracted audio.
            }
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

        private class CoreVictoryBgmData
        {
            public Sma5h.Data.Ui.Param.Database.PrcUiBgmDatabaseModels.PrcBgmDbRootEntry DbRoot { get; set; }
            public Sma5h.Data.Ui.Param.Database.PrcUiBgmDatabaseModels.PrcBgmStreamSetEntry StreamSet { get; set; }
            public Sma5h.Data.Ui.Param.Database.PrcUiBgmDatabaseModels.PrcBgmAssignedInfoEntry AssignedInfo { get; set; }
            public Sma5h.Data.Ui.Param.Database.PrcUiBgmDatabaseModels.PrcBgmStreamPropertyEntry StreamProperty { get; set; }
            public Sma5h.Mods.Data.Sound.Config.BgmPropertyStructs.BgmPropertyEntry BgmProperty { get; set; }
        }

        private class VictoryThemeBgmPatchDatabase : IStateManagerDb
        {
            [PrcDictionary("ui_chara_id")]
            [PrcHexMapping("fighter_jingle")]
            public Dictionary<string, PrcBgmFighterJingleBgmEntry> FighterJingleEntries { get; set; }
                = new Dictionary<string, PrcBgmFighterJingleBgmEntry>(StringComparer.OrdinalIgnoreCase);
        }
    }
}
