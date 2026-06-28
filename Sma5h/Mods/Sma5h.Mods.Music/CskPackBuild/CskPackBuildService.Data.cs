using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Sma5h.Mods.Music;
using Sma5h.Mods.Music.Helpers;
using Sma5h.Mods.Music.Interfaces;
using Sma5h.Mods.Music.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Sma5h.Mods.Music.CskPackBuild
{
    public partial class CskPackBuildService
    {
        #region Types

        private class CskModContext
        {
            public IMusicMod Mod { get; set; }
            public string MetadataPath { get; set; }
            public JObject Metadata { get; set; }
            public string PackName { get; set; }
            public string SafePackName { get; set; }
            public List<JObject> SeriesList { get; set; }
            public Dictionary<string, string> SeriesIdToName { get; set; }
        }

        private class CskBuildResources
        {
            public Dictionary<string, string> CoreGameSeriesById { get; set; }
            public HashSet<string> CoreBgmIds { get; set; }
            public JObject PlaylistData { get; set; }
            public bool HasCoreOverrides { get; set; }
            public JObject OrderOverride { get; set; }
            //raw override files, read from musicoverrides
            public JObject RawCoreBgmOverride { get; set; }
            public JObject RawCoreGameOverride { get; set; }
            public JObject RawCoreSeriesOverride { get; set; }
            //raw + game state
            public JObject CoreBgmOverride { get; set; }
            public JObject CoreGameOverride { get; set; }
            public JObject CoreSeriesOverride { get; set; }
            public JObject StageOverride { get; set; }
        }

        private class CoreBgmVolumeOverrideEntry
        {
            public string NameId { get; set; }
            public string SeriesName { get; set; }
            public float Volume { get; set; }
        }

        private class CoreOverrideMusicMod : IMusicMod
        {
            private readonly MusicModInformation _mod;

            //virtual mod
            public CoreOverrideMusicMod(string modPath)
            {
                ModPath = modPath;
                _mod = new MusicModInformation
                {
                    Name = "Music Pack"
                };
            }

            public string Id => "core_overrides";
            public string Name => _mod.Name;
            public string ModPath { get; }
            public MusicModInformation Mod => _mod;

            public bool UpdateModInformation(MusicModInformation configBase) => false;
            public MusicModEntries GetMusicModEntries() => new MusicModEntries();
            public System.Threading.Tasks.Task<bool> AddOrUpdateMusicModEntries(MusicModEntries musicModEntries) => System.Threading.Tasks.Task.FromResult(false);
            public bool ReorderSongs(List<string> list) => false;
            public bool AdjustSongVolumes(float amount, float minimumVolume, float maximumVolume) => false;
            public bool SetSongVolumes(float volume, float minimumVolume, float maximumVolume) => false;
            public bool RemoveMusicModEntries(MusicModDeleteEntries musicModDeleteEntries) => false;
        }

        #endregion

        #region Mod Contexts

        private List<IMusicMod> GetMusicMods()
        {
            var mods = _musicModManagerService.MusicMods.ToList();
            if (mods.Count == 0)
                mods = _musicModManagerService.RefreshMusicMods().ToList();
            return mods;
        }

        private List<CskModContext> LoadModContexts(IEnumerable<IMusicMod> mods)
        {
            var contexts = new List<CskModContext>();

            foreach (var mod in mods)
            {
                var metadataPath = Path.Combine(mod.ModPath, MusicConstants.MusicModFiles.MUSIC_MOD_METADATA_JSON_FILE);
                if (!File.Exists(metadataPath))
                    continue;

                var metadata = JObject.Parse(File.ReadAllText(metadataPath));
                //fix for old metadata_mod.json without series/game info
                if (RepairMissingSeriesAndGameMetadata(mod, metadata))
                    metadata = JObject.Parse(File.ReadAllText(metadataPath));

                var packName = GetString(metadata, "name", mod.Name);
                var seriesList = GetArray(metadata, "series").OfType<JObject>().ToList();

                contexts.Add(new CskModContext
                {
                    Mod = mod,
                    MetadataPath = metadataPath,
                    Metadata = metadata,
                    PackName = packName,
                    SafePackName = SanitizePathSegment(packName, mod.Name, "pack folder name"),
                    SeriesList = seriesList,
                    SeriesIdToName = seriesList
                        .Where(p => !string.IsNullOrEmpty(GetString(p, "ui_series_id")) && !string.IsNullOrEmpty(GetString(p, "name_id")))
                        .GroupBy(p => GetString(p, "ui_series_id"), StringComparer.OrdinalIgnoreCase)
                        .ToDictionary(p => p.Key, p => GetString(p.First(), "name_id"), StringComparer.OrdinalIgnoreCase)
                });
            }

            return contexts;
        }

        //turn core overrides into a mod internally
        private List<CskModContext> LoadCoreOverrideContexts(CskBuildResources buildResources)
        {
            if (buildResources?.HasCoreOverrides != true)
                return new List<CskModContext>();

            var seriesList = CreateCoreOverrideSeries(buildResources).ToList();
            if (seriesList.Count == 0)
                return new List<CskModContext>();

            var modPath = Path.Combine(_config.CurrentValue.Sma5hMusicOverride.ModPath ?? "CoreOverrides", "_core_overrides");
            var mod = new CoreOverrideMusicMod(modPath);
            var metadata = new JObject
            {
                ["name"] = mod.Name,
                ["series"] = new JArray(seriesList.Select(p => p.DeepClone()))
            };

            return new List<CskModContext>
            {
                new CskModContext
                {
                    Mod = mod,
                    MetadataPath = "core overrides",
                    Metadata = metadata,
                    PackName = mod.Name,
                    SafePackName = SanitizePathSegment(mod.Name, "core overrides", "pack folder name"),
                    SeriesList = seriesList,
                    SeriesIdToName = seriesList
                        .Where(p => !string.IsNullOrEmpty(GetString(p, "ui_series_id")) && !string.IsNullOrEmpty(GetString(p, "name_id")))
                        .GroupBy(p => GetString(p, "ui_series_id"), StringComparer.OrdinalIgnoreCase)
                        .ToDictionary(p => p.Key, p => GetString(p.First(), "name_id"), StringComparer.OrdinalIgnoreCase)
                }
            };
        }

        private IEnumerable<JObject> CreateCoreOverrideSeries(CskBuildResources buildResources)
        {
            foreach (var uiSeriesId in GetCoreOverrideSeriesIds(buildResources).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                //prefer the real/effective series data, fallback only if the series was not found
                var series = buildResources.CoreSeriesOverride?[uiSeriesId] as JObject ??
                             CreateFallbackCoreOverrideSeries(uiSeriesId);
                if (series == null)
                    continue;

                var output = (JObject)series.DeepClone();
                output["games"] = CreateCoreOverrideGames(uiSeriesId, buildResources);
                yield return output;
            }
        }

        private IEnumerable<string> GetCoreOverrideSeriesIds(CskBuildResources buildResources)
        {
            foreach (var property in buildResources.RawCoreSeriesOverride?.Properties() ?? Enumerable.Empty<JProperty>())
                yield return property.Name;

            //game edits belong to the game's series
            foreach (var property in buildResources.RawCoreGameOverride?.Properties() ?? Enumerable.Empty<JProperty>())
            {
                var game = buildResources.CoreGameOverride?[property.Name] as JObject;
                var uiSeriesId = GetString(game, "ui_series_id");
                if (!string.IsNullOrEmpty(uiSeriesId))
                    yield return uiSeriesId;
            }

            var dbRoots = buildResources.RawCoreBgmOverride?["CoreBgmDbRootOverrides"] as JObject;
            foreach (var property in dbRoots?.Properties() ?? Enumerable.Empty<JProperty>())
            {
                var db = buildResources.CoreBgmOverride?["CoreBgmDbRootOverrides"]?[property.Name] as JObject;
                var uiSeriesId = GetCoreBgmUiSeriesId(db, buildResources.CoreGameOverride);
                if (!string.IsNullOrEmpty(uiSeriesId))
                    yield return uiSeriesId;
            }

            //get data for volume-only bgms (needed for series lookup)
            var volumeOverrides = buildResources.RawCoreBgmOverride?["CoreBgmVolumeOverrides"] as JObject;
            foreach (var property in volumeOverrides?.Properties() ?? Enumerable.Empty<JProperty>())
            {
                var uiSeriesId = GetCoreBgmUiSeriesIdByNameId(property.Name, buildResources.CoreGameOverride);
                if (!string.IsNullOrEmpty(uiSeriesId))
                    yield return uiSeriesId;
            }
        }

        private JArray CreateCoreOverrideGames(string uiSeriesId, CskBuildResources buildResources)
        {
            var gameIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var property in buildResources.RawCoreGameOverride?.Properties() ?? Enumerable.Empty<JProperty>())
            {
                var game = buildResources.CoreGameOverride?[property.Name] as JObject;
                if (string.Equals(GetString(game, "ui_series_id"), uiSeriesId, StringComparison.OrdinalIgnoreCase))
                    gameIds.Add(property.Name);
            }

            var dbRoots = buildResources.RawCoreBgmOverride?["CoreBgmDbRootOverrides"] as JObject;
            foreach (var property in dbRoots?.Properties() ?? Enumerable.Empty<JProperty>())
            {
                var db = buildResources.CoreBgmOverride?["CoreBgmDbRootOverrides"]?[property.Name] as JObject;
                if (!string.Equals(GetCoreBgmUiSeriesId(db, buildResources.CoreGameOverride), uiSeriesId, StringComparison.OrdinalIgnoreCase))
                    continue;

                var uiGameTitleId = GetString(db, "ui_gametitle_id");
                if (!string.IsNullOrEmpty(uiGameTitleId))
                    gameIds.Add(uiGameTitleId);
            }

            var volumeOverrides = buildResources.RawCoreBgmOverride?["CoreBgmVolumeOverrides"] as JObject;
            foreach (var property in volumeOverrides?.Properties() ?? Enumerable.Empty<JProperty>())
            {
                var db = GetOriginalCoreDbRootByNameId(property.Name);
                if (db == null)
                    continue;

                var bgmSeriesId = GetCoreBgmUiSeriesId(db.UiGameTitleId, null, buildResources.CoreGameOverride);
                if (!string.Equals(bgmSeriesId, uiSeriesId, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!string.IsNullOrEmpty(db.UiGameTitleId))
                    gameIds.Add(db.UiGameTitleId);
            }

            var games = new JArray();
            foreach (var uiGameTitleId in gameIds)
            {
                var game = buildResources.CoreGameOverride?[uiGameTitleId] as JObject;
                if (game == null)
                    continue;

                var output = (JObject)game.DeepClone();
                output["bgms"] = new JArray();
                games.Add(output);
            }

            return games;
        }

        private static JObject CreateFallbackCoreOverrideSeries(string uiSeriesId)
        {
            if (string.IsNullOrEmpty(uiSeriesId))
                return null;

            var nameId = uiSeriesId.StartsWith(MusicConstants.InternalIds.SERIES_ID_PREFIX, StringComparison.OrdinalIgnoreCase)
                ? uiSeriesId.Substring(MusicConstants.InternalIds.SERIES_ID_PREFIX.Length)
                : uiSeriesId;

            return new JObject
            {
                ["ui_series_id"] = uiSeriesId,
                ["name_id"] = nameId,
                ["disp_order"] = 0,
                ["disp_order_sound"] = 0,
                ["save_no"] = 0,
                ["msbt_title"] = new JObject(),
                ["games"] = new JArray()
            };
        }

        #endregion

        #region Metadata Repair

        //forces a save for old metadata_mod.json without needed info
        private bool RepairMissingSeriesAndGameMetadata(IMusicMod mod, JObject metadata)
        {
            var saved = false;
            //use the current audio state as source for complete metadata
            var seriesById = _audioStateService.GetSeriesEntries()
                .Where(p => !string.IsNullOrEmpty(p.UiSeriesId))
                .GroupBy(p => p.UiSeriesId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(p => p.Key, p => p.OrderByDescending(GetSeriesMetadataScore).First(), StringComparer.OrdinalIgnoreCase);
            var gameById = _audioStateService.GetGameTitleEntries()
                .Where(p => !string.IsNullOrEmpty(p.UiGameTitleId))
                .GroupBy(p => p.UiGameTitleId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(p => p.Key, p => p.OrderByDescending(GetGameMetadataScore).First(), StringComparer.OrdinalIgnoreCase);

            foreach (JObject series in GetArray(metadata, "series").OfType<JObject>())
            {
                var uiSeriesId = GetString(series, "ui_series_id");
                if (NeedsMetadataRepair(series) &&
                    !string.IsNullOrEmpty(uiSeriesId) &&
                    seriesById.TryGetValue(uiSeriesId, out var seriesEntry) &&
                    HasCompleteSeriesMetadata(seriesEntry))
                {
                    var entries = new MusicModEntries();
                    entries.SeriesEntries.Add(seriesEntry);
                    if (!mod.AddOrUpdateMusicModEntries(entries).GetAwaiter().GetResult())
                        throw new InvalidOperationException($"Could not update series metadata for {uiSeriesId} in mod {mod.Name}.");

                    _logger.LogInformation("[CSK] Updated missing series metadata for {UiSeriesId} in mod {ModName}.", uiSeriesId, mod.Name);
                    saved = true;
                }

                foreach (JObject game in GetArray(series, "games").OfType<JObject>())
                {
                    var uiGameTitleId = GetString(game, "ui_gametitle_id");
                    //skip if this metadata already has the required fields
                    if (!NeedsMetadataRepair(game) ||
                        string.IsNullOrEmpty(uiGameTitleId) ||
                        !gameById.TryGetValue(uiGameTitleId, out var gameEntry) ||
                        !HasCompleteGameMetadata(gameEntry))
                    {
                        continue;
                    }

                    var entries = new MusicModEntries();
                    entries.SeriesEntries.Add(
                        //prefer audio state series, fallback to metadata so the game can still be saved
                        seriesById.TryGetValue(gameEntry.UiSeriesId, out var parentSeriesEntry)
                            ? parentSeriesEntry
                            : CreateSeriesEntryFromMetadata(series));
                    entries.GameTitleEntries.Add(gameEntry);

                    if (!mod.AddOrUpdateMusicModEntries(entries).GetAwaiter().GetResult())
                        throw new InvalidOperationException($"Could not update game title metadata for {uiGameTitleId} in mod {mod.Name}.");

                    _logger.LogInformation("[CSK] Updated missing game title metadata for {UiGameTitleId} in mod {ModName}.", uiGameTitleId, mod.Name);
                    saved = true;
                }
            }

            return saved;
        }

        private static bool NeedsMetadataRepair(JObject entry)
        {
            return string.IsNullOrWhiteSpace(GetString(entry, "name_id")) ||
                   IsNullOrMissing(entry, "msbt_title");
        }

        private static bool IsNullOrMissing(JObject entry, string key)
        {
            if (entry == null || !entry.TryGetValue(key, out var value))
                return true;

            return value == null || value.Type == JTokenType.Null;
        }

        private static bool HasCompleteSeriesMetadata(SeriesEntry seriesEntry)
        {
            return seriesEntry != null &&
                   !string.IsNullOrWhiteSpace(seriesEntry.NameId) &&
                   seriesEntry.MSBTTitle != null;
        }

        private static bool HasCompleteGameMetadata(GameTitleEntry gameEntry)
        {
            return gameEntry != null &&
                   !string.IsNullOrWhiteSpace(gameEntry.NameId) &&
                   gameEntry.MSBTTitle != null;
        }

        private static int GetSeriesMetadataScore(SeriesEntry seriesEntry)
        {
            if (seriesEntry == null)
                return 0;

            return (!string.IsNullOrWhiteSpace(seriesEntry.NameId) ? 1 : 0) +
                   (seriesEntry.MSBTTitle != null ? 1 : 0);
        }

        private static int GetGameMetadataScore(GameTitleEntry gameEntry)
        {
            if (gameEntry == null)
                return 0;

            return (!string.IsNullOrWhiteSpace(gameEntry.NameId) ? 1 : 0) +
                   (gameEntry.MSBTTitle != null ? 1 : 0);
        }

        private static SeriesEntry CreateSeriesEntryFromMetadata(JObject series)
        {
            //fallback object used only when the audio state has no parent series entry
            return new SeriesEntry(GetString(series, "ui_series_id"), EntrySource.Mod)
            {
                NameId = GetString(series, "name_id"),
                DispOrder = ToSByte(GetInt(series, "disp_order", 0)),
                DispOrderSound = ToSByte(GetInt(series, "disp_order_sound", 0)),
                SaveNo = ToSByte(GetInt(series, "save_no", -1)),
                Unk1 = GetBool(series, "0x1c38302364", false),
                IsDlc = GetBool(series, "is_dlc", false),
                IsPatch = GetBool(series, "is_patch", false),
                DlcCharaId = GetString(series, "dlc_chara_id"),
                IsUseAmiiboBg = GetBool(series, "is_use_amiibo_bg", false),
                MSBTTitle = series["msbt_title"]?.ToObject<Dictionary<string, string>>() ?? new Dictionary<string, string>()
            };
        }

        private static sbyte ToSByte(int value)
        {
            if (value < sbyte.MinValue)
                return sbyte.MinValue;
            if (value > sbyte.MaxValue)
                return sbyte.MaxValue;

            return (sbyte)value;
        }

        #endregion

        #region Series Sets

        private static readonly HashSet<string> DlcSeries = new HashSet<string>(new[]
        {
            "persona", "dragonquest", "banjokazooie", "fatalfury", "arms", "minecraft", "tekken", "kingdomhearts"
        }, StringComparer.OrdinalIgnoreCase);

        private static readonly HashSet<string> VanillaSeries = new HashSet<string>(new[]
        {
            "mario", "mariokart", "donkeykong", "zelda",
            "metroid", "yoshi", "kirby", "starfox", "pokemon", "fzero", "mother",
            "fireemblem", "gamewatch", "palutena", "wario", "pikmin",
            "doubutsu", "wiifit", "punchout", "xenoblade", "metalgear", "sonic",
            "rockman", "pacman", "streetfighter", "finalfantasy", "bayonetta",
            "splatoon", "castlevania", "smashbros", "arms", "persona",
            "dragonquest", "banjokazooie", "fatalfury", "minecraft",
            "tekken", "kingdomhearts", "etc"
        }, StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, List<string>> SeriesToPlaylist = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["doubutsu"] = new List<string> { "bgmanimal" },
            ["bayonetta"] = new List<string> { "bgmbeyo" },
            ["dragonquest"] = new List<string> { "bgmbrave" },
            ["banjokazooie"] = new List<string> { "bgmbuddy" },
            ["tekken"] = new List<string> { "bgmdemon" },
            ["donkeykong"] = new List<string> { "bgmdk" },
            ["fatalfury"] = new List<string> { "bgmdolly" },
            ["castlevania"] = new List<string> { "bgmdracula" },
            ["finalfantasy"] = new List<string> { "bgmedge", "bgmff" },
            ["xenoblade"] = new List<string> { "bgmelement", "bgmxenoblade" },
            ["fireemblem"] = new List<string> { "bgmfe", "bgmmaster" },
            ["starfox"] = new List<string> { "bgmfox" },
            ["fzero"] = new List<string> { "bgmfzero" },
            ["gamewatch"] = new List<string> { "bgmgamewatch" },
            ["palutena"] = new List<string> { "bgmicaros" },
            ["persona"] = new List<string> { "bgmjack" },
            ["kirby"] = new List<string> { "bgmkirby" },
            ["mario"] = new List<string> { "bgmmario" },
            ["metalgear"] = new List<string> { "bgmmetalgear" },
            ["metroid"] = new List<string> { "bgmmetroid" },
            ["mariokart"] = new List<string> { "bgmmkart" },
            ["mother"] = new List<string> { "bgmmother" },
            ["etc"] = new List<string> { "bgmother" },
            ["pacman"] = new List<string> { "bgmpacman" },
            ["minecraft"] = new List<string> { "bgmpickel" },
            ["pikmin"] = new List<string> { "bgmpikmin" },
            ["pokemon"] = new List<string> { "bgmpokemon" },
            ["punchout"] = new List<string> { "bgmpunchout" },
            ["rockman"] = new List<string> { "bgmrockman" },
            ["streetfighter"] = new List<string> { "bgmsf" },
            ["smashbros"] = new List<string> { "bgmsmashbtl" },
            ["sonic"] = new List<string> { "bgmsonic" },
            ["splatoon"] = new List<string> { "bgmspla" },
            ["arms"] = new List<string> { "bgmtantan" },
            ["kingdomhearts"] = new List<string> { "bgmtrail" },
            ["wario"] = new List<string> { "bgmwario" },
            ["wiifit"] = new List<string> { "bgmwiifit" },
            ["yoshi"] = new List<string> { "bgmyoshi" },
            ["zelda"] = new List<string> { "bgmzelda" }
        };

        #endregion
    }
}
