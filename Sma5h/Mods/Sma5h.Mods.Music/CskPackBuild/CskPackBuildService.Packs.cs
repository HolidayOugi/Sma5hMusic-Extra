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

        //generation of multiple packs for each series
        private void GenerateCskPacks(IEnumerable<CskModContext> contexts, string generatedBgmFolder, string outputRoot, HashSet<string> selectedSeriesKeys, CskBuildResources buildResources, bool includeAudio)
        {
            var contextList = contexts.ToList();
            var allSeries = contextList.SelectMany(context => context.SeriesList).ToList();
            //build sound order for music select menu
            var seriesSoundOrder = BuildSeriesSoundOrder(
                allSeries,
                buildResources.OrderOverride);
            var seriesDatabaseFileCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var copiedSeriesIconKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var context in contextList)
            {
                _logger.LogInformation("Generating CSK packs from {MetadataPath}", context.MetadataPath);

                foreach (var series in context.SeriesList.Where(series => selectedSeriesKeys.Contains(CreateSeriesKey(context.Mod, series))))
                {
                    var seriesName = GetString(series, "name_id");
                    var databaseFileBaseName = SanitizePathSegment(seriesName, "series", "series database file name");
                    //if multiple packs for same series, append a number to the file name
                    seriesDatabaseFileCounts.TryGetValue(databaseFileBaseName, out var databaseFileCount);
                    databaseFileCount++;
                    seriesDatabaseFileCounts[databaseFileBaseName] = databaseFileCount;
                    var databaseFileName = databaseFileCount == 1
                        ? $"{databaseFileBaseName}.json"
                        : $"{databaseFileBaseName}{databaseFileCount}.json";

                    //process individual series
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
                        buildResources,
                        copiedSeriesIconKeys);

                    _logger.LogInformation("[CSK] Saved {SeriesName}: {SavedPath}", GetString(series, "name_id", "<unknown>"), savedPath);
                }
            }

            //generates series order pack for vanilla series not included
            GenerateSeriesOrderPack(
                contextList,
                outputRoot,
                selectedSeriesKeys,
                seriesSoundOrder,
                buildResources.CoreSeriesOverride);

            //generates pack with vanilla song changes for non-selected series
            GenerateVanillaSongsChangesPack(
                contextList,
                outputRoot,
                selectedSeriesKeys,
                generatedBgmFolder,
                buildResources,
                includeAudio);
        }

        //generation of one pack for each selected mod
        private void GenerateCskPacksByMod(IEnumerable<CskModContext> contexts, string generatedBgmFolder, string outputRoot, HashSet<string> selectedSeriesKeys, CskBuildResources buildResources, bool includeAudio)
        {
            var contextList = contexts.ToList();
            var allSeries = contextList.SelectMany(context => context.SeriesList).ToList();
            var seriesSoundOrder = BuildSeriesSoundOrder(allSeries, buildResources.OrderOverride);

            foreach (var context in contextList.Where(context => context.SeriesList.Any(series => selectedSeriesKeys.Contains(CreateSeriesKey(context.Mod, series)))))
            {
                var packFolderName = SanitizePathSegment(context.Mod.Name, context.SafePackName, "mod pack folder name");
                var packRoot = Path.Combine(outputRoot, packFolderName);
                var databaseFolder = Path.Combine(packRoot, "database");
                var uiFolder = Path.Combine(packRoot, "ui", "message");
                Directory.CreateDirectory(databaseFolder);
                Directory.CreateDirectory(uiFolder);

                var songData = CreateSongData();
                var msgBgmEntries = new List<string>();
                var msgTitleEntries = new List<string>();
                var metadataBgmIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                _logger.LogInformation("Generating CSK pack for mod {ModName} from {MetadataPath}", context.Mod.Name, context.MetadataPath);

                foreach (var series in context.SeriesList.Where(series => selectedSeriesKeys.Contains(CreateSeriesKey(context.Mod, series))))
                {
                    var seriesName = GetString(series, "name_id", "<unknown>");
                    _logger.LogInformation("[CSK] Adding {SeriesName} to mod pack {ModName}.", seriesName, context.Mod.Name);
                    CopySeriesIcon(series, packRoot);
                    PopulateSeriesPackData(
                        series,
                        songData,
                        msgBgmEntries,
                        msgTitleEntries,
                        packFolderName,
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
                        metadataBgmIds,
                        includeAudio);

                    if (includeAudio)
                        CopyCoreVolumeOverrideBankFiles(seriesName, packFolderName, outputRoot, generatedBgmFolder, buildResources);
                }

                NormalizeCombinedSongData(songData);
                WriteCombinedXmsbt(Path.Combine(uiFolder, "msg_bgm.xmsbt"), msgBgmEntries);
                WriteCombinedXmsbt(Path.Combine(uiFolder, "msg_title.xmsbt"), msgTitleEntries);

                var databaseFileBaseName = SanitizePathSegment(
                    context.Mod.Name.Replace(' ', '_').ToLowerInvariant(),
                    context.SafePackName.Replace(' ', '_').ToLowerInvariant(),
                    "mod database file name");
                var outputJsonPath = Path.Combine(databaseFolder, $"{databaseFileBaseName}.json");
                File.WriteAllText(outputJsonPath, JsonConvert.SerializeObject(songData, Formatting.Indented), new UTF8Encoding(false));
                _logger.LogInformation("[CSK] Saved mod CSK pack: {SavedPath}", outputJsonPath);
            }

            //generates series order pack for vanilla series not included
            GenerateSeriesOrderPack(
                contextList,
                outputRoot,
                selectedSeriesKeys,
                seriesSoundOrder,
                buildResources.CoreSeriesOverride);

            //generates pack with vanilla song changes for non-selected series
            GenerateVanillaSongsChangesPack(
                contextList,
                outputRoot,
                selectedSeriesKeys,
                generatedBgmFolder,
                buildResources,
                includeAudio);
        }

        //generation of a single pack for all series
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

            //build the data for each series
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
                    //copy nus3bank files for core volume overrides for this series
                    CopyCoreVolumeOverrideBankFiles(seriesName, packFolderName, outputRoot, generatedBgmFolder, buildResources);
            }

            //adds vanilla series entries for sound order
            var VanillaSeriesOrderEntries = CreateVanillaSeriesOrderEntries(
                contextList,
                selectedSeriesKeys,
                seriesSoundOrder,
                buildResources.CoreSeriesOverride);
            AddSeriesOrderEntries(songData, VanillaSeriesOrderEntries);

            //adds vanilla song changes for non-selected series
            AddVanillaSongsChanges(contextList, selectedSeriesKeys, songData, msgBgmEntries, msgTitleEntries, packRoot, generatedBgmFolder, buildResources, includeAudio);

            //remove duplicates
            NormalizeCombinedSongData(songData);
            
            WriteCombinedXmsbt(Path.Combine(uiFolder, "msg_bgm.xmsbt"), msgBgmEntries);
            WriteCombinedXmsbt(Path.Combine(uiFolder, "msg_title.xmsbt"), msgTitleEntries);

            var outputJsonPath = Path.Combine(databaseFolder, "song_data.json");
            File.WriteAllText(outputJsonPath, JsonConvert.SerializeObject(songData, Formatting.Indented), new UTF8Encoding(false));
            _logger.LogInformation("[CSK] Saved single CSK pack: {SavedPath}", outputJsonPath);
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

        private static string GetSeriesIconCopyKey(JObject series)
        {
            return GetString(series, "ui_series_id", GetString(series, "name_id", string.Empty));
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
            CskBuildResources buildResources,
            HashSet<string> copiedSeriesIconKeys)
        {
            //get series name and folder name
            var seriesName = GetString(series, "name_id");
            var realName = GetSeriesDisplayName(series);
            var safeSeriesName = SanitizePathSegment(realName, seriesName, "series folder name");
            var seriesFolderName = SanitizePathSegment($"{packName} - {safeSeriesName}", seriesName, "full series folder name");

            var seriesDbFolder = Path.Combine(outputRoot, seriesFolderName, "database");
            var seriesUiFolder = Path.Combine(outputRoot, seriesFolderName, "ui", "message");
            Directory.CreateDirectory(seriesDbFolder);
            Directory.CreateDirectory(seriesUiFolder);

            //copy icon is not already copied
            var seriesIconKey = GetSeriesIconCopyKey(series);
            if (!copiedSeriesIconKeys.Contains(seriesIconKey) &&
                CopySeriesIcon(series, Path.Combine(outputRoot, seriesFolderName)))
            {
                copiedSeriesIconKeys.Add(seriesIconKey);
            }

            var songData = CreateSongData();
            var msgBgmEntries = new List<string>();
            var msgTitleEntries = new List<string>();
            var metadataBgmIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            //get data for series
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

            //copy nus3bank for core songs
            if (includeAudio)
                CopyCoreVolumeOverrideBankFiles(seriesName, seriesFolderName, outputRoot, generatedBgmFolder, buildResources);

            //write json
            var outputJsonPath = Path.Combine(seriesDbFolder, databaseFileName);
            File.WriteAllText(outputJsonPath, JsonConvert.SerializeObject(songData, Formatting.Indented), new UTF8Encoding(false));
            //write xmsbts
            WriteXmsbt(Path.Combine(seriesUiFolder, "msg_bgm.xmsbt"), msgBgmEntries);
            WriteXmsbt(Path.Combine(seriesUiFolder, "msg_title.xmsbt"), msgTitleEntries);
            return outputJsonPath;
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

            //get series title from override if changed
            if (coreSeriesOverride != null)
            {
                var overrideEntry = coreSeriesOverride[GetString(series, "ui_series_id")] as JObject;
                if (overrideEntry != null)
                    seriesTitle = GetLocalizedString(overrideEntry["msbt_title"], seriesTitle);
            }

            //get sound order
            if (orderOverride != null || !VanillaSeries.Contains(seriesName) || seriesName.StartsWith("etc", StringComparison.OrdinalIgnoreCase))
            {
                var dispOrderSound = GetSeriesSoundOrder(seriesSoundOrder, series);
                //clamp to 127, more is unsupported
                if (dispOrderSound > 127)
                    dispOrderSound = 127;

                GetArray(songData, "series_database_entries").Add(CreateSeriesDatabaseEntry(series, coreSeriesOverride, dispOrderSound));
            }

            msgTitleEntries.Add(MakeEntry($"tit_series_snd_{seriesName}", seriesTitle));
            msgTitleEntries.Add(MakeEntry($"tit_series_{seriesName}", seriesTitle));

            //game processing
            foreach (JObject game in GetArray(series, "games"))
            {
                //skip game for this series if series was changed
                if (coreGameOverride != null)
                {
                    var gameOverride = coreGameOverride[GetString(game, "ui_gametitle_id")] as JObject;
                    if (gameOverride != null && GetString(gameOverride, "ui_series_id") != GetString(series, "ui_series_id"))
                        continue;
                }

                //get game data
                var effectiveGame = GetEffectiveOverrideObject(game, coreGameOverride, "ui_gametitle_id");
                var gameName = GetString(effectiveGame, "name_id", GetString(game, "name_id"));
                if (ShouldAddGameTitleEntry(effectiveGame, seriesName, coreGameSeriesById))
                    AddGameTitleEntry(songData, effectiveGame);

                var gameTitle = GetLocalizedString(effectiveGame["msbt_title"], gameName);
                msgTitleEntries.Add(MakeEntry($"tit_{gameName}", gameTitle));

                //process bgms for this game
                foreach (JObject bgm in GetArray(game, "bgms"))
                    orderCounter = ProcessBgm(bgm, songData, playlistData, msgBgmEntries, coreBgmOverride, orderOverride, seriesName, seriesFolderName, outputRoot, generatedBgmFolder, metadataBgmIds, includeAudio, orderCounter);
            }

            //process bgms from core games that have been moved to this series
            ProcessCoreGameMovedBgms(series, metadata, coreGameOverride, songData, playlistData, msgBgmEntries, msgTitleEntries, coreBgmOverride, orderOverride, seriesName, seriesFolderName, outputRoot, generatedBgmFolder, metadataBgmIds, includeAudio, ref orderCounter);
            //include every unmodified core BGM belonging to this series as well (this is done to ensure that the ordering is always respected)
            AddCoreBgmEntriesForSeries(songData, seriesName, coreBgmOverride, orderOverride, coreGameSeriesById);
            //save vanilla playlists data
            PopulateVanillaPlaylists(songData, seriesName, playlistData, coreBgmIds, coreBgmOverride, orderOverride);
            //save custom playlists data
            if (VanillaSeries.Contains(seriesName))
                PopulateCustomPlaylists(songData, seriesName, playlistData, coreBgmOverride, orderOverride, coreGameSeriesById);
            //add stage entries
            PopulateStageDatabaseEntries(songData, seriesName, stageOverride, playlistData);
            //process overrides for core bgms
            ProcessCoreBgmOverrides(songData, playlistData, msgBgmEntries, msgTitleEntries, seriesName, seriesIdToName, coreBgmOverride, orderOverride, coreGameOverride, ref orderCounter);
            //process overrides for core games moved to new series
            AddCoreGameOverridesForNewSeries(songData, series, seriesName, coreGameOverride);
        }

        #endregion

    }
}
