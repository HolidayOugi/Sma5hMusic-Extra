using Newtonsoft.Json.Linq;
using Sma5h.Mods.Music.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Sma5h.Mods.Music.CskPackBuild
{
    public partial class CskPackBuildService
    {
        #region Stage Database

        private JObject BuildEffectiveStageData(JObject stageOverride)
        {
            var stageData = new JObject();

            foreach (var stage in _audioStateService.GetStagesEntries().Where(p => !string.IsNullOrEmpty(p.UiStageId)))
                stageData[stage.UiStageId] = CreateStageObject(stage);

            OverlayProperties(stageData, stageOverride);
            return stageData;
        }

        private static JObject CreateStageObject(StageEntry stage)
        {
            return new JObject
            {
                ["ui_stage_id"] = stage.UiStageId,
                ["name_id"] = stage.NameId,
                ["save_no"] = stage.SaveNo,
                ["ui_series_id"] = stage.UiSeriesId,
                ["can_select"] = stage.CanSelect,
                ["disp_order"] = stage.DispOrder,
                ["stage_place_id"] = stage.StagePlaceId,
                ["secret_stage_place_id"] = stage.SecretStagePlaceId,
                ["can_demo"] = stage.CanDemo,
                ["0x10359e17b0"] = stage.Unk1,
                ["is_usable_flag"] = stage.IsUsableFlag,
                ["is_usable_amiibo"] = stage.IsUsableAmiibo,
                ["secret_command_id"] = stage.SecretCommandId,
                ["secret_command_id_joycon"] = stage.SecretCommandIdJoycon,
                ["bgm_set_id"] = stage.BgmSetId,
                ["bgm_setting_no"] = stage.BgmSettingNo,
                ["bgm_selector"] = stage.BgmSelector,
                ["is_dlc"] = stage.IsDlc,
                ["is_patch"] = stage.IsPatch,
                ["dlc_chara_id"] = stage.DlcCharaId
            };
        }

        private void PopulateStageDatabaseEntries(JObject songData, string seriesName, JObject stageOverride, JObject playlistData)
        {
            if (stageOverride == null)
                return;

            if (VanillaSeries.Contains(seriesName))
                PopulateVanillaStageDatabaseEntries(songData, seriesName, stageOverride, playlistData);
            else
                PopulateCustomStageDatabaseEntries(songData, stageOverride);
        }

        private void PopulateCustomStageDatabaseEntries(JObject songData, JObject stageOverride)
        {
            var validPlaylists = SeriesToPlaylist.Values.SelectMany(p => p).ToHashSet(StringComparer.OrdinalIgnoreCase);
            validPlaylists.Add("bgmsmashmenu");

            foreach (var playlistName in ((JObject)songData["playlist_entries"]).Properties().Select(p => p.Name).ToList())
            {
                if (validPlaylists.Contains(playlistName))
                    continue;

                var foundStage = stageOverride.Properties()
                    .FirstOrDefault(p => GetString(p.Value, "bgm_set_id") == playlistName);

                if (foundStage != null)
                {
                    GetArray(songData, "stage_database_entries").Add(new JObject
                    {
                        ["ui_stage_id"] = foundStage.Name,
                        ["bgm_set_id"] = playlistName
                    });
                }
            }
        }

        private void PopulateVanillaStageDatabaseEntries(JObject songData, string seriesName, JObject stageOverride, JObject playlistData)
        {
            var seriesKey = seriesName.ToLowerInvariant();
            if (!SeriesToPlaylist.ContainsKey(seriesKey))
                return;

            var validPlaylists = SeriesToPlaylist[seriesKey];
            var defaultPlaylist = validPlaylists[0];
            var validUiSeries = seriesKey == "etc"
                ? new HashSet<string>(new[]
                {
                    "ui_series_etc", "ui_series_nintendogs", "ui_series_balloonfight",
                    "ui_series_duckhunt", "ui_series_plankton", "ui_series_iceclimber",
                    "ui_series_touch", "ui_series_lightplane", "ui_series_miiplaza",
                    "ui_series_tomodachi", "ui_series_wuhuisland", "ui_series_wreckingcrew"
                }, StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(new[] { $"ui_series_{seriesKey}" }, StringComparer.OrdinalIgnoreCase);

            var stageSeriesOverride = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["ui_stage_kart_circuitfor"] = "mariokart",
                ["ui_stage_kart_circuitx"] = "mariokart"
            };

            foreach (var stageProperty in stageOverride.Properties())
            {
                var stageId = stageProperty.Name;
                var stageData = stageProperty.Value;
                var uiSeriesId = GetString(stageData, "ui_series_id");
                var validPlaylistsStage = validPlaylists;
                var defaultPlaylistStage = defaultPlaylist;
                var validUiSeriesStage = validUiSeries;
                var uiSeriesIdCheck = uiSeriesId;

                if (stageSeriesOverride.ContainsKey(stageId))
                {
                    var forcedSeriesKey = stageSeriesOverride[stageId];
                    if (seriesKey != forcedSeriesKey || !SeriesToPlaylist.ContainsKey(forcedSeriesKey))
                        continue;

                    uiSeriesIdCheck = $"ui_series_{forcedSeriesKey}";
                    validPlaylistsStage = SeriesToPlaylist[forcedSeriesKey];
                    defaultPlaylistStage = validPlaylistsStage[0];
                    validUiSeriesStage = new HashSet<string>(new[] { uiSeriesIdCheck }, StringComparer.OrdinalIgnoreCase);
                }

                if (!validUiSeriesStage.Contains(uiSeriesIdCheck))
                    continue;

                var bgmSetId = GetString(stageData, "bgm_set_id");
                if (string.IsNullOrEmpty(bgmSetId))
                    continue;

                var chosenBgm = validPlaylistsStage.Contains(bgmSetId) || playlistData[bgmSetId] != null
                    ? bgmSetId
                    : defaultPlaylistStage;

                GetArray(songData, "stage_database_entries").Add(new JObject
                {
                    ["ui_stage_id"] = stageId,
                    ["bgm_set_id"] = chosenBgm
                });
            }
        }

        #endregion
    }
}
