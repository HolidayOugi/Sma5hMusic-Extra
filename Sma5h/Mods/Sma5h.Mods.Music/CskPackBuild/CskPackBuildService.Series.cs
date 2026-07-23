using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Sma5h.Mods.Music;
using Sma5h.Mods.Music.Helpers;
using Sma5h.Mods.Music.Interfaces;
using Sma5h.Mods.Music.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Sma5h.Mods.Music.CskPackBuild
{
    public partial class CskPackBuildService
    {
        #region Generation

        private void GenerateSeriesOrderPack(
            List<CskModContext> contexts,
            string outputRoot,
            HashSet<string> selectedSeriesKeys,
            Dictionary<string, int> seriesSoundOrder,
            JObject coreSeriesOverride)
        {
            //# of series with no added entries
            var seriesEntries = CreateVanillaSeriesOrderEntries(
                contexts,
                selectedSeriesKeys,
                seriesSoundOrder,
                coreSeriesOverride);

            if (seriesEntries.Count == 0)
                return;

            var folderName = contexts.Count > 1
                ? "CSK Packs - Series Order"
                : SanitizePathSegment(
                    $"{contexts.Select(p => p.SafePackName).FirstOrDefault(p => !string.IsNullOrWhiteSpace(p)) ?? SinglePackFolderName} - Series Order",
                    "Series Order",
                    "series order folder name");
            var databaseFolder = Path.Combine(outputRoot, folderName, "database");
            Directory.CreateDirectory(databaseFolder);

            var songData = CreateSeriesOrderSongData(seriesEntries);
            var outputJsonPath = Path.Combine(databaseFolder, "series_order.json");
            File.WriteAllText(outputJsonPath, JsonConvert.SerializeObject(songData, Formatting.Indented), new UTF8Encoding(false));
            _logger.LogInformation("[CSK] Saved series order pack: {SavedPath}", outputJsonPath);
        }

        private void GenerateVanillaSongsChangesPack(
            List<CskModContext> contexts,
            string outputRoot,
            HashSet<string> selectedSeriesKeys,
            string generatedBgmFolder,
            CskBuildResources buildResources,
            bool includeAudio)
        {
            var folderName = contexts.Count == 1
                ? SanitizePathSegment(
                    $"{contexts.Select(p => p.SafePackName).FirstOrDefault(p => !string.IsNullOrWhiteSpace(p)) ?? SinglePackFolderName} - Vanilla Songs Changes",
                    "Vanilla Songs Changes",
                    "vanilla songs changes folder name")
                : "CSK Packs - Vanilla Songs Changes";
            var packRoot = Path.Combine(outputRoot, folderName);
            var songData = new JObject { ["bgm_database_entries"] = new JArray() };
            var msgBgmEntries = new List<string>();
            var msgTitleEntries = new List<string>();

            if (!AddVanillaSongsChanges(contexts, selectedSeriesKeys, songData, msgBgmEntries, msgTitleEntries, packRoot, generatedBgmFolder, buildResources, includeAudio))
                return;

            if (GetArray(songData, "bgm_database_entries").Count > 0)
            {
                var databaseFolder = Path.Combine(packRoot, "database");
                Directory.CreateDirectory(databaseFolder);
                File.WriteAllText(
                    Path.Combine(databaseFolder, "database.json"),
                    JsonConvert.SerializeObject(songData, Formatting.Indented),
                    new UTF8Encoding(false));
            }

            if (msgBgmEntries.Count > 0 || msgTitleEntries.Count > 0)
            {
                var uiFolder = Path.Combine(packRoot, "ui", "message");
                Directory.CreateDirectory(uiFolder);
                if (msgBgmEntries.Count > 0)
                    WriteCombinedXmsbt(Path.Combine(uiFolder, "msg_bgm.xmsbt"), msgBgmEntries);
                if (msgTitleEntries.Count > 0)
                    WriteCombinedXmsbt(Path.Combine(uiFolder, "msg_title.xmsbt"), msgTitleEntries);
            }

            _logger.LogInformation("[CSK] Saved vanilla songs changes pack: {SavedPath}", packRoot);
        }

        private bool AddVanillaSongsChanges(
            List<CskModContext> contexts,
            HashSet<string> selectedSeriesKeys,
            JObject songData,
            List<string> msgBgmEntries,
            List<string> msgTitleEntries,
            string packRoot,
            string generatedBgmFolder,
            CskBuildResources buildResources,
            bool includeAudio)
        {
            if (buildResources.CoreBgmOverride == null)
                return false;

            var bgmCount = GetArray(songData, "bgm_database_entries").Count;
            var msgBgmCount = msgBgmEntries.Count;
            var msgTitleCount = msgTitleEntries.Count;
            var copiedAudio = false;
            //get all games already covered by the selected series
            var selectedGameTitleIds = GetSelectedGameTitleIds(contexts, selectedSeriesKeys ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase));

            var dbRoots = buildResources.CoreBgmOverride["CoreBgmDbRootOverrides"] as JObject ?? new JObject();

            foreach (var dbProperty in dbRoots.Properties())
            {
                var db = dbProperty.Value as JObject;
                var uiBgmId = GetString(db, "ui_bgm_id", dbProperty.Name);
                if (string.IsNullOrEmpty(uiBgmId))
                    continue;

                var uiGameTitleId = GetString(db, "ui_gametitle_id");
                if (selectedGameTitleIds.Contains(uiGameTitleId))
                    continue;

                var bgmDbRootEntry = _audioStateService.GetBgmDbRootEntries()
                    .Concat(_audioStateService.GetOriginalCoreBgmDbRootEntries())
                    .Where(p => string.Equals(p.UiBgmId, uiBgmId, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(p.NameId))
                    .FirstOrDefault();
                var nameId = bgmDbRootEntry?.NameId;
                if (string.IsNullOrEmpty(nameId) || _unavailableBgmNameIds.Value?.Contains(nameId) == true)
                    continue;

                GetArray(songData, "bgm_database_entries").Add(new JObject
                {
                    ["ui_bgm_id"] = uiBgmId,
                    ["clone_from_ui_bgm_id"] = CloneBgmId,
                    ["stream_set_id"] = GetString(db, "stream_set_id"),
                    ["name_id"] = nameId,
                    ["ui_gametitle_id"] = GetString(db, "ui_gametitle_id"),
                    ["test_disp_order"] = bgmDbRootEntry.TestDispOrder <= 4 ? bgmDbRootEntry.TestDispOrder : bgmDbRootEntry.MenuValue, //for the first 5 songs get their test disp order
                    ["record_type"] = GetString(db, "record_type", "record_original")
                });

                //create xmsbt
                AddOptionalBgmMessageUnique(msgBgmEntries, $"bgm_title_{nameId}", db["msbt_title"]);
                AddOptionalBgmMessageUnique(msgBgmEntries, $"bgm_author_{nameId}", db["msbt_author"]);
                AddOptionalBgmMessageUnique(msgBgmEntries, $"bgm_copyright_{nameId}", db["msbt_copyright"]);

                var game = buildResources.CoreGameOverride?[uiGameTitleId] as JObject;
                var gameTitle = GetLocalizedString(game?["msbt_title"]);
                if (!string.IsNullOrEmpty(gameTitle))
                    AddUniqueMessage(msgTitleEntries, $"tit_{GetString(game, "name_id")}", gameTitle);

                //add game title entry if custom
                AddNonCoreGameTitleEntry(songData, msgTitleEntries, uiGameTitleId);
            }

            //write nus3bank for volume overrides
            var volumeEntries = GetCoreVolumeOverrideEntries(buildResources)
                .Where(p =>
                {
                    var db = GetOriginalCoreDbRootByNameId(p.NameId);
                    return db != null &&
                           !selectedGameTitleIds.Contains(db.UiGameTitleId);
                })
                .ToList();

            if (includeAudio && volumeEntries.Count > 0 && !string.IsNullOrEmpty(generatedBgmFolder))
            {
                var destFolder = Path.Combine(packRoot, "stream;", "sound", "bgm");
                Directory.CreateDirectory(generatedBgmFolder);
                foreach (var entry in volumeEntries)
                {
                    var source = Path.Combine(generatedBgmFolder, string.Format(MusicConstants.GameResources.NUS3BANK_FILE, entry.NameId));
                    if (!File.Exists(source))
                        _nus3AudioService.GenerateNus3Bank(entry.NameId, entry.Volume, source);

                    CopyIfExists(source, Path.Combine(destFolder, Path.GetFileName(source)));
                    copiedAudio = true;
                }
            }

            return GetArray(songData, "bgm_database_entries").Count > bgmCount ||
                   msgBgmEntries.Count > msgBgmCount ||
                   msgTitleEntries.Count > msgTitleCount ||
                   copiedAudio;
        }

        private void AddNonCoreGameTitleEntry(
            JObject songData,
            List<string> msgTitleEntries,
            string uiGameTitleId)
        {
            if (string.IsNullOrEmpty(uiGameTitleId))
                return;

            var gameEntry = _audioStateService.GetGameTitleEntries()
                .FirstOrDefault(p => string.Equals(p.UiGameTitleId, uiGameTitleId, StringComparison.OrdinalIgnoreCase));
            if (gameEntry == null || gameEntry.Source == EntrySource.Core)
                return;

            if (!(songData["gametitle_database_entries"] is JArray))
                songData["gametitle_database_entries"] = new JArray();

            var game = CreateGameObject(gameEntry);
            if (!GetArray(songData, "gametitle_database_entries")
                .Any(p => string.Equals(GetString(p, "ui_gametitle_id"), uiGameTitleId, StringComparison.OrdinalIgnoreCase)))
            {
                AddGameTitleEntry(songData, game);
            }

            var gameName = GetString(game, "name_id");
            if (!string.IsNullOrEmpty(gameName))
                AddUniqueMessage(msgTitleEntries, $"tit_{gameName}", GetLocalizedString(game["msbt_title"], gameName));
        }

        private HashSet<string> GetSelectedGameTitleIds(IEnumerable<CskModContext> contexts, HashSet<string> selectedSeriesKeys)
        {
            var selectedSeriesIds = contexts
                .SelectMany(context => context.SeriesList
                    .Where(series => selectedSeriesKeys.Contains(CreateSeriesKey(context.Mod, series))))
                .Select(series => GetString(series, "ui_series_id"))
                .Where(p => !string.IsNullOrEmpty(p))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            return _audioStateService.GetGameTitleEntries()
                .Where(p => !string.IsNullOrEmpty(p.UiGameTitleId) &&
                            !string.IsNullOrEmpty(p.UiSeriesId) &&
                            selectedSeriesIds.Contains(p.UiSeriesId))
                .Select(p => p.UiGameTitleId)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        #endregion

        #region Series Options

        private CskPackSeriesOption CreateSeriesOption(CskModContext context, JObject series)
        {
            return new CskPackSeriesOption
            {
                Key = CreateSeriesKey(context.Mod, series),
                DisplayName = GetSeriesDisplayName(series),
                NameId = GetString(series, "name_id"),
                UiSeriesId = GetString(series, "ui_series_id"),
                ModName = context.Mod.Name
            };
        }

        private static string CreateSeriesKey(IMusicMod mod, JObject series)
        {
            return $"{Path.GetFullPath(mod.ModPath)}|{GetString(series, "ui_series_id")}|{GetString(series, "name_id")}";
        }

        private string GetSeriesDisplayName(JObject series)
        {
            var seriesName = GetString(series, "name_id");
            var title = GetLocalizedString(series["msbt_title"]);
            if (string.IsNullOrWhiteSpace(title))
                title = GetLocalizedString(series["title"]);

            return string.IsNullOrWhiteSpace(title) ? seriesName : title;
        }

        #endregion

        #region Effective Series Data

        private JObject BuildEffectiveCoreSeriesData(JObject coreSeriesOverride)
        {
            var seriesData = new JObject();

            foreach (var series in _audioStateService.GetSeriesEntries().Where(p => !string.IsNullOrEmpty(p.UiSeriesId)))
                seriesData[series.UiSeriesId] = CreateSeriesObject(series);

            OverlayProperties(seriesData, coreSeriesOverride);
            return seriesData;
        }

        private static JObject CreateSeriesObject(SeriesEntry series)
        {
            return new JObject
            {
                ["ui_series_id"] = series.UiSeriesId,
                ["name_id"] = series.NameId,
                ["disp_order"] = series.DispOrder,
                ["disp_order_sound"] = series.DispOrderSound,
                ["save_no"] = series.SaveNo,
                ["0x1c38302364"] = series.Unk1,
                ["is_dlc"] = series.IsDlc,
                ["is_patch"] = series.IsPatch,
                ["dlc_chara_id"] = series.DlcCharaId,
                ["is_use_amiibo_bg"] = series.IsUseAmiiboBg,
                ["msbt_title"] = CreateLocalizedObject(series.MSBTTitle)
            };
        }

        #endregion

        #region Series Entries

        private List<JObject> CreateVanillaSeriesOrderEntries(
            IEnumerable<CskModContext> contexts,
            HashSet<string> selectedSeriesKeys,
            Dictionary<string, int> seriesSoundOrder,
            JObject coreSeriesOverride)
        {
            //get all series selected
            var selectedSeriesIds = contexts
                .SelectMany(context => context.SeriesList
                    .Where(series => selectedSeriesKeys.Contains(CreateSeriesKey(context.Mod, series)))
                    .Select(series => GetString(series, "ui_series_id")))
                .Where(p => !string.IsNullOrEmpty(p))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            //get all vanilla series that are not selected
            var unselectedVanillaSeries = _audioStateService.GetSeriesEntries()
                .Where(series => IsVanillaSeries(series.NameId))
                .Where(series => !string.IsNullOrEmpty(series.UiSeriesId))
                .Where(series => !selectedSeriesIds.Contains(series.UiSeriesId));

            //create series entry
            var seriesEntries = unselectedVanillaSeries
                .Select(series =>
                {
                    var seriesObject = CreateSeriesObject(series);
                    var dispOrderSound = GetSeriesSoundOrder(seriesSoundOrder, seriesObject);
                    if (dispOrderSound > 127)
                        dispOrderSound = 127;

                    return CreateSeriesDatabaseEntry(seriesObject, coreSeriesOverride, dispOrderSound);
                })
                .OrderBy(entry => GetInt(entry, "disp_order_sound", 0))
                .ThenBy(entry => GetString(entry, "name_id"), StringComparer.OrdinalIgnoreCase)
                .ToList();

            return seriesEntries;
        }

        private static bool IsVanillaSeries(string seriesName)
        {
            return !string.IsNullOrEmpty(seriesName) &&
                   VanillaSeries.Contains(seriesName);
        }

        private static JObject CreateSeriesOrderSongData(IEnumerable<JObject> seriesEntries)
        {
            return new JObject
            {
                ["series_database_entries"] = new JArray(seriesEntries)
            };
        }

        private static void AddSeriesOrderEntries(JObject songData, IEnumerable<JObject> seriesEntries)
        {
            var entries = GetArray(songData, "series_database_entries");

            foreach (var entry in seriesEntries)
                entries.Add((JObject)entry.DeepClone());
        }

        #endregion

        #region Sound Order

        private Dictionary<string, int> BuildSeriesSoundOrder(IEnumerable<JObject> seriesList, JObject orderOverride)
        {
            var allSeries = seriesList.ToList();
            //get order from the audio state
            var seriesOrder = BuildSeriesSoundOrderFromAudioState(orderOverride);
            //fallback, audio state might fail (TODO: investigate why)
            var metadataOrder = BuildSeriesSoundOrderFromMetadata(allSeries, orderOverride);

            foreach (var series in allSeries)
            {
                var uiSeriesId = GetString(series, "ui_series_id");
                var nameId = GetString(series, "name_id");

                if (!string.IsNullOrEmpty(uiSeriesId) && seriesOrder.ContainsKey(uiSeriesId))
                    SetSeriesOrderKey(seriesOrder, nameId, seriesOrder[uiSeriesId]);
                else if (!string.IsNullOrEmpty(nameId) && seriesOrder.ContainsKey(nameId))
                    SetSeriesOrderKey(seriesOrder, uiSeriesId, seriesOrder[nameId]);
            }

            foreach (var fallbackOrder in metadataOrder)
                SetSeriesOrderKey(seriesOrder, fallbackOrder.Key, fallbackOrder.Value);

            return seriesOrder;
        }

        //get series order based on the min(test_disp_order) of the bgms for each series
        private Dictionary<string, int> BuildSeriesSoundOrderFromAudioState(JObject orderOverride)
        {
            var output = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var seriesEntries = _audioStateService.GetSeriesEntries()
                .Where(p => p.DispOrderSound > -1 && !string.IsNullOrEmpty(p.UiSeriesId))
                .GroupBy(p => p.UiSeriesId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(p => p.Key, p => p.First(), StringComparer.OrdinalIgnoreCase);
            var gameEntries = _audioStateService.GetGameTitleEntries()
                .Where(p => !string.IsNullOrEmpty(p.UiGameTitleId) && !string.IsNullOrEmpty(p.UiSeriesId))
                .GroupBy(p => p.UiGameTitleId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(p => p.Key, p => p.First(), StringComparer.OrdinalIgnoreCase);
            var sortedGames = _audioStateService.GetBgmDbRootEntries()
                .Where(p => !string.IsNullOrEmpty(p.UiGameTitleId))
                .Select(p => new
                {
                    p.UiGameTitleId,
                    Order = GetInt(orderOverride, p.UiBgmId, p.TestDispOrder)
                })
                .Where(p => p.Order >= 0)
                .OrderBy(p => p.Order)
                .GroupBy(p => p.UiGameTitleId, StringComparer.OrdinalIgnoreCase)
                .Select(p => p.First().UiGameTitleId)
                .ToList();

            var index = GetStartingOrderForSeries();
            foreach (var gameId in sortedGames)
            {
                if (!gameEntries.ContainsKey(gameId))
                    continue;

                var uiSeriesId = gameEntries[gameId].UiSeriesId;
                if (!seriesEntries.ContainsKey(uiSeriesId) || output.ContainsKey(uiSeriesId))
                    continue;

                SetSeriesOrderKey(output, uiSeriesId, index);
                SetSeriesOrderKey(output, seriesEntries[uiSeriesId].NameId, index);
                if (index != sbyte.MaxValue)
                    index++;
            }

            return output;
        }

        //legacy method from python script
        private Dictionary<string, int> BuildSeriesSoundOrderFromMetadata(List<JObject> allSeries, JObject orderOverride)
        {
            if (orderOverride != null && orderOverride.HasValues)
            {
                var seriesMinOverride = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                var aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                foreach (var series in allSeries)
                {
                    var uiSeriesId = GetString(series, "ui_series_id");
                    var nameId = GetString(series, "name_id");
                    var bgmIdsToCheck = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    int? minValue = null;

                    foreach (JObject game in GetArray(series, "games"))
                    {
                        foreach (JObject bgm in GetArray(game, "bgms"))
                        {
                            var dbRoot = bgm["db_root"] as JObject;
                            var uiBgmId = GetString(dbRoot, "ui_bgm_id");
                            if (!string.IsNullOrEmpty(uiBgmId))
                                bgmIdsToCheck.Add(uiBgmId);
                        }
                    }

                    foreach (var uiBgmId in bgmIdsToCheck)
                    {
                        var value = GetInt(orderOverride, uiBgmId, int.MinValue);
                        if (value == int.MinValue || value == -1)
                            continue;

                        if (!minValue.HasValue || value < minValue.Value)
                            minValue = value;
                    }

                    SetMinSeriesOrder(seriesMinOverride, nameId, minValue ?? int.MaxValue);
                    if (!string.IsNullOrEmpty(uiSeriesId) && !string.IsNullOrEmpty(nameId))
                        aliases[uiSeriesId] = nameId;
                }

                var ranked = seriesMinOverride
                    .OrderBy(p => p.Value)
                    .ThenBy(p => p.Key, StringComparer.OrdinalIgnoreCase)
                    .Select((p, i) => new { p.Key, Index = GetStartingOrderForSeries() + i })
                    .ToDictionary(p => p.Key, p => p.Index, StringComparer.OrdinalIgnoreCase);

                foreach (var alias in aliases)
                {
                    if (ranked.ContainsKey(alias.Value))
                        ranked[alias.Key] = ranked[alias.Value];
                }

                return ranked;
            }

            var firstCustomOrder = 39;

            return allSeries
                .Where(p => !VanillaSeries.Contains(GetString(p, "name_id")))
                .OrderBy(p => GetSeriesDisplayName(p).ToLowerInvariant())
                .Select((p, i) => new
                {
                    NameId = GetString(p, "name_id"),
                    UiSeriesId = GetString(p, "ui_series_id"),
                    Order = firstCustomOrder + i
                })
                .SelectMany(p => new[]
                {
                    new { Key = p.NameId, p.Order },
                    new { Key = p.UiSeriesId, p.Order }
                })
                .Where(p => !string.IsNullOrEmpty(p.Key))
                .GroupBy(p => p.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(p => p.Key, p => p.First().Order, StringComparer.OrdinalIgnoreCase);
        }

        #endregion

        #region Helpers

        private int GetSeriesSoundOrder(Dictionary<string, int> seriesSoundOrder, JObject series)
        {
            var uiSeriesId = GetString(series, "ui_series_id");
            if (!string.IsNullOrEmpty(uiSeriesId) && seriesSoundOrder.ContainsKey(uiSeriesId))
                return seriesSoundOrder[uiSeriesId];

            var nameId = GetString(series, "name_id");
            if (!string.IsNullOrEmpty(nameId) && seriesSoundOrder.ContainsKey(nameId))
                return seriesSoundOrder[nameId];

            return GetStartingOrderForSeries();
        }

        private int GetStartingOrderForSeries()
        {
            var value = _config.CurrentValue.Sma5hMusicGUI?.StartingOrderForSeries ?? 1;
            return Math.Clamp(value, 0, 39);
        }

        private static void SetSeriesOrderKey(Dictionary<string, int> seriesOrder, string key, int value)
        {
            if (string.IsNullOrEmpty(key) || seriesOrder.ContainsKey(key))
                return;

            seriesOrder[key] = value;
        }

        private static void SetMinSeriesOrder(Dictionary<string, int> seriesOrder, string seriesName, int value)
        {
            if (string.IsNullOrEmpty(seriesName))
                return;

            if (!seriesOrder.TryGetValue(seriesName, out var currentValue) || value < currentValue)
                seriesOrder[seriesName] = value;
        }

        private static JObject CreateSeriesDatabaseEntry(JObject series, JObject coreSeriesOverride, int dispOrderSound)
        {
            var uiSeriesId = GetString(series, "ui_series_id");
            var seriesName = GetString(series, "name_id");
            var effectiveSeries = GetEffectiveOverrideObject(series, coreSeriesOverride, "ui_series_id");
            var isDlcSeries = DlcSeries.Contains(seriesName);
            var entry = new JObject
            {
                ["ui_series_id"] = GetString(effectiveSeries, "ui_series_id", uiSeriesId),
                ["clone_from_series_id"] = CloneSeriesId,
                ["name_id"] = GetString(effectiveSeries, "name_id", seriesName),
                ["disp_order"] = GetInt(effectiveSeries, "disp_order", 0),
                ["disp_order_sound"] = dispOrderSound,
                ["save_no"] = GetInt(effectiveSeries, "save_no", 0),
                ["shown_as_series_in_directory"] = GetBool(effectiveSeries, "0x1c38302364", false),
                ["is_dlc"] = GetBool(effectiveSeries, "is_dlc", isDlcSeries),
                ["is_patch"] = GetBool(effectiveSeries, "is_patch", isDlcSeries),
                ["is_use_amiibo_bg"] = GetBool(effectiveSeries, "is_use_amiibo_bg", false)
            };

            var dlcCharaId = GetString(effectiveSeries, "dlc_chara_id");
            if (!string.IsNullOrEmpty(dlcCharaId))
                entry["dlc_chara_id"] = dlcCharaId;

            return entry;
        }

        #endregion
    }
}
