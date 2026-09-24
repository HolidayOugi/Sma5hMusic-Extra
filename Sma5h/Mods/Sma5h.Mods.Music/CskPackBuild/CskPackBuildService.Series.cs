using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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
        #region Series Options

        private CskPackSeriesOption CreateSeriesOption(CskModContext context, SeriesEntry series)
        {
            return new CskPackSeriesOption
            {
                Key = CreateSeriesKey(context.Mod, series),
                DisplayName = GetSeriesDisplayName(series),
                NameId = series.NameId,
                UiSeriesId = series.UiSeriesId,
                ModName = context.Mod.Name
            };
        }

        private static string CreateSeriesKey(IMusicMod mod, SeriesEntry series)
        {
            return $"{CreateModKey(mod)}|{series.UiSeriesId}|{series.NameId}";
        }

        private static string CreateModKey(IMusicMod mod)
        {
            return Path.GetFullPath(mod.ModPath);
        }

        private string GetSeriesDisplayName(SeriesEntry series)
        {
            return GetLocalizedString(series.MSBTTitle, series.NameId);
        }

        #endregion

        #region Sound Order

        private Dictionary<string, int> BuildSeriesSoundOrder(IEnumerable<SeriesEntry> contextSeries, CskBuildState state)
        {
            var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            //get order from the audio state
            var sortedGames = state.BgmDbRoots.Values
                .Where(db => !string.IsNullOrEmpty(db.UiGameTitleId) && db.TestDispOrder >= 0)
                .OrderBy(db => db.TestDispOrder)
                .GroupBy(db => db.UiGameTitleId, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.Key);
            var index = GetStartingOrderForSeries();
            foreach (var gameId in sortedGames)
            {
                if (!state.Games.TryGetValue(gameId, out var game) || !state.Series.TryGetValue(game.UiSeriesId ?? string.Empty, out var series) ||
                    series.DispOrderSound < 0 || result.ContainsKey(series.UiSeriesId))
                    continue;
                SetSeriesOrder(result, series, index);
                if (index < sbyte.MaxValue)
                    index++;
            }

            var firstCustomOrder = 39;
            foreach (var series in contextSeries
                .Where(series => !VanillaSeries.Contains(series.NameId))
                .OrderBy(GetSeriesDisplayName, StringComparer.OrdinalIgnoreCase))
            {
                if (!result.ContainsKey(series.UiSeriesId))
                    SetSeriesOrder(result, series, firstCustomOrder++);
            }
            return result;
        }

        private static void SetSeriesOrder(Dictionary<string, int> order, SeriesEntry series, int value)
        {
            if (!string.IsNullOrEmpty(series.UiSeriesId) && !order.ContainsKey(series.UiSeriesId))
                order[series.UiSeriesId] = value;
            if (!string.IsNullOrEmpty(series.NameId) && !order.ContainsKey(series.NameId))
                order[series.NameId] = value;
        }

        private int GetSeriesSoundOrder(Dictionary<string, int> order, SeriesEntry series)
        {
            if (!string.IsNullOrEmpty(series.UiSeriesId) && order.TryGetValue(series.UiSeriesId, out var value))
                return value;
            if (!string.IsNullOrEmpty(series.NameId) && order.TryGetValue(series.NameId, out value))
                return value;
            return GetStartingOrderForSeries();
        }

        private int GetStartingOrderForSeries()
        {
            return Math.Clamp(_config.CurrentValue.Sma5hMusicGUI?.StartingOrderForSeries ?? 1, 0, 39);
        }

        #endregion

        #region Series Entries

        private static JObject CreateSeriesDatabaseEntry(SeriesEntry series, int dispOrderSound)
        {
            var isDlc = MusicConstants.DLC_SERIES.Contains(series.UiSeriesId, StringComparer.OrdinalIgnoreCase);
            var output = new JObject
            {
                ["ui_series_id"] = series.UiSeriesId, ["clone_from_series_id"] = CloneSeriesId,
                ["name_id"] = series.NameId, ["disp_order"] = series.DispOrder,
                ["disp_order_sound"] = dispOrderSound, ["save_no"] = series.SaveNo,
                ["shown_as_series_in_directory"] = series.Unk1,
                ["is_dlc"] = series.IsDlc || isDlc, ["is_patch"] = series.IsPatch || isDlc,
                ["is_use_amiibo_bg"] = series.IsUseAmiiboBg
            };
            if (!string.IsNullOrEmpty(series.DlcCharaId))
                output["dlc_chara_id"] = series.DlcCharaId;
            return output;
        }

        private void GenerateSeriesOrderPack(List<CskModContext> contexts, string outputRoot, HashSet<string> selectedKeys, Dictionary<string, int> order, CskBuildState state)
        {
            var selectedIds = GetSelectedSeriesIds(contexts, selectedKeys);
            var entries = state.Series.Values
                .Where(series => VanillaSeries.Contains(series.NameId) && !selectedIds.Contains(series.UiSeriesId))
                .Select(series => new { Series = series, Order = Math.Min(GetSeriesSoundOrder(order, series), 127) })
                .Where(item =>
                    !MusicConstants.DEFAULT_SERIES_DISP_ORDER_SOUND.TryGetValue(item.Series.UiSeriesId, out var defaultOrder) ||
                    !MusicConstants.DEFAULT_SERIES_SHOWN_AS_SERIES_IN_DIRECTORY.TryGetValue(item.Series.UiSeriesId, out var defaultShown) ||
                    item.Order != defaultOrder || item.Series.Unk1 != defaultShown)
                .OrderBy(item => item.Order)
                .ThenBy(item => item.Series.NameId, StringComparer.OrdinalIgnoreCase)
                .Select(item => CreateSeriesDatabaseEntry(item.Series, item.Order))
                .ToList();
            if (entries.Count == 0)
                return;
            var folderName = contexts.Count > 1 ? "CSK Packs - Series Order" : SanitizePathSegment(
                $"{contexts.Select(context => context.SafePackName).FirstOrDefault(name => !string.IsNullOrWhiteSpace(name)) ?? SinglePackFolderName} - Series Order",
                "Series Order", "series order folder name");
            var databaseFolder = Path.Combine(outputRoot, folderName, "database");
            Directory.CreateDirectory(databaseFolder);
            var json = new JObject { ["series_database_entries"] = new JArray(entries) };
            var output = Path.Combine(databaseFolder, "series_order.json");
            File.WriteAllText(output, JsonConvert.SerializeObject(json, Formatting.Indented), new UTF8Encoding(false));
            _logger.LogInformation("[CSK] Saved series order pack: {SavedPath}", output);
        }

        private void AddSeriesOrderEntries(JObject songData, IEnumerable<JObject> entries)
        {
            foreach (var entry in entries)
                GetArray(songData, "series_database_entries").Add(entry);
        }

        private IEnumerable<JObject> CreateVanillaSeriesOrderEntries(List<CskModContext> contexts, HashSet<string> selectedKeys, Dictionary<string, int> order, CskBuildState state)
        {
            //get all series selected
            var selectedIds = GetSelectedSeriesIds(contexts, selectedKeys);
            //get all vanilla series that are not selected
            return state.Series.Values
                .Where(series => VanillaSeries.Contains(series.NameId) && !selectedIds.Contains(series.UiSeriesId))
                .Select(series => new { Series = series, Order = Math.Min(GetSeriesSoundOrder(order, series), 127) })
                .Where(item =>
                    //create series entries only when their sound order and shown as series in directory differ from the vanilla defaults
                    !MusicConstants.DEFAULT_SERIES_DISP_ORDER_SOUND.TryGetValue(item.Series.UiSeriesId, out var defaultOrder) ||
                    !MusicConstants.DEFAULT_SERIES_SHOWN_AS_SERIES_IN_DIRECTORY.TryGetValue(item.Series.UiSeriesId, out var defaultShown) ||
                    item.Order != defaultOrder || item.Series.Unk1 != defaultShown)
                .OrderBy(item => item.Order)
                .ThenBy(item => item.Series.NameId, StringComparer.OrdinalIgnoreCase)
                .Select(item => CreateSeriesDatabaseEntry(item.Series, item.Order));
        }

        private HashSet<string> GetSelectedSeriesIds(IEnumerable<CskModContext> contexts, HashSet<string> selectedKeys)
        {
            return contexts.SelectMany(context => context.SeriesList
                    .Where(series => selectedKeys.Contains(CreateSeriesKey(context.Mod, series)))
                    .Select(series => series.UiSeriesId))
                .Where(id => !string.IsNullOrEmpty(id))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        private HashSet<string> GetSelectedSeriesNames(IEnumerable<CskModContext> contexts, HashSet<string> selectedKeys)
        {
            return contexts.SelectMany(context => context.SeriesList
                    .Where(series => selectedKeys.Contains(CreateSeriesKey(context.Mod, series)))
                    .Select(series => series.NameId))
                .Where(name => !string.IsNullOrEmpty(name))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        private HashSet<string> GetSelectedGameTitleIds(IEnumerable<CskModContext> contexts, HashSet<string> selectedKeys, CskBuildState state)
        {
            var selectedSeries = GetSelectedSeriesIds(contexts, selectedKeys);
            return state.Games.Values
                .Where(game => selectedSeries.Contains(game.UiSeriesId))
                .Select(game => game.UiGameTitleId)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        #endregion

        #region Generation

        private void GenerateVanillaSongsChangesPack(List<CskModContext> contexts, string outputRoot, HashSet<string> selectedKeys, string generatedBgmFolder, CskBuildState state, bool includeAudio)
        {
            var folderName = contexts.Count == 1
                ? SanitizePathSegment($"{contexts[0].SafePackName} - Vanilla Songs Changes", "Vanilla Songs Changes", "vanilla songs changes folder name")
                : "CSK Packs - Vanilla Songs Changes";
            var packRoot = Path.Combine(outputRoot, folderName);
            var songData = new JObject { ["bgm_database_entries"] = new JArray() };
            var bgmMessages = new List<string>();
            var titleMessages = new List<string>();
            if (!AddVanillaSongsChanges(contexts, selectedKeys, songData, bgmMessages, titleMessages, packRoot, generatedBgmFolder, state, includeAudio))
                return;
            if (songData["stage_database_entries"] is JArray stageEntries && stageEntries.Count == 0)
                songData.Remove("stage_database_entries");
            if (GetArray(songData, "bgm_database_entries").Count > 0)
            {
                var database = Path.Combine(packRoot, "database");
                Directory.CreateDirectory(database);
                File.WriteAllText(Path.Combine(database, "database.json"), JsonConvert.SerializeObject(songData, Formatting.Indented), new UTF8Encoding(false));
            }
            if (bgmMessages.Count > 0 || titleMessages.Count > 0)
            {
                var messages = Path.Combine(packRoot, "ui", "message");
                Directory.CreateDirectory(messages);
                if (bgmMessages.Count > 0) WriteCombinedXmsbt(Path.Combine(messages, "msg_bgm.xmsbt"), bgmMessages);
                if (titleMessages.Count > 0) WriteCombinedXmsbt(Path.Combine(messages, "msg_title.xmsbt"), titleMessages);
            }
            _logger.LogInformation("[CSK] Saved vanilla songs changes pack: {SavedPath}", packRoot);
        }

        private bool AddVanillaSongsChanges(
            List<CskModContext> contexts,
            HashSet<string> selectedKeys,
            JObject songData,
            List<string> bgmMessages,
            List<string> titleMessages,
            string packRoot,
            string generatedBgmFolder,
            CskBuildState state,
            bool includeAudio)
        {
            var before = GetArray(songData, "bgm_database_entries").Count + GetArray(songData, "stage_database_entries").Count + bgmMessages.Count + titleMessages.Count;
            var selectedNames = GetSelectedSeriesNames(contexts, selectedKeys);
            //populate playlists both vanilla and custom
            foreach (var name in VanillaSeries.Where(name => !selectedNames.Contains(name)))
                PopulateVanillaPlaylists(songData, name, state, true);
            foreach (var name in state.CoreGameSeriesById.Values.Where(name => !string.IsNullOrEmpty(name) && !selectedNames.Contains(name)).Distinct(StringComparer.OrdinalIgnoreCase))
                PopulateCustomPlaylists(songData, name, state);
            PopulateCustomStageDatabaseEntries(songData, state, GetSelectedSeriesIds(contexts, selectedKeys));
            if (GetArray(songData, "stage_database_entries").Count == 0)
                songData.Remove("stage_database_entries");

            //get all games already covered by the selected series
            var selectedGames = GetSelectedGameTitleIds(contexts, selectedKeys, state);
            foreach (var bgmId in state.OverriddenCoreBgmIds)
            {
                if (!state.BgmDbRoots.TryGetValue(bgmId, out var db) || selectedGames.Contains(db.UiGameTitleId) ||
                    string.IsNullOrEmpty(db.NameId) || _unavailableBgmNameIds.Value?.Contains(db.NameId) == true)
                    continue;
                GetArray(songData, "bgm_database_entries").Add(new JObject
                {
                    ["ui_bgm_id"] = db.UiBgmId, ["clone_from_ui_bgm_id"] = CloneBgmId,
                    ["stream_set_id"] = db.StreamSetId, ["name_id"] = db.NameId,
                    ["ui_gametitle_id"] = db.UiGameTitleId,
                    ["test_disp_order"] = db.TestDispOrder <= 4 ? db.TestDispOrder : db.MenuValue,
                    ["record_type"] = string.IsNullOrEmpty(db.RecordType) ? "record_original" : db.RecordType
                });
                if (state.Games.TryGetValue(db.UiGameTitleId ?? string.Empty, out var game))
                {
                    var gameTitle = GetLocalizedString(game.MSBTTitle);
                    if (!string.IsNullOrEmpty(gameTitle))
                        AddUniqueMessage(titleMessages, $"tit_{game.NameId}", gameTitle);
                    //add game title entry if custom
                    if (game.Source != EntrySource.Core && !HasEntry(songData, "gametitle_database_entries", "ui_gametitle_id", game.UiGameTitleId))
                        GetArray(songData, "gametitle_database_entries").Add(CreateGameEntry(game));
                }
            }

            foreach (var bgmId in state.OverriddenCoreBgmIds)
            {
                if (!state.BgmDbRoots.TryGetValue(bgmId, out var db) || selectedGames.Contains(db.UiGameTitleId))
                    continue;
                AddCoreBgmTextChanges(db, bgmMessages);
            }

            //write nus3bank for volume overrides
            var copiedAudio = false;
            if (includeAudio && !string.IsNullOrEmpty(generatedBgmFolder))
            {
                var destination = Path.Combine(packRoot, "stream;", "sound", "bgm");
                foreach (var entry in state.CoreVolumeChanges.Where(entry =>
                {
                    var db = FindOriginalCoreDbRootByNameId(entry.NameId, state);
                    return db != null && !selectedGames.Contains(db.UiGameTitleId);
                }))
                {
                    var source = Path.Combine(generatedBgmFolder, $"bgm_{entry.NameId}.nus3bank");
                    if (!File.Exists(source))
                        _nus3AudioService.GenerateNus3Bank(entry.NameId, entry.Volume, source);
                    CopyIfExists(source, Path.Combine(destination, Path.GetFileName(source)));
                    copiedAudio = true;
                }
            }
            var after = GetArray(songData, "bgm_database_entries").Count + GetArray(songData, "stage_database_entries").Count + bgmMessages.Count + titleMessages.Count;
            return after > before || copiedAudio;
        }

        #endregion
    }
}
