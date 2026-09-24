using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Sma5h.Mods.Music.Helpers;
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
        private void GenerateCskPacks(List<CskModContext> contexts, string generatedBgmFolder, string outputRoot, HashSet<string> selectedKeys, CskBuildState state, bool includeAudio)
        {
            //build sound order for music select menu
            var order = BuildSeriesSoundOrder(contexts.SelectMany(context => context.SeriesList), state);
            var fileCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var copiedIcons = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var context in contexts)
            {
                _logger.LogInformation("Generating CSK packs for mod {ModName}", context.Mod.Name);
                foreach (var series in context.SeriesList.Where(series => selectedKeys.Contains(CreateSeriesKey(context.Mod, series))))
                {
                    var fileBase = SanitizePathSegment(series.NameId, "series", "series database file name");
                    //if multiple packs for same series, append a number to the file name
                    fileCounts.TryGetValue(fileBase, out var count);
                    fileCounts[fileBase] = ++count;
                    var fileName = count == 1 ? $"{fileBase}.json" : $"{fileBase}{count}.json";
                    //process individual series
                    var saved = ProcessSeries(context, series, context.SafePackName, fileName, outputRoot, generatedBgmFolder, order, state, includeAudio, copiedIcons);
                    _logger.LogInformation("[CSK] Saved {SeriesName}: {SavedPath}", series.NameId, saved);
                }
            }
            //generates series order pack for vanilla series not included
            GenerateSeriesOrderPack(contexts, outputRoot, selectedKeys, order, state);
            //generates pack with vanilla song changes for non-selected series
            GenerateVanillaSongsChangesPack(contexts, outputRoot, selectedKeys, generatedBgmFolder, state, includeAudio);
        }

        //generation of one pack for each selected mod
        private void GenerateCskPacksByMod(List<CskModContext> contexts, string generatedBgmFolder, string outputRoot, HashSet<string> selectedKeys, CskBuildState state, bool includeAudio)
        {
            var order = BuildSeriesSoundOrder(contexts.SelectMany(context => context.SeriesList), state);
            foreach (var context in contexts.Where(context => context.SeriesList.Any(series => selectedKeys.Contains(CreateSeriesKey(context.Mod, series)))))
            {
                _logger.LogInformation("Generating CSK pack for mod {ModName}", context.Mod.Name);
                var packFolder = SanitizePathSegment(context.Mod.Name, context.SafePackName, "mod pack folder name");
                var packRoot = Path.Combine(outputRoot, packFolder);
                var database = Path.Combine(packRoot, "database");
                var messages = Path.Combine(packRoot, "ui", "message");
                Directory.CreateDirectory(database);
                Directory.CreateDirectory(messages);
                var songData = CreateSongData();
                var bgmMessages = new List<string>();
                var titleMessages = new List<string>();
                var writtenModBgms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var series in context.SeriesList.Where(series => selectedKeys.Contains(CreateSeriesKey(context.Mod, series))))
                {
                    _logger.LogInformation("[CSK] Adding {SeriesName} to mod pack {ModName}.", series.NameId, context.Mod.Name);
                    CopySeriesIcon(series, packRoot);
                    PopulateSeriesPackData(context, series, songData, bgmMessages, titleMessages, packFolder, outputRoot, generatedBgmFolder, order, writtenModBgms, includeAudio, state);
                    if (includeAudio)
                        CopyCoreVolumeBanks(series.NameId, packFolder, outputRoot, generatedBgmFolder, state);
                }
                NormalizeCombinedSongData(songData);
                WriteCombinedXmsbt(Path.Combine(messages, "msg_bgm.xmsbt"), bgmMessages);
                WriteCombinedXmsbt(Path.Combine(messages, "msg_title.xmsbt"), titleMessages);
                var fileBase = SanitizePathSegment(context.Mod.Name.Replace(' ', '_').ToLowerInvariant(), context.SafePackName.Replace(' ', '_').ToLowerInvariant(), "mod database file name");
                var output = Path.Combine(database, $"{fileBase}.json");
                File.WriteAllText(output, JsonConvert.SerializeObject(songData, Formatting.Indented), new UTF8Encoding(false));
                _logger.LogInformation("[CSK] Saved mod CSK pack: {SavedPath}", output);
            }
            //generates series order pack for vanilla series not included
            GenerateSeriesOrderPack(contexts, outputRoot, selectedKeys, order, state);
            //generates pack with vanilla song changes for non-selected series
            GenerateVanillaSongsChangesPack(contexts, outputRoot, selectedKeys, generatedBgmFolder, state, includeAudio);
        }

        //generation of a single pack for all series
        private void GenerateSingleCskPack(List<CskModContext> contexts, string generatedBgmFolder, string outputRoot, HashSet<string> selectedKeys, CskBuildState state, bool includeAudio)
        {
            var selected = contexts.SelectMany(context => context.SeriesList
                    .Where(series => selectedKeys.Contains(CreateSeriesKey(context.Mod, series)))
                    .Select(series => (Context: context, Series: series)))
                .ToList();
            if (selected.Count == 0)
                throw new InvalidOperationException("No selected series were found in the currently loaded music mods.");
            var order = BuildSeriesSoundOrder(contexts.SelectMany(context => context.SeriesList), state);
            var packFolder = GetSingleCskPackOutputFolderName(contexts);
            var packRoot = string.IsNullOrEmpty(packFolder) ? outputRoot : Path.Combine(outputRoot, packFolder);
            var database = Path.Combine(packRoot, "database");
            var messages = Path.Combine(packRoot, "ui", "message");
            Directory.CreateDirectory(database);
            Directory.CreateDirectory(messages);
            var songData = CreateSongData();
            var bgmMessages = new List<string>();
            var titleMessages = new List<string>();
            var writtenModBgms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            //build the data for each series
            foreach (var item in selected)
            {
                _logger.LogInformation("[CSK] Adding {SeriesName} to single CSK pack.", item.Series.NameId);
                CopySeriesIcon(item.Series, packRoot);
                PopulateSeriesPackData(item.Context, item.Series, songData, bgmMessages, titleMessages, packFolder, outputRoot, generatedBgmFolder, order, writtenModBgms, includeAudio, state);
                if (includeAudio)
                    //copy nus3bank files for core volume overrides for this series
                    CopyCoreVolumeBanks(item.Series.NameId, packFolder, outputRoot, generatedBgmFolder, state);
            }
            //adds vanilla series entries for sound order
            AddSeriesOrderEntries(songData, CreateVanillaSeriesOrderEntries(contexts, selectedKeys, order, state));
            //adds vanilla song changes for non-selected series
            AddVanillaSongsChanges(contexts, selectedKeys, songData, bgmMessages, titleMessages, packRoot, generatedBgmFolder, state, includeAudio);
            //remove duplicates
            NormalizeCombinedSongData(songData);
            WriteCombinedXmsbt(Path.Combine(messages, "msg_bgm.xmsbt"), bgmMessages);
            WriteCombinedXmsbt(Path.Combine(messages, "msg_title.xmsbt"), titleMessages);
            var output = Path.Combine(database, "song_data.json");
            File.WriteAllText(output, JsonConvert.SerializeObject(songData, Formatting.Indented), new UTF8Encoding(false));
            _logger.LogInformation("[CSK] Saved single CSK pack: {SavedPath}", output);
        }

        #endregion

        #region Pack Helpers

        private string GetSingleCskPackOutputFolderName(IReadOnlyList<CskModContext> contexts)
        {
            if (_config.CurrentValue.Sma5hMusicGUI?.SaveOutputToSubfolder == false)
                return string.Empty;
            return contexts.Count == 1 && !string.IsNullOrWhiteSpace(contexts[0].SafePackName) ? contexts[0].SafePackName : SinglePackFolderName;
        }

        #endregion

        #region Series Processing

        private string ProcessSeries(
            CskModContext context,
            SeriesEntry series,
            string packName,
            string databaseFileName,
            string outputRoot,
            string generatedBgmFolder,
            Dictionary<string, int> order,
            CskBuildState state,
            bool includeAudio,
            HashSet<string> copiedIcons)
        {
            //get series name and folder name
            var safeName = SanitizePathSegment(GetSeriesDisplayName(series), series.NameId, "series folder name");
            var folderName = SanitizePathSegment($"{packName} - {safeName}", series.NameId, "full series folder name");
            var database = Path.Combine(outputRoot, folderName, "database");
            var messages = Path.Combine(outputRoot, folderName, "ui", "message");
            Directory.CreateDirectory(database);
            Directory.CreateDirectory(messages);
            //copy icon is not already copied
            if (copiedIcons.Add(series.UiSeriesId ?? series.NameId))
                CopySeriesIcon(series, Path.Combine(outputRoot, folderName));
            var songData = CreateSongData();
            var bgmMessages = new List<string>();
            var titleMessages = new List<string>();
            //get data for series
            PopulateSeriesPackData(context, series, songData, bgmMessages, titleMessages, folderName, outputRoot, generatedBgmFolder, order,
                new HashSet<string>(StringComparer.OrdinalIgnoreCase), includeAudio, state);
            //copy nus3bank for core songs
            if (includeAudio)
                CopyCoreVolumeBanks(series.NameId, folderName, outputRoot, generatedBgmFolder, state);
            var output = Path.Combine(database, databaseFileName);
            //write json
            File.WriteAllText(output, JsonConvert.SerializeObject(songData, Formatting.Indented), new UTF8Encoding(false));
            //write xmsbts
            WriteXmsbt(Path.Combine(messages, "msg_bgm.xmsbt"), bgmMessages);
            WriteXmsbt(Path.Combine(messages, "msg_title.xmsbt"), titleMessages);
            return output;
        }

        private void PopulateSeriesPackData(
            CskModContext context,
            SeriesEntry series,
            JObject songData,
            List<string> bgmMessages,
            List<string> titleMessages,
            string packFolder,
            string outputRoot,
            string generatedBgmFolder,
            Dictionary<string, int> order,
            HashSet<string> writtenModBgms,
            bool includeAudio,
            CskBuildState state)
        {
            var orderCounter = GetNextPlaylistOrder(series.NameId, state);
            //get sound order
            var dispOrderSound = Math.Min(GetSeriesSoundOrder(order, series), 127);
            if (!VanillaSeries.Contains(series.NameId) ||
                !MusicConstants.DEFAULT_SERIES_DISP_ORDER_SOUND.TryGetValue(series.UiSeriesId, out var defaultOrder) ||
                !MusicConstants.DEFAULT_SERIES_SHOWN_AS_SERIES_IN_DIRECTORY.TryGetValue(series.UiSeriesId, out var defaultShown) ||
                dispOrderSound != defaultOrder || series.Unk1 != defaultShown)
                GetArray(songData, "series_database_entries").Add(CreateSeriesDatabaseEntry(series, dispOrderSound));
            var seriesTitle = GetLocalizedString(series.MSBTTitle, series.NameId);
            titleMessages.Add(MakeEntry($"tit_series_snd_{series.NameId}", seriesTitle));
            titleMessages.Add(MakeEntry($"tit_series_{series.NameId}", seriesTitle));

            //game processing
            foreach (var item in GetContextGamesForSeries(context, series.UiSeriesId, state))
            {
                //get game data
                var game = item.Game;
                if (!state.CoreGameSeriesById.TryGetValue(game.UiGameTitleId, out var coreSeriesName) ||
                    !string.Equals(coreSeriesName, series.NameId, StringComparison.OrdinalIgnoreCase))
                    if (!HasEntry(songData, "gametitle_database_entries", "ui_gametitle_id", game.UiGameTitleId))
                        GetArray(songData, "gametitle_database_entries").Add(CreateGameEntry(game));
                titleMessages.Add(MakeEntry($"tit_{game.NameId}", GetLocalizedString(game.MSBTTitle, game.NameId)));
                //process bgms for this game
                foreach (var bgm in item.Bgms)
                {
                    var database = state.BgmDbRoots.TryGetValue(bgm.Database.UiBgmId, out var current)
                        ? current
                        : bgm.Database;
                    orderCounter = AddBgmToPack(database, songData, bgmMessages, series.NameId, packFolder, outputRoot,
                        generatedBgmFolder, writtenModBgms, includeAudio, false, true, bgm, orderCounter, state);
                }
            }

            //include every unmodified core BGM belonging to this series as well (this is done to ensure that the ordering is always respected)
            foreach (var original in state.OriginalBgmDbRoots.Values.Where(db =>
                state.Games.TryGetValue(db.UiGameTitleId ?? string.Empty, out var game) &&
                string.Equals(game.UiSeriesId, series.UiSeriesId, StringComparison.OrdinalIgnoreCase)))
            {
                if (state.BgmDbRoots.TryGetValue(original.UiBgmId, out var current) && current.TestDispOrder == -1)
                    continue;
                AddOriginalCoreBgm(songData, original.UiBgmId, state);
            }

            //save vanilla playlists data
            PopulateVanillaPlaylists(songData, series.NameId, state);
            //save custom playlists data
            if (VanillaSeries.Contains(series.NameId))
                PopulateCustomPlaylists(songData, series.NameId, state);
            //add stage entries
            PopulateStageDatabaseEntries(songData, series.NameId, state);

            //process overrides for core bgms
            var overrideGameTitles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var bgmId in state.OverriddenCoreBgmIds)
            {
                if (!state.BgmDbRoots.TryGetValue(bgmId, out var db) || !state.Games.TryGetValue(db.UiGameTitleId ?? string.Empty, out var game) ||
                    !string.Equals(game.UiSeriesId, series.UiSeriesId, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (HasEntry(songData, "bgm_database_entries", "ui_bgm_id", bgmId))
                    continue;
                orderCounter = AddBgmToPack(db, songData, bgmMessages, series.NameId, packFolder, outputRoot, generatedBgmFolder,
                        writtenModBgms, includeAudio, true, false, null, orderCounter, state);
                var gameTitle = GetLocalizedString(game.MSBTTitle);
                if (overrideGameTitles.Add(game.NameId) && !string.IsNullOrEmpty(gameTitle))
                    titleMessages.Add(MakeEntry($"tit_{game.NameId}", gameTitle));
            }

            foreach (var bgmId in state.OverriddenCoreBgmIds)
            {
                if (!state.BgmDbRoots.TryGetValue(bgmId, out var db) ||
                    !state.Games.TryGetValue(db.UiGameTitleId ?? string.Empty, out var game) ||
                    !string.Equals(game.UiSeriesId, series.UiSeriesId, StringComparison.OrdinalIgnoreCase))
                    continue;
                AddCoreBgmTextChanges(db, bgmMessages);
            }

            if (!VanillaSeries.Contains(series.NameId))
            {
                foreach (var game in state.Games.Values.Where(game => string.Equals(game.UiSeriesId, series.UiSeriesId, StringComparison.OrdinalIgnoreCase)))
                    if (!HasEntry(songData, "gametitle_database_entries", "ui_gametitle_id", game.UiGameTitleId))
                        GetArray(songData, "gametitle_database_entries").Add(CreateGameEntry(game));
            }
        }

        #endregion
    }
}
