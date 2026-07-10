using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Sma5h.Mods.Music.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Sma5h.Mods.Music.CskPackBuild
{
    public partial class CskPackBuildService
    {
        #region Game Entries

        //needed because metadata_mod.json doesn't always store the correct series for core games,
        //need override to check if it's the actual series
        private void AddCoreGameOverridesForNewSeries(JObject songData, JObject series, string seriesName, JObject coreGameOverride)
        {
            if (coreGameOverride == null || VanillaSeries.Contains(seriesName))
                return;

            var expectedUiSeriesId = GetString(series, "ui_series_id");
            foreach (var overrideProperty in coreGameOverride.Properties())
            {
                var overrideEntry = overrideProperty.Value as JObject;
                if (GetString(overrideEntry, "ui_series_id") != expectedUiSeriesId)
                    continue;

                var newEntry = new JObject
                {
                    ["ui_gametitle_id"] = overrideProperty.Name,
                    ["clone_from_gametitle_id"] = CloneGameTitleId,
                    ["name_id"] = GetString(overrideEntry, "name_id"),
                    ["ui_series_id"] = GetString(overrideEntry, "ui_series_id"),
                    ["shown_as_series_in_directory"] = GetBool(overrideEntry, "0x1c38302364", false),
                };

                var entries = GetArray(songData, "gametitle_database_entries");
                if (!entries.Any(p => JToken.DeepEquals(p, newEntry)))
                    entries.Add(newEntry);
            }
        }

        private void AddGameTitleEntry(JObject songData, JObject game)
        {
            GetArray(songData, "gametitle_database_entries").Add(new JObject
            {
                ["ui_gametitle_id"] = GetString(game, "ui_gametitle_id"),
                ["clone_from_gametitle_id"] = CloneGameTitleId,
                ["name_id"] = GetString(game, "name_id"),
                ["ui_series_id"] = GetString(game, "ui_series_id"),
                ["shown_as_series_in_directory"] = GetBool(game, "0x1c38302364", false)
            });
        }

        #endregion

        #region Effective BGM Data

        private JObject BuildEffectiveOrderData(JObject orderOverride)
        {
            var orderData = new JObject();

            foreach (var bgmEntry in _audioStateService.GetBgmDbRootEntries().Where(p => !string.IsNullOrEmpty(p.UiBgmId)))
                orderData[bgmEntry.UiBgmId] = bgmEntry.TestDispOrder;

            OverlayProperties(orderData, orderOverride);
            return orderData;
        }

        private HashSet<string> BuildCoreBgmIds()
        {
            return _audioStateService.GetOriginalCoreBgmDbRootEntries()
                .Where(p => !string.IsNullOrEmpty(p.UiBgmId))
                .Select(p => p.UiBgmId)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        #endregion

        #region Core BGM Entries

        private void AddCoreBgmFromState(JObject songData, string uiBgmId, JObject coreBgmOverride, JObject orderOverride)
        {
            if (string.IsNullOrEmpty(uiBgmId))
                return;

            if (IsCoreBgmOverride(coreBgmOverride, uiBgmId) || HasBgmDatabaseEntry(songData, uiBgmId))
                return;

            var bgmEntries = GetArray(songData, "bgm_database_entries");
            var db = _audioStateService.GetOriginalCoreBgmDbRootEntries()
                .FirstOrDefault(p => string.Equals(p.UiBgmId, uiBgmId, StringComparison.OrdinalIgnoreCase));
            if (db == null)
                return;

            bgmEntries.Add(new JObject
            {
                ["ui_bgm_id"] = db.UiBgmId,
                ["clone_from_ui_bgm_id"] = CloneBgmId,
                ["stream_set_id"] = db.StreamSetId,
                ["name_id"] = db.NameId,
                ["ui_gametitle_id"] = db.UiGameTitleId,
                ["test_disp_order"] = orderOverride != null ? GetInt(orderOverride, uiBgmId, db.TestDispOrder) : db.TestDispOrder,
                ["record_type"] = db.RecordType
            });
        }

        private static bool IsCoreBgmOverride(JObject coreBgmOverride, string uiBgmId)
        {
            if (coreBgmOverride == null || string.IsNullOrEmpty(uiBgmId))
                return false;

            var dbRoots = coreBgmOverride["CoreBgmDbRootOverrides"] as JObject;
            return dbRoots != null && dbRoots[uiBgmId] != null;
        }

        private static bool HasBgmDatabaseEntry(JObject songData, string uiBgmId)
        {
            if (string.IsNullOrEmpty(uiBgmId))
                return false;

            return GetArray(songData, "bgm_database_entries")
                .Any(p => string.Equals(GetString(p, "ui_bgm_id"), uiBgmId, StringComparison.OrdinalIgnoreCase));
        }

        #endregion

        #region BGM Entries

        private static JObject CreateAssignedInfoEntry(JObject assigned)
        {
            return new JObject
            {
                ["info_id"] = GetString(assigned, "info_id"),
                ["stream_id"] = GetString(assigned, "stream_id"),
                ["condition"] = GetString(assigned, "condition"),
                ["condition_process"] = GetString(assigned, "condition_process", "sound_condition_process_add"),
                ["start_frame"] = GetInt(assigned, "start_frame", 0),
                ["change_fadein_frame"] = GetInt(assigned, "change_fadein_frame", 0),
                ["change_start_delay_frame"] = GetInt(assigned, "change_start_delay_frame", 0),
                ["change_fadeout_frame"] = GetInt(assigned, "change_fadeout_frame", 60),
                ["change_stop_delay_frame"] = GetInt(assigned, "change_stop_delay_frame", 0),
                ["menu_change_fadein_frame"] = GetInt(assigned, "menu_change_fadein_frame", 0),
                ["menu_change_start_delay_frame"] = GetInt(assigned, "menu_change_start_delay_frame", 0),
                ["menu_change_fadeout_frame"] = GetInt(assigned, "menu_change_fadeout_frame", 60),
                ["menu_change_stop_delay_frame"] = GetInt(assigned, "menu_change_stop_delay_frame", 0)
            };
        }

        private JObject CreateStreamSetEntry(JObject streamSet, string streamSetId = null)
        {
            var entry = new JObject { ["stream_set_id"] = streamSetId ?? GetString(streamSet, "stream_set_id") };
            for (var i = 0; i < 16; i++)
            {
                var key = $"info{i}";
                var value = GetString(streamSet, key);
                if (!string.IsNullOrEmpty(value))
                    entry[key] = value;
            }

            //avoid crashing in-game if info1 is not set
            var info1 = GetString(streamSet, "info1");
            var specialCategory = GetString(streamSet, "special_category");
            if (!string.IsNullOrWhiteSpace(specialCategory) &&
                (!string.Equals(specialCategory, "sf_situationlink", StringComparison.OrdinalIgnoreCase) || !string.IsNullOrWhiteSpace(info1)))
                entry["special_category"] = specialCategory;

            return entry;
        }

        #endregion

        #region Audio State Objects

        private static JObject CreateBgmDbRootObject(BgmDbRootEntry db)
        {
            return new JObject
            {
                ["ui_bgm_id"] = db.UiBgmId,
                ["stream_set_id"] = db.StreamSetId,
                ["record_type"] = db.RecordType,
                ["ui_gametitle_id"] = db.UiGameTitleId,
                ["name_id"] = db.NameId,
                ["test_disp_order"] = db.TestDispOrder,
                ["msbt_title"] = CreateLocalizedObject(db.Title),
                ["msbt_author"] = CreateLocalizedObject(db.Author),
                ["msbt_copyright"] = CreateLocalizedObject(db.Copyright)
            };
        }

        private static JObject CreateStreamSetObject(BgmStreamSetEntry streamSet)
        {
            var output = new JObject
            {
                ["stream_set_id"] = streamSet.StreamSetId,
                ["special_category"] = streamSet.SpecialCategory
            };

            var infos = new[]
            {
                streamSet.Info0, streamSet.Info1, streamSet.Info2, streamSet.Info3,
                streamSet.Info4, streamSet.Info5, streamSet.Info6, streamSet.Info7,
                streamSet.Info8, streamSet.Info9, streamSet.Info10, streamSet.Info11,
                streamSet.Info12, streamSet.Info13, streamSet.Info14, streamSet.Info15
            };

            for (var i = 0; i < infos.Length; i++)
                output[$"info{i}"] = infos[i];

            return output;
        }

        private static JObject CreateAssignedInfoObject(BgmAssignedInfoEntry assignedInfo)
        {
            return new JObject
            {
                ["info_id"] = assignedInfo.InfoId,
                ["stream_id"] = assignedInfo.StreamId,
                ["condition"] = assignedInfo.Condition,
                ["condition_process"] = assignedInfo.ConditionProcess,
                ["start_frame"] = assignedInfo.StartFrame,
                ["change_fadein_frame"] = assignedInfo.ChangeFadeInFrame,
                ["change_start_delay_frame"] = assignedInfo.ChangeStartDelayFrame,
                ["change_fadeout_frame"] = assignedInfo.ChangeFadoutFrame,
                ["change_stop_delay_frame"] = assignedInfo.ChangeStopDelayFrame,
                ["menu_change_fadein_frame"] = assignedInfo.MenuChangeFadeInFrame,
                ["menu_change_start_delay_frame"] = assignedInfo.MenuChangeStartDelayFrame,
                ["menu_change_fadeout_frame"] = assignedInfo.MenuChangeFadeOutFrame,
                ["menu_change_stop_delay_frame"] = assignedInfo.MenuChangeStopDelayFrame
            };
        }

        private static JObject CreateStreamPropertyObject(BgmStreamPropertyEntry streamProperty)
        {
            return new JObject
            {
                ["stream_id"] = streamProperty.StreamId,
                ["data_name0"] = streamProperty.DataName0,
                ["data_name1"] = streamProperty.DataName1,
                ["data_name2"] = streamProperty.DataName2,
                ["data_name3"] = streamProperty.DataName3,
                ["data_name4"] = streamProperty.DataName4,
                ["loop"] = streamProperty.Loop,
                ["end_point"] = streamProperty.EndPoint,
                ["fadeout_frame"] = streamProperty.FadeOutFrame,
                ["start_point_suddendeath"] = streamProperty.StartPointSuddenDeath,
                ["start_point_transition"] = streamProperty.StartPointTransition,
                ["start_point0"] = streamProperty.StartPoint0,
                ["start_point1"] = streamProperty.StartPoint1,
                ["start_point2"] = streamProperty.StartPoint2,
                ["start_point3"] = streamProperty.StartPoint3,
                ["start_point4"] = streamProperty.StartPoint4
            };
        }

        private static JObject CreateBgmPropertyObject(BgmPropertyEntry bgmProperty)
        {
            return new JObject
            {
                ["name_id"] = bgmProperty.NameId,
                ["loop_start_ms"] = bgmProperty.LoopStartMs,
                ["loop_start_sample"] = bgmProperty.LoopStartSample,
                ["loop_end_ms"] = bgmProperty.LoopEndMs,
                ["loop_end_sample"] = bgmProperty.LoopEndSample,
                ["total_time_ms"] = bgmProperty.TotalTimeMs,
                ["total_samples"] = bgmProperty.TotalSamples
            };
        }

        private static JObject CreateLocalizedObject(Dictionary<string, string> localizedText)
        {
            var output = new JObject();
            if (localizedText == null)
                return output;

            foreach (var entry in localizedText)
                output[entry.Key] = entry.Value;

            return output;
        }

        #endregion

        #region BGM Processing

        private int ProcessBgm(
            JObject bgm,
            JObject songData,
            JObject playlistOverride,
            List<string> msgBgmEntries,
            JObject coreBgmOverride,
            JObject orderOverride,
            string seriesName,
            string seriesFolderName,
            string outputRoot,
            string generatedBgmFolder,
            HashSet<string> metadataBgmIds,
            bool includeAudio,
            int orderCounter)
        {
            var db = bgm["db_root"] as JObject;
            var assigned = bgm["assigned_info"] as JObject;
            var streamProp = bgm["stream_property"] as JObject;
            var bgmProp = bgm["bgm_properties"] as JObject;
            var streamSet = bgm["stream_set"] as JObject;

            var uiBgmId = GetString(db, "ui_bgm_id");
            var nameId = GetString(bgmProp, "name_id");
            if (includeAudio && _unavailableBgmNameIds.Value?.Contains(nameId) == true)
            {
                _logger.LogWarning("[CSK] Excluding unavailable song {NameId} from pack metadata.", nameId);
                return orderCounter;
            }

            //get test disp order for bgm
            var testDispOrder = orderOverride != null ? GetInt(orderOverride, uiBgmId, GetInt(db, "test_disp_order", 0)) : 0;
            var alreadyAdded = HasBgmDatabaseEntry(songData, uiBgmId);
            var alreadyAddedFromMetadata = metadataBgmIds?.Contains(uiBgmId) == true;
            var shouldWriteMetadata = !alreadyAdded || !alreadyAddedFromMetadata;

            //add to json
            if (shouldWriteMetadata)
            {
                AddOrReplaceJObjectByKey(songData, "bgm_database_entries", "ui_bgm_id", new JObject
                {
                    ["ui_bgm_id"] = uiBgmId,
                    ["clone_from_ui_bgm_id"] = CloneBgmId,
                    ["stream_set_id"] = GetString(db, "stream_set_id"),
                    ["name_id"] = nameId,
                    ["ui_gametitle_id"] = GetString(db, "ui_gametitle_id"),
                    ["test_disp_order"] = testDispOrder,
                    ["record_type"] = GetString(db, "record_type", "record_original")
                });

                AddOrReplaceJObjectByKey(songData, "stream_set_entries", "stream_set_id", CreateStreamSetEntry(streamSet));
                AddOrReplaceJObjectByKey(songData, "assigned_info_entries", "info_id", new JObject
                {
                    ["info_id"] = GetString(assigned, "info_id"),
                    ["stream_id"] = GetString(assigned, "stream_id"),
                    ["condition"] = GetString(assigned, "condition"),
                    ["condition_process"] = "sound_condition_process_add",
                    ["change_fadeout_frame"] = 60,
                    ["menu_change_fadeout_frame"] = 60
                });

                AddOrReplaceJObjectByKey(songData, "stream_property_entries", "stream_id", new JObject
                {
                    ["stream_id"] = GetString(streamProp, "stream_id"),
                    ["data_name0"] = GetString(streamProp, "data_name0")
                });

                AddOrReplaceJObjectByKey(songData, "bgm_property_entries", "stream_name", new JObject
                {
                    ["stream_name"] = GetString(streamProp, "data_name0"),
                    ["loop_start_ms"] = GetInt(bgmProp, "loop_start_ms", 0),
                    ["loop_start_sample"] = GetInt(bgmProp, "loop_start_sample", 0),
                    ["loop_end_ms"] = GetInt(bgmProp, "loop_end_ms", 0),
                    ["loop_end_sample"] = GetInt(bgmProp, "loop_end_sample", 0),
                    ["duration_ms"] = GetInt(bgmProp, "total_time_ms", 0),
                    ["duration_sample"] = GetInt(bgmProp, "total_samples", 0)
                });

                var titleText = GetLocalizedString(db["msbt_title"], nameId);
                AddOrReplaceMessage(msgBgmEntries, $"bgm_title_{nameId}", titleText);

                var authorText = GetLocalizedString(db["msbt_author"]);
                AddOrReplaceMessage(msgBgmEntries, $"bgm_author_{nameId}", authorText);

                var copyrightText = GetLocalizedString(db["msbt_copyright"]);
                AddOrReplaceMessage(msgBgmEntries, $"bgm_copyright_{nameId}", copyrightText);

                metadataBgmIds?.Add(uiBgmId);
            }

            //add playlist entries
            orderCounter = AddToPlaylists(uiBgmId, songData, playlistOverride, seriesName, orderCounter);

            //copy audio files
            if (includeAudio)
                CopyBgmFiles(bgm, seriesFolderName, outputRoot, generatedBgmFolder);

            return orderCounter;
        }

        #endregion

    }
}
