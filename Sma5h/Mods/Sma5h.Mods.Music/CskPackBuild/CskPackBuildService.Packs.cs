using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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
        #region Pack Generation

        private void GenerateCskPacks(IEnumerable<CskModContext> contexts, string generatedBgmFolder, string outputRoot, HashSet<string> selectedSeriesKeys, CskBuildResources buildResources, bool includeAudio)
        {
            var contextList = contexts.ToList();
            var allSeries = contextList.SelectMany(context => context.SeriesList).ToList();
            var selectedSeries = contextList
                .SelectMany(context => context.SeriesList
                    .Where(series => selectedSeriesKeys.Contains(CreateSeriesKey(context.Mod, series))))
                .ToList();
            var onlyCoreReplacements = HasOnlyCoreReplacementBgms(selectedSeries, buildResources.CoreBgmIds);
            var seriesSoundOrder = BuildSeriesSoundOrder(
                allSeries,
                buildResources.OrderOverride);
            var seriesDatabaseFileCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var context in contextList)
            {
                _logger.LogInformation("Generating CSK packs from {MetadataPath}", context.MetadataPath);

                foreach (var series in context.SeriesList.Where(series => selectedSeriesKeys.Contains(CreateSeriesKey(context.Mod, series))))
                {
                    var seriesName = GetString(series, "name_id");
                    var databaseFileBaseName = SanitizePathSegment(seriesName, "series", "series database file name");
                    seriesDatabaseFileCounts.TryGetValue(databaseFileBaseName, out var databaseFileCount);
                    databaseFileCount++;
                    seriesDatabaseFileCounts[databaseFileBaseName] = databaseFileCount;
                    var databaseFileName = databaseFileCount == 1
                        ? $"{databaseFileBaseName}.json"
                        : $"{databaseFileBaseName}{databaseFileCount}.json";

                    var savedPath = ProcessSeries(
                        series,
                        context.SafePackName,
                        databaseFileName,
                        outputRoot,
                        generatedBgmFolder,
                        buildResources.PlaylistData,
                        context.SeriesIdToName,
                        buildResources.CoreBgmOverride,
                        buildResources.OrderOverride,
                        buildResources.CoreGameSeriesById,
                        seriesSoundOrder,
                        buildResources.StageOverride,
                        buildResources.CoreGameOverride,
                        buildResources.CoreSeriesOverride,
                        context.Metadata,
                        buildResources.CoreBgmIds,
                        includeAudio,
                        buildResources);

                    _logger.LogInformation("[CSK] Saved {SeriesName}: {SavedPath}", GetString(series, "name_id", "<unknown>"), savedPath);
                }
            }

            GenerateSeriesOrderPack(
                contextList,
                outputRoot,
                selectedSeriesKeys,
                seriesSoundOrder,
                buildResources.CoreSeriesOverride,
                onlyCoreReplacements);
        }

        private void GenerateSingleCskPack(IEnumerable<CskModContext> contexts, string generatedBgmFolder, string outputRoot, HashSet<string> selectedSeriesKeys, CskBuildResources buildResources, bool includeAudio)
        {
            var contextList = contexts.ToList();
            var allSeries = contextList.SelectMany(context => context.SeriesList).ToList();
            var selectedSeries = contextList
                .SelectMany(context => context.SeriesList
                    .Where(series => selectedSeriesKeys.Contains(CreateSeriesKey(context.Mod, series)))
                    .Select(series => new { Context = context, Series = series }))
                .ToList();

            if (selectedSeries.Count == 0)
                throw new InvalidOperationException("No selected series were found in the currently loaded music mods.");

            var seriesSoundOrder = BuildSeriesSoundOrder(allSeries, buildResources.OrderOverride);
            var seriesIdToName = contextList
                .SelectMany(context => context.SeriesIdToName)
                .GroupBy(p => p.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(p => p.Key, p => p.First().Value, StringComparer.OrdinalIgnoreCase);

            var singlePackFolderName = GetSingleCskPackFolderName(contextList);
            var packFolderName = GetSingleCskPackOutputFolderName(contextList);
            var packRoot = GetSingleCskPackRoot(outputRoot, contextList);
            var databaseFolder = Path.Combine(packRoot, "database");
            var uiFolder = Path.Combine(packRoot, "ui", "message");
            Directory.CreateDirectory(databaseFolder);
            Directory.CreateDirectory(uiFolder);

            var songData = CreateSongData();
            var msgBgmEntries = new List<string>();
            var msgTitleEntries = new List<string>();
            var metadataBgmIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var onlyCoreReplacements = HasOnlyCoreReplacementBgms(
                selectedSeries.Select(p => p.Series),
                buildResources.CoreBgmIds);

            foreach (var item in selectedSeries)
            {
                var seriesName = GetString(item.Series, "name_id", "<unknown>");
                _logger.LogInformation("[CSK] Adding {SeriesName} to single CSK pack.", seriesName);
                CopySeriesIcon(item.Series, packRoot);
                PopulateSeriesPackData(
                    item.Series,
                    songData,
                    msgBgmEntries,
                    msgTitleEntries,
                    packFolderName,
                    outputRoot,
                    generatedBgmFolder,
                    buildResources.PlaylistData,
                    seriesIdToName,
                    buildResources.CoreBgmOverride,
                    buildResources.OrderOverride,
                    buildResources.CoreGameSeriesById,
                    seriesSoundOrder,
                    buildResources.StageOverride,
                    buildResources.CoreGameOverride,
                    buildResources.CoreSeriesOverride,
                    item.Context.Metadata,
                    buildResources.CoreBgmIds,
                    metadataBgmIds,
                    includeAudio);

                if (includeAudio)
                    CopyCoreVolumeOverrideBankFiles(seriesName, packFolderName, outputRoot, generatedBgmFolder, buildResources);
            }

            var coreOnlyVanillaSeriesOrderEntries = CreateCoreOnlyVanillaSeriesOrderEntries(
                contextList,
                selectedSeriesKeys,
                seriesSoundOrder,
                buildResources.CoreSeriesOverride);
            AddSeriesOrderEntries(songData, coreOnlyVanillaSeriesOrderEntries);

            NormalizeCombinedSongData(songData);
            if (onlyCoreReplacements)
                KeepOnlyReplacementBgmDatabaseEntries(songData, metadataBgmIds);

            WriteCombinedXmsbt(Path.Combine(uiFolder, "msg_bgm.xmsbt"), msgBgmEntries);
            WriteCombinedXmsbt(Path.Combine(uiFolder, "msg_title.xmsbt"), msgTitleEntries);

            var outputJsonPath = Path.Combine(databaseFolder, "song_data.json");
            File.WriteAllText(outputJsonPath, JsonConvert.SerializeObject(songData, Formatting.Indented), new UTF8Encoding(false));
            _logger.LogInformation("[CSK] Saved single CSK pack: {SavedPath}", outputJsonPath);
        }

        private void GenerateSingleAudioOnlyCskPack(IEnumerable<CskModContext> contexts, string generatedBgmFolder, string outputRoot, HashSet<string> selectedSeriesKeys, CskBuildResources buildResources)
        {
            var contextList = contexts.ToList();
            var packRoot = GetSingleCskPackRoot(outputRoot, contextList);
            var bgmFolder = Path.Combine(packRoot, "stream;", "sound", "bgm");
            CopyGeneratedBgmFiles(generatedBgmFolder, bgmFolder);

            var msgBgmEntries = CollectSelectedBgmMessages(contextList, selectedSeriesKeys);
            if (msgBgmEntries.Count > 0)
            {
                var uiFolder = Path.Combine(packRoot, "ui", "message");
                Directory.CreateDirectory(uiFolder);
                WriteCombinedXmsbt(Path.Combine(uiFolder, "msg_bgm.xmsbt"), msgBgmEntries);
            }

            _logger.LogInformation("[CSK] Saved single audio-only CSK pack: {SavedPath}", packRoot);
        }

        private List<string> CollectSelectedBgmMessages(IEnumerable<CskModContext> contexts, HashSet<string> selectedSeriesKeys)
        {
            var output = new List<string>();
            foreach (var context in contexts)
            {
                foreach (var series in context.SeriesList.Where(series => selectedSeriesKeys.Contains(CreateSeriesKey(context.Mod, series))))
                {
                    foreach (JObject game in GetArray(series, "games"))
                    {
                        foreach (JObject bgm in GetArray(game, "bgms"))
                            AddBgmMessagesFromMetadata(output, bgm);
                    }
                }
            }

            return output;
        }

        private void AddBgmMessagesFromMetadata(List<string> msgBgmEntries, JObject bgm)
        {
            var db = bgm["db_root"] as JObject;
            var bgmProp = bgm["bgm_properties"] as JObject;
            var nameId = GetString(bgmProp, "name_id");
            if (string.IsNullOrEmpty(nameId))
                return;

            AddOrReplaceMessage(msgBgmEntries, $"bgm_title_{nameId}", GetLocalizedString(db?["msbt_title"], nameId));
            AddOrReplaceMessage(msgBgmEntries, $"bgm_author_{nameId}", GetLocalizedString(db?["msbt_author"]));
            AddOrReplaceMessage(msgBgmEntries, $"bgm_copyright_{nameId}", GetLocalizedString(db?["msbt_copyright"]));
        }

        private static string GetSingleCskPackFolderName(IReadOnlyList<CskModContext> contexts)
        {
            if (contexts.Count == 1 && !string.IsNullOrWhiteSpace(contexts[0].SafePackName))
                return contexts[0].SafePackName;

            return SinglePackFolderName;
        }

        private string GetSingleCskPackOutputFolderName(IReadOnlyList<CskModContext> contexts)
        {
            return _config.CurrentValue.Sma5hMusicGUI?.SaveOutputToSubfolder == false
                ? string.Empty
                : GetSingleCskPackFolderName(contexts);
        }

        private string GetSingleCskPackRoot(string outputRoot, IReadOnlyList<CskModContext> contexts)
        {
            var folderName = GetSingleCskPackOutputFolderName(contexts);
            return string.IsNullOrEmpty(folderName)
                ? outputRoot
                : Path.Combine(outputRoot, folderName);
        }

        #endregion

        #region Pack Helpers

        private static JObject GetEffectiveOverrideObject(JObject source, JObject overrides, string idKey)
        {
            if (source == null)
                source = new JObject();

            if (overrides == null)
                return source;

            var id = GetString(source, idKey);
            var overrideObject = overrides[id] as JObject;
            return overrideObject == null ? source : MergeObjects(source, overrideObject);
        }

        private static bool ShouldAddGameTitleEntry(JObject game, string seriesName, Dictionary<string, string> coreGameSeriesById)
        {
            var uiGameTitleId = GetString(game, "ui_gametitle_id");
            if (string.IsNullOrEmpty(uiGameTitleId) || !coreGameSeriesById.ContainsKey(uiGameTitleId))
                return true;

            return !string.Equals(coreGameSeriesById[uiGameTitleId], seriesName, StringComparison.OrdinalIgnoreCase);
        }

        #endregion

        #region Series Processing

        private string ProcessSeries(
            JObject series,
            string packName,
            string databaseFileName,
            string outputRoot,
            string generatedBgmFolder,
            JObject playlistData,
            Dictionary<string, string> seriesIdToName,
            JObject coreBgmOverride,
            JObject orderOverride,
            Dictionary<string, string> coreGameSeriesById,
            Dictionary<string, int> seriesSoundOrder,
            JObject stageOverride,
            JObject coreGameOverride,
            JObject coreSeriesOverride,
            JObject metadata,
            HashSet<string> coreBgmIds,
            bool includeAudio,
            CskBuildResources buildResources)
        {
            var seriesName = GetString(series, "name_id");
            var realName = GetSeriesDisplayName(series);
            var safeSeriesName = SanitizePathSegment(realName, seriesName, "series folder name");
            var seriesFolderName = SanitizePathSegment($"{packName} - {safeSeriesName}", seriesName, "full series folder name");

            var seriesDbFolder = Path.Combine(outputRoot, seriesFolderName, "database");
            var seriesUiFolder = Path.Combine(outputRoot, seriesFolderName, "ui", "message");
            var onlyCoreReplacements = HasOnlyCoreReplacementBgms(new[] { series }, coreBgmIds);
            Directory.CreateDirectory(seriesDbFolder);
            Directory.CreateDirectory(seriesUiFolder);
            CopySeriesIcon(series, Path.Combine(outputRoot, seriesFolderName));

            var songData = CreateSongData();
            var msgBgmEntries = new List<string>();
            var msgTitleEntries = new List<string>();
            var metadataBgmIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            PopulateSeriesPackData(
                series,
                songData,
                msgBgmEntries,
                msgTitleEntries,
                seriesFolderName,
                outputRoot,
                generatedBgmFolder,
                playlistData,
                seriesIdToName,
                coreBgmOverride,
                orderOverride,
                coreGameSeriesById,
                seriesSoundOrder,
                stageOverride,
                coreGameOverride,
                coreSeriesOverride,
                metadata,
                coreBgmIds,
                metadataBgmIds,
                includeAudio);

            if (includeAudio)
                CopyCoreVolumeOverrideBankFiles(seriesName, seriesFolderName, outputRoot, generatedBgmFolder, buildResources);

            if (onlyCoreReplacements)
                KeepOnlyReplacementBgmDatabaseEntries(songData, metadataBgmIds);

            var outputJsonPath = Path.Combine(seriesDbFolder, databaseFileName);
            File.WriteAllText(outputJsonPath, JsonConvert.SerializeObject(songData, Formatting.Indented), new UTF8Encoding(false));
            WriteXmsbt(Path.Combine(seriesUiFolder, "msg_bgm.xmsbt"), msgBgmEntries);
            WriteXmsbt(Path.Combine(seriesUiFolder, "msg_title.xmsbt"), msgTitleEntries);
            return outputJsonPath;
        }

        private void KeepOnlyReplacementBgmDatabaseEntries(JObject songData, HashSet<string> replacementBgmIds)
        {
            var menuValueByBgmId = _audioStateService.GetOriginalCoreBgmDbRootEntries()
                .Where(p => !string.IsNullOrEmpty(p.UiBgmId))
                .ToDictionary(p => p.UiBgmId, p => p.MenuValue, StringComparer.OrdinalIgnoreCase);

            var bgmDatabaseEntries = new JArray(GetArray(songData, "bgm_database_entries")
                .OfType<JObject>()
                .Where(p => replacementBgmIds.Contains(GetString(p, "ui_bgm_id")))
                .Select(p =>
                {
                    var entry = (JObject)p.DeepClone();
                    var uiBgmId = GetString(entry, "ui_bgm_id");
                    if (!string.IsNullOrEmpty(uiBgmId) && menuValueByBgmId.TryGetValue(uiBgmId, out var menuValue))
                        entry["test_disp_order"] = menuValue;

                    return entry;
                }));

            songData.RemoveAll();
            songData["bgm_database_entries"] = bgmDatabaseEntries;
        }

        private static bool HasOnlyCoreReplacementBgms(IEnumerable<JObject> seriesEntries, HashSet<string> coreBgmIds)
        {
            if (coreBgmIds == null || coreBgmIds.Count == 0)
                return false;

            var bgmIds = seriesEntries
                .Where(p => p != null)
                .SelectMany(series => GetArray(series, "games").OfType<JObject>())
                .SelectMany(game => GetArray(game, "bgms").OfType<JObject>())
                .Select(bgm => GetString(bgm["db_root"], "ui_bgm_id"))
                .Where(p => !string.IsNullOrEmpty(p))
                .ToList();

            return bgmIds.Count > 0 && bgmIds.All(coreBgmIds.Contains);
        }

        private void DeleteDirectoryIfExists(string path, string logMessage)
        {
            if (!Directory.Exists(path))
                return;

            Directory.Delete(path, true);
            _logger.LogInformation(logMessage, path);
        }

        private void PopulateSeriesPackData(
            JObject series,
            JObject songData,
            List<string> msgBgmEntries,
            List<string> msgTitleEntries,
            string seriesFolderName,
            string outputRoot,
            string generatedBgmFolder,
            JObject playlistData,
            Dictionary<string, string> seriesIdToName,
            JObject coreBgmOverride,
            JObject orderOverride,
            Dictionary<string, string> coreGameSeriesById,
            Dictionary<string, int> seriesSoundOrder,
            JObject stageOverride,
            JObject coreGameOverride,
            JObject coreSeriesOverride,
            JObject metadata,
            HashSet<string> coreBgmIds,
            HashSet<string> metadataBgmIds,
            bool includeAudio)
        {
            var seriesName = GetString(series, "name_id");
            var orderCounter = GetNextPlaylistOrder(seriesName, playlistData);
            var seriesTitle = GetLocalizedString(series["msbt_title"], seriesName);

            if (coreSeriesOverride != null)
            {
                var overrideEntry = coreSeriesOverride[GetString(series, "ui_series_id")] as JObject;
                if (overrideEntry != null)
                    seriesTitle = GetLocalizedString(overrideEntry["msbt_title"], seriesTitle);
            }

            if (orderOverride != null || !VanillaSeries.Contains(seriesName) || seriesName.StartsWith("etc", StringComparison.OrdinalIgnoreCase))
            {
                var dispOrderSound = GetSeriesSoundOrder(seriesSoundOrder, series);
                if (dispOrderSound > 127)
                    dispOrderSound = 127;

                GetArray(songData, "series_database_entries").Add(CreateSeriesDatabaseEntry(series, coreSeriesOverride, dispOrderSound));
            }

            msgTitleEntries.Add(MakeEntry($"tit_series_snd_{seriesName}", seriesTitle));
            msgTitleEntries.Add(MakeEntry($"tit_series_{seriesName}", seriesTitle));

            foreach (JObject game in GetArray(series, "games"))
            {
                if (coreGameOverride != null)
                {
                    var gameOverride = coreGameOverride[GetString(game, "ui_gametitle_id")] as JObject;
                    if (gameOverride != null && GetString(gameOverride, "ui_series_id") != GetString(series, "ui_series_id"))
                        continue;
                }

                var effectiveGame = GetEffectiveOverrideObject(game, coreGameOverride, "ui_gametitle_id");
                var gameName = GetString(effectiveGame, "name_id", GetString(game, "name_id"));
                if (ShouldAddGameTitleEntry(effectiveGame, seriesName, coreGameSeriesById))
                    AddGameTitleEntry(songData, effectiveGame);

                var gameTitle = GetLocalizedString(effectiveGame["msbt_title"], gameName);
                msgTitleEntries.Add(MakeEntry($"tit_{gameName}", gameTitle));

                foreach (JObject bgm in GetArray(game, "bgms"))
                    orderCounter = ProcessBgm(bgm, songData, playlistData, msgBgmEntries, coreBgmOverride, orderOverride, seriesName, seriesFolderName, outputRoot, generatedBgmFolder, metadataBgmIds, includeAudio, orderCounter);
            }

            ProcessCoreGameMovedBgms(series, metadata, coreGameOverride, songData, playlistData, msgBgmEntries, msgTitleEntries, coreBgmOverride, orderOverride, seriesName, seriesFolderName, outputRoot, generatedBgmFolder, metadataBgmIds, includeAudio, ref orderCounter);
            PopulateVanillaPlaylists(songData, seriesName, playlistData, coreBgmIds, coreBgmOverride, orderOverride);
            PopulateStageDatabaseEntries(songData, seriesName, stageOverride, playlistData);
            ProcessCoreBgmOverrides(songData, playlistData, msgBgmEntries, msgTitleEntries, seriesName, seriesIdToName, coreBgmOverride, orderOverride, coreGameOverride, ref orderCounter);
            AddCoreGameOverridesForNewSeries(songData, series, seriesName, coreGameOverride);
        }

        #endregion

    }
}
