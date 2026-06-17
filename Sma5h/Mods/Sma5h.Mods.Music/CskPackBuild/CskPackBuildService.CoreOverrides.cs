using Newtonsoft.Json.Linq;
using Sma5h.Mods.Music.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Sma5h.Mods.Music.CskPackBuild
{
    public partial class CskPackBuildService
    {
        #region Moved Core Games

        private void ProcessCoreGameMovedBgms(
            JObject series,
            JObject metadata,
            JObject coreGameOverride,
            JObject songData,
            JObject playlistOverride,
            List<string> msgBgmEntries,
            List<string> msgTitleEntries,
            JObject coreBgmOverride,
            JObject orderOverride,
            string seriesName,
            string seriesFolderName,
            string outputRoot,
            string generatedBgmFolder,
            bool includeAudio,
            ref int orderCounter)
        {
            if (coreGameOverride == null)
                return;

            foreach (var gameMeta in GetCoreGameMovedGames(series, metadata, coreGameOverride))
            {
                var gameTitle = GetLocalizedString(gameMeta["msbt_title"], GetString(gameMeta, "name_id"));
                msgTitleEntries.Add(MakeEntry($"tit_{GetString(gameMeta, "name_id")}", EscapeXml(gameTitle)));

                foreach (JObject bgm in GetArray(gameMeta, "bgms"))
                    orderCounter = ProcessBgm(bgm, songData, playlistOverride, msgBgmEntries, coreBgmOverride, orderOverride, seriesName, seriesFolderName, outputRoot, generatedBgmFolder, includeAudio, orderCounter);
            }
        }

        private IEnumerable<JObject> GetCoreGameMovedGames(JObject series, JObject metadata, JObject coreGameOverride)
        {
            if (coreGameOverride == null)
                yield break;

            foreach (var overrideProperty in coreGameOverride.Properties())
            {
                var overrideEntry = overrideProperty.Value as JObject;
                if (GetString(overrideEntry, "ui_series_id") != GetString(series, "ui_series_id"))
                    continue;

                var movedGame = FindCoreGameMovedGame(metadata, overrideEntry);
                if (movedGame != null)
                    yield return movedGame;
            }
        }

        private JObject FindCoreGameMovedGame(JObject metadata, JObject overrideEntry)
        {
            foreach (JObject metaSeries in GetArray(metadata, "series"))
            {
                foreach (JObject game in GetArray(metaSeries, "games"))
                {
                    if (GetString(game, "ui_gametitle_id") == GetString(overrideEntry, "ui_gametitle_id") &&
                        GetString(game, "ui_series_id") != GetString(overrideEntry, "ui_series_id"))
                    {
                        return game;
                    }
                }
            }

            return null;
        }

        private static string GetCoreBgmSeriesName(
            string uiGameTitleId,
            string dbSeriesId,
            Dictionary<string, string> seriesIdToName,
            JObject coreGameOverride)
        {
            var gameOverride = coreGameOverride != null && !string.IsNullOrEmpty(uiGameTitleId)
                ? coreGameOverride[uiGameTitleId] as JObject
                : null;
            var gameSeriesId = GetString(gameOverride, "ui_series_id");
            var gameSeriesName = GetSeriesNameFromUiSeriesId(gameSeriesId, seriesIdToName);
            if (!string.IsNullOrEmpty(gameSeriesName))
                return gameSeriesName;

            return GetSeriesNameFromUiSeriesId(dbSeriesId, seriesIdToName);
        }

        private static string GetSeriesNameFromUiSeriesId(string uiSeriesId, Dictionary<string, string> seriesIdToName)
        {
            if (string.IsNullOrEmpty(uiSeriesId))
                return null;

            if (seriesIdToName.ContainsKey(uiSeriesId))
                return seriesIdToName[uiSeriesId];

            return uiSeriesId.StartsWith("ui_series_", StringComparison.OrdinalIgnoreCase)
                ? uiSeriesId.Substring("ui_series_".Length)
                : uiSeriesId;
        }

        #endregion

        #region Effective Core Data

        private Dictionary<string, string> BuildCoreGameSeriesById(JObject effectiveCoreGameData)
        {
            var seriesNames = _audioStateService.GetSeriesEntries()
                .Where(p => !string.IsNullOrEmpty(p.UiSeriesId))
                .GroupBy(p => p.UiSeriesId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(p => p.Key, p => p.First().NameId, StringComparer.OrdinalIgnoreCase);

            return _audioStateService.GetGameTitleEntries()
                .Where(p => p.Source == EntrySource.Core && !string.IsNullOrEmpty(p.UiGameTitleId))
                .GroupBy(p => p.UiGameTitleId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    p => p.Key,
                    p =>
                    {
                        var game = effectiveCoreGameData[p.Key] as JObject;
                        var uiSeriesId = GetString(game, "ui_series_id", p.First().UiSeriesId);
                        return GetSeriesNameFromUiSeriesId(uiSeriesId, seriesNames);
                    },
                    StringComparer.OrdinalIgnoreCase);
        }

        private JObject BuildEffectiveCoreGameData(JObject coreGameOverride)
        {
            var gameData = new JObject();

            foreach (var game in _audioStateService.GetGameTitleEntries().Where(p => !string.IsNullOrEmpty(p.UiGameTitleId)))
                gameData[game.UiGameTitleId] = CreateGameObject(game);

            OverlayProperties(gameData, coreGameOverride);
            return gameData;
        }

        private static JObject CreateGameObject(GameTitleEntry game)
        {
            return new JObject
            {
                ["ui_gametitle_id"] = game.UiGameTitleId,
                ["name_id"] = game.NameId,
                ["ui_series_id"] = game.UiSeriesId,
                ["0x1c38302364"] = game.Unk1,
                ["release"] = game.Release,
                ["msbt_title"] = CreateLocalizedObject(game.MSBTTitle)
            };
        }

        private JObject BuildEffectiveCoreBgmOverrideData(JObject coreBgmOverride)
        {
            if (coreBgmOverride == null)
                return null;

            var output = (JObject)coreBgmOverride.DeepClone();
            var dbRoots = EnsureObject(output, "CoreBgmDbRootOverrides");
            var streamSets = EnsureObject(output, "CoreBgmStreamSetOverrides");
            var assignedInfos = EnsureObject(output, "CoreBgmAssignedInfoOverrides");
            var streamProperties = EnsureObject(output, "CoreBgmStreamPropertyOverrides");
            var bgmProperties = EnsureObject(output, "CoreBgmPropertyOverrides");

            var dbRootEntries = _audioStateService.GetBgmDbRootEntries()
                .Where(p => !string.IsNullOrEmpty(p.UiBgmId))
                .ToDictionary(p => p.UiBgmId, p => p, StringComparer.OrdinalIgnoreCase);
            var streamSetEntries = _audioStateService.GetBgmStreamSetEntries()
                .Where(p => !string.IsNullOrEmpty(p.StreamSetId))
                .ToDictionary(p => p.StreamSetId, p => p, StringComparer.OrdinalIgnoreCase);
            var assignedInfoEntries = _audioStateService.GetBgmAssignedInfoEntries()
                .Where(p => !string.IsNullOrEmpty(p.InfoId))
                .ToDictionary(p => p.InfoId, p => p, StringComparer.OrdinalIgnoreCase);
            var streamPropertyEntries = _audioStateService.GetBgmStreamPropertyEntries()
                .Where(p => !string.IsNullOrEmpty(p.StreamId))
                .ToDictionary(p => p.StreamId, p => p, StringComparer.OrdinalIgnoreCase);
            var bgmPropertyEntries = _audioStateService.GetBgmPropertyEntries()
                .Where(p => !string.IsNullOrEmpty(p.NameId))
                .ToDictionary(p => p.NameId, p => p, StringComparer.OrdinalIgnoreCase);

            foreach (var dbProperty in dbRoots.Properties().ToList())
            {
                var uiBgmId = dbProperty.Name;
                if (dbRootEntries.ContainsKey(uiBgmId))
                    dbRoots[uiBgmId] = MergeObjects(CreateBgmDbRootObject(dbRootEntries[uiBgmId]), dbProperty.Value as JObject);

                var db = dbRoots[uiBgmId] as JObject;
                var streamSetId = GetString(db, "stream_set_id");
                if (string.IsNullOrEmpty(streamSetId))
                    continue;

                if (streamSetEntries.ContainsKey(streamSetId))
                    streamSets[streamSetId] = MergeObjects(CreateStreamSetObject(streamSetEntries[streamSetId]), streamSets[streamSetId] as JObject);

                var streamSet = streamSets[streamSetId] as JObject;
                for (var i = 0; i < 16; i++)
                {
                    var infoId = GetString(streamSet, $"info{i}");
                    if (string.IsNullOrEmpty(infoId))
                        continue;

                    if (assignedInfoEntries.ContainsKey(infoId))
                        assignedInfos[infoId] = MergeObjects(CreateAssignedInfoObject(assignedInfoEntries[infoId]), assignedInfos[infoId] as JObject);

                    var assigned = assignedInfos[infoId] as JObject;
                    var streamId = GetString(assigned, "stream_id");
                    if (string.IsNullOrEmpty(streamId))
                        continue;

                    if (streamPropertyEntries.ContainsKey(streamId))
                        streamProperties[streamId] = MergeObjects(CreateStreamPropertyObject(streamPropertyEntries[streamId]), streamProperties[streamId] as JObject);

                    var streamProperty = streamProperties[streamId] as JObject;
                    var nameId = GetString(streamProperty, "data_name0");
                    if (!string.IsNullOrEmpty(nameId) && bgmPropertyEntries.ContainsKey(nameId))
                        bgmProperties[nameId] = MergeObjects(CreateBgmPropertyObject(bgmPropertyEntries[nameId]), bgmProperties[nameId] as JObject);
                }
            }

            return output;
        }

        #endregion

        #region Core BGM Overrides

        private void AddOptionalBgmMessageUnique(List<string> entries, string label, JToken localizedText)
        {
            var text = GetLocalizedString(localizedText);
            AddUniqueMessage(entries, label, text);
        }

        private void ProcessCoreBgmOverrides(
            JObject songData,
            List<string> msgBgmEntries,
            List<string> msgTitleEntries,
            string seriesName,
            Dictionary<string, string> seriesIdToName,
            JObject coreBgmOverride,
            JObject orderOverride,
            JObject coreGameOverride)
        {
            if (coreBgmOverride == null)
                return;

            var alreadyAdded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var dbRoots = coreBgmOverride["CoreBgmDbRootOverrides"] as JObject ?? new JObject();
            var streamSets = coreBgmOverride["CoreBgmStreamSetOverrides"] as JObject ?? new JObject();
            var assignedInfos = coreBgmOverride["CoreBgmAssignedInfoOverrides"] as JObject ?? new JObject();
            var addedAssignedInfos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var dbProperty in dbRoots.Properties())
            {
                var uiBgmId = dbProperty.Name;
                var db = dbProperty.Value as JObject;
                var dbSeriesId = GetString(db, "ui_series_id");
                var uiGameTitleId = GetString(db, "ui_gametitle_id");
                var coreSeries = GetCoreBgmSeriesName(uiGameTitleId, dbSeriesId, seriesIdToName, coreGameOverride);

                if (coreSeries != seriesName)
                    continue;

                var streamSetId = GetString(db, "stream_set_id");
                var streamSetData = streamSets[streamSetId] as JObject ?? new JObject();
                var testDispOrder = orderOverride != null ? GetInt(orderOverride, uiBgmId, GetInt(db, "test_disp_order", 0)) : 0;
                var nameId = GetString(db, "name_id", uiBgmId);

                if (_unavailableBgmNameIds.Value?.Contains(nameId) == true)
                    continue;

                if (HasBgmDatabaseEntry(songData, uiBgmId))
                    continue;

                GetArray(songData, "bgm_database_entries").Add(new JObject
                {
                    ["ui_bgm_id"] = uiBgmId,
                    ["clone_from_ui_bgm_id"] = CloneBgmId,
                    ["stream_set_id"] = streamSetId,
                    ["name_id"] = nameId,
                    ["ui_gametitle_id"] = uiGameTitleId,
                    ["test_disp_order"] = testDispOrder,
                    ["record_type"] = GetString(db, "record_type", "record_original")
                });

                AddUniqueJObjectByKey(songData, "stream_set_entries", "stream_set_id", CreateStreamSetEntry(streamSetData, streamSetId));

                for (var i = 0; i < 16; i++)
                {
                    var infoKey = GetString(streamSetData, $"info{i}");
                    var assigned = assignedInfos[infoKey] as JObject;
                    if (assigned == null)
                        continue;

                    if (addedAssignedInfos.Add(infoKey))
                        AddUniqueJObjectByKey(songData, "assigned_info_entries", "info_id", CreateAssignedInfoEntry(assigned));
                }

                AddOptionalBgmMessageUnique(msgBgmEntries, $"bgm_title_{nameId}", db["msbt_title"]);
                AddOptionalBgmMessageUnique(msgBgmEntries, $"bgm_author_{nameId}", db["msbt_author"]);
                AddOptionalBgmMessageUnique(msgBgmEntries, $"bgm_copyright_{nameId}", db["msbt_copyright"]);

                if (coreGameOverride == null || string.IsNullOrEmpty(uiGameTitleId))
                    continue;

                var game = coreGameOverride[uiGameTitleId] as JObject;
                var gameTitle = GetLocalizedString(game?["msbt_title"]);
                if (!string.IsNullOrEmpty(gameTitle))
                {
                    var entryId = $"tit_{GetString(game, "name_id")}";
                    if (alreadyAdded.Add(entryId))
                        msgTitleEntries.Add(MakeEntry(entryId, EscapeXml(gameTitle)));
                }
            }
        }

        #endregion

    }
}
