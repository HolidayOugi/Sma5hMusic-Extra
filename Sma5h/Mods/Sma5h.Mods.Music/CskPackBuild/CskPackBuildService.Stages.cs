using Newtonsoft.Json.Linq;
using Sma5h.Mods.Music.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Sma5h.Mods.Music.CskPackBuild
{
    public partial class CskPackBuildService
    {
        #region Stage Database

        private void PopulateStageDatabaseEntries(JObject songData, string seriesName, CskBuildState state)
        {
            //populate stage database
            if (VanillaSeries.Contains(seriesName))
                PopulateVanillaStageDatabaseEntries(songData, seriesName, state);
            else
                PopulateCustomStageDatabaseEntries(songData, state);
        }

        private void PopulateCustomStageDatabaseEntries(JObject songData, CskBuildState state, HashSet<string> excludedSeriesIds = null)
        {
            //get playlists
            if (songData["playlist_entries"] is not JObject playlists)
                return;
            var vanillaPlaylists = SeriesToPlaylist.Values.SelectMany(ids => ids).Concat(VanillaNonSeriesPlaylists).ToHashSet(StringComparer.OrdinalIgnoreCase);
            //for each playlist, assign all of its stages
            foreach (var playlistId in playlists.Properties().Select(property => property.Name).Where(id => !vanillaPlaylists.Contains(id)).ToList())
            {
                //exclude any stages that are part of the excluded series
                foreach (var stage in state.Stages.Where(stage =>
                    string.Equals(stage.BgmSetId, playlistId, StringComparison.OrdinalIgnoreCase) &&
                    excludedSeriesIds?.Contains(stage.UiSeriesId) != true))
                {
                    AddStageEntryIfChanged(songData, stage.UiStageId, playlistId, stage.BgmSettingNo);
                }
            }
        }

        private void PopulateVanillaStageDatabaseEntries(JObject songData, string seriesName, CskBuildState state)
        {
            //get playlists for this series
            if (!SeriesToPlaylist.TryGetValue(seriesName, out var validPlaylists))
                return;
            var validSeries = string.Equals(seriesName, "etc", StringComparison.OrdinalIgnoreCase)
                ? new HashSet<string>(new[]
                {
                    "ui_series_etc", "ui_series_nintendogs", "ui_series_balloonfight", "ui_series_duckhunt",
                    "ui_series_plankton", "ui_series_iceclimber", "ui_series_touch", "ui_series_lightplane",
                    "ui_series_miiplaza", "ui_series_tomodachi", "ui_series_wuhuisland", "ui_series_wreckingcrew"
                }, StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(new[] { $"ui_series_{seriesName}" }, StringComparer.OrdinalIgnoreCase);

            foreach (var stage in state.Stages)
            {
                var stagePlaylists = validPlaylists;
                var stageSeries = validSeries;
                //mariokart stage is in mario playlist, need override
                if (stage.UiStageId is "ui_stage_kart_circuitfor" or "ui_stage_kart_circuitx")
                {
                    //mario kart case
                    if (!string.Equals(seriesName, "mariokart", StringComparison.OrdinalIgnoreCase))
                        continue;
                    stagePlaylists = SeriesToPlaylist["mariokart"];
                    stageSeries = new HashSet<string>(new[] { "ui_series_mariokart" }, StringComparer.OrdinalIgnoreCase);
                }
                var isMarioKartCircuit = stage.UiStageId is "ui_stage_kart_circuitfor" or "ui_stage_kart_circuitx";
                if ((!isMarioKartCircuit && !stageSeries.Contains(stage.UiSeriesId)) || string.IsNullOrEmpty(stage.BgmSetId))
                    continue;
                var chosenPlaylist = stagePlaylists.Contains(stage.BgmSetId) || state.Playlists.ContainsKey(stage.BgmSetId)
                    ? stage.BgmSetId
                    : stagePlaylists[0];
                //add stage entry for this series
                AddStageEntryIfChanged(songData, stage.UiStageId, chosenPlaylist, stage.BgmSettingNo);
            }
        }

        private static void AddStageEntryIfChanged(JObject songData, string stageId, string playlistId, int setting)
        {
            var changedPlaylist = !MusicConstants.DEFAULT_STAGE_BGM_SET_ID.TryGetValue(stageId, out var defaultPlaylist) ||
                                  !string.Equals(playlistId, defaultPlaylist, StringComparison.OrdinalIgnoreCase);
            var changedSetting = !MusicConstants.DEFAULT_STAGE_BGM_SETTING_NO.TryGetValue(stageId, out var defaultSetting) || setting != defaultSetting;
            if (!changedPlaylist && !changedSetting)
                return;
            var entries = GetArray(songData, "stage_database_entries");
            if (entries.Any(entry => string.Equals((string)entry["ui_stage_id"], stageId, StringComparison.OrdinalIgnoreCase)))
                return;
            var output = new JObject { ["ui_stage_id"] = stageId };
            if (changedPlaylist)
                output["bgm_set_id"] = playlistId;
            if (changedSetting)
                output["bgm_setting_no"] = setting;
            entries.Add(output);
        }

        #endregion
    }
}
