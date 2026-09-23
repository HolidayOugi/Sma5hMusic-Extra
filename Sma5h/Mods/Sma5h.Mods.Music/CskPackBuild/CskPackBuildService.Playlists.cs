using Newtonsoft.Json.Linq;
using Sma5h.Mods.Music.Helpers;
using Sma5h.Mods.Music.Models;
using Sma5h.Mods.Music.Models.PlaylistEntryModels;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Sma5h.Mods.Music.CskPackBuild
{
    public partial class CskPackBuildService
    {
        #region Vanilla Playlists

        private void PopulateVanillaPlaylists(JObject songData, string seriesName, JObject playlistData, HashSet<string> coreBgmIds, JObject coreBgmOverride, JObject orderOverride)
        {
            if (!VanillaSeries.Contains(seriesName))
                return;

            var playlists = SeriesToPlaylist.ContainsKey(seriesName.ToLowerInvariant())
                ? SeriesToPlaylist[seriesName.ToLowerInvariant()]
                : new List<string>();

            foreach (var playlistId in playlists)
            {
                var playlist = playlistData[playlistId];
                if (playlist == null)
                    continue;

                var playlistEntries = EnsurePlaylist(songData, playlistId);
                foreach (JObject track in GetArray(playlist, "tracks"))
                {
                    var uiBgmId = GetString(track, "ui_bgm_id");
                    if (!coreBgmIds.Contains(uiBgmId))
                        continue;

                    AddCoreBgmFromState(songData, uiBgmId, coreBgmOverride, orderOverride);

                    var entry = new JObject { ["ui_bgm_id"] = uiBgmId };
                    foreach (var i in GetPlaylistSettingIndices(playlistId))
                    {
                        entry[$"order{i}"] = GetInt(track, $"o{i}", 0);
                        entry[$"incidence{i}"] = GetInt(track, $"i{i}", DefaultPlaylistIncidence);
                    }

                    playlistEntries.Add(entry);
                }
            }
        }

        #endregion

        //adds entries for any core bgm that are part of a custom playlist
        private void PopulateCustomPlaylists(JObject songData, string seriesName, JObject playlistData, JObject coreBgmOverride, JObject orderOverride, Dictionary<string, string> coreGameSeriesById)
        {   
            //get all core bgms that are part of the series
            var seriesCoreBgmIds = _audioStateService.GetOriginalCoreBgmDbRootEntries()
                .Where(p => !string.IsNullOrEmpty(p.UiBgmId)
                            && !string.IsNullOrEmpty(p.UiGameTitleId)
                            && coreGameSeriesById.TryGetValue(p.UiGameTitleId, out var bgmSeries)
                            && string.Equals(bgmSeries, seriesName, StringComparison.OrdinalIgnoreCase))
                .Select(p => p.UiBgmId)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var vanillaPlaylistIds = SeriesToPlaylist.Values.SelectMany(p => p).Concat(VanillaNonSeriesPlaylists);

            //for every custom playlist, check if any of the tracks are part of the series and add them to the song data if they are
            foreach (var playlist in playlistData.Properties().Where(p => !vanillaPlaylistIds.Contains(p.Name, StringComparer.OrdinalIgnoreCase)))
            {
                foreach (JObject track in GetArray(playlist.Value, "tracks"))
                {
                    var uiBgmId = GetString(track, "ui_bgm_id");
                    if (!seriesCoreBgmIds.Contains(uiBgmId))
                        continue;

                    var entries = EnsurePlaylist(songData, playlist.Name);
                    if (entries.Any(p => string.Equals(GetString(p, "ui_bgm_id"), uiBgmId, StringComparison.OrdinalIgnoreCase)))
                        continue;

                    AddCoreBgmFromState(songData, uiBgmId, coreBgmOverride, orderOverride);
                    var entry = new JObject { ["ui_bgm_id"] = uiBgmId };
                    foreach (var i in GetPlaylistSettingIndices(playlist.Name))
                    {
                        entry[$"order{i}"] = GetInt(track, "o0", 0);
                        entry[$"incidence{i}"] = GetInt(track, "i0", DefaultPlaylistIncidence);
                    }
                    entries.Add(entry);
                }
            }
        }

        #region Effective Playlist Data

        //creates a diff of the vanilla playlists, only including playlists that have been modified by the override
        private JObject BuildVanillaPlaylistDiff(JObject playlistOverride)
        {
            var diff = new JObject();
            if (playlistOverride == null)
                return diff;

            var originalPlaylists = _audioStateService.GetOriginalCorePlaylists()
                .ToDictionary(p => p.Id, StringComparer.OrdinalIgnoreCase);
            var vanillaPlaylistIds = SeriesToPlaylist.Values.SelectMany(p => p).Concat(VanillaNonSeriesPlaylists);

            foreach (var playlist in playlistOverride.Properties().Where(p => vanillaPlaylistIds.Contains(p.Name, StringComparer.OrdinalIgnoreCase)))
            {
                var normalized = NormalizePlaylistObject(playlist.Name, (JObject)playlist.Value);
                if (!originalPlaylists.TryGetValue(playlist.Name, out var original))
                {
                    diff[playlist.Name] = normalized;
                    continue;
                }

                var originalTracks = new JArray(original.Tracks.Select(CreatePlaylistTrack));
                for (var i = 0; i < 16; i++)
                {   
                    //replicate behaviour of playlist_override.json in GUI
                    //the order ids are set based on their appeareance in GUI
                    var orderId = $"o{i}";
                    var visibleTracks = originalTracks
                        .OfType<JObject>()
                        .Where(p => GetInt(p, orderId, -1) != -1)
                        .OrderBy(p => GetInt(p, orderId, -1))
                        .ToList();

                    for (var order = 0; order < visibleTracks.Count; order++)
                        visibleTracks[order][orderId] = order;
                }

                if (!JToken.DeepEquals(normalized["tracks"], originalTracks))
                    diff[playlist.Name] = normalized;
            }

            return diff;
        }

        private JObject BuildEffectivePlaylistData(JObject playlistOverride)
        {
            var playlistData = new JObject();

            foreach (var playlist in _audioStateService.GetPlaylists())
                playlistData[playlist.Id] = CreatePlaylistObject(playlist);

            if (playlistOverride == null)
                return playlistData;

            foreach (var playlistProperty in playlistOverride.Properties())
            {
                var overridePlaylist = playlistProperty.Value as JObject;
                if (overridePlaylist == null)
                    continue;

                playlistData[playlistProperty.Name] = NormalizePlaylistObject(playlistProperty.Name, overridePlaylist);
            }

            return playlistData;
        }

        #endregion

        #region Playlist Resources

        private static JObject CreatePlaylistObject(PlaylistEntry playlist)
        {
            return new JObject
            {
                ["id"] = playlist.Id,
                ["title"] = playlist.Title,
                ["tracks"] = new JArray(playlist.Tracks.Select(CreatePlaylistTrack))
            };
        }

        private static JObject CreatePlaylistTrack(PlaylistValueEntry track)
        {
            var orders = new[]
            {
                track.Order0, track.Order1, track.Order2, track.Order3,
                track.Order4, track.Order5, track.Order6, track.Order7,
                track.Order8, track.Order9, track.Order10, track.Order11,
                track.Order12, track.Order13, track.Order14, track.Order15
            };
            var incidences = new[]
            {
                track.Incidence0, track.Incidence1, track.Incidence2, track.Incidence3,
                track.Incidence4, track.Incidence5, track.Incidence6, track.Incidence7,
                track.Incidence8, track.Incidence9, track.Incidence10, track.Incidence11,
                track.Incidence12, track.Incidence13, track.Incidence14, track.Incidence15
            };

            var output = new JObject { ["ui_bgm_id"] = track.UiBgmId };
            for (var i = 0; i < 16; i++)
            {
                output[$"o{i}"] = orders[i];
                output[$"i{i}"] = incidences[i];
            }

            return output;
        }

        private JObject NormalizePlaylistObject(string playlistId, JObject playlist)
        {
            return new JObject
            {
                ["id"] = GetString(playlist, "id", playlistId),
                ["title"] = GetString(playlist, "title"),
                ["tracks"] = new JArray(GetArray(playlist, "tracks").OfType<JObject>().Select(NormalizePlaylistTrack))
            };
        }

        private JObject NormalizePlaylistTrack(JObject track)
        {
            var output = new JObject { ["ui_bgm_id"] = GetString(track, "ui_bgm_id") };
            for (var i = 0; i < 16; i++)
            {
                output[$"o{i}"] = GetInt(track, $"o{i}", GetInt(track, $"order{i}", 0));
                output[$"i{i}"] = GetInt(track, $"i{i}", GetInt(track, $"incidence{i}", DefaultPlaylistIncidence));
            }

            return output;
        }

        #endregion

        #region Playlist Helpers

        private int DefaultPlaylistIncidence => _config.CurrentValue.Sma5hMusicGUI?.PlaylistIncidenceDefault ?? 0;

        private Dictionary<string, HashSet<int>> BuildPlaylistSettingIndexMap()
        {
            var result = new Dictionary<string, HashSet<int>>(StringComparer.OrdinalIgnoreCase);

            foreach (var stage in _audioStateService.GetStagesEntries())
            {
                if (string.IsNullOrEmpty(stage.BgmSetId) || stage.BgmSettingNo < 0 || stage.BgmSettingNo >= 16)
                    continue;

                if (!result.TryGetValue(stage.BgmSetId, out var settingIndices))
                {
                    settingIndices = new HashSet<int>();
                    result[stage.BgmSetId] = settingIndices;
                }

                settingIndices.Add(stage.BgmSettingNo);
            }

            return result;
        }

        private IEnumerable<int> GetPlaylistSettingIndices(string playlistId)
        {
            return _playlistSettingIndices.Value != null
                && _playlistSettingIndices.Value.TryGetValue(playlistId, out var settingIndices)
                    ? settingIndices.OrderBy(p => p)
                    : Enumerable.Empty<int>();
        }

        private int GetNextPlaylistOrder(string seriesName, JObject playlistData)
        {
            var playlistIds = GetFallbackPlaylistIds(seriesName);
            if (playlistIds.Count == 0)
                return 0;

            var maxOrder = -1;
            foreach (var playlistId in playlistIds)
            {
                foreach (JObject track in GetArray(playlistData[playlistId], "tracks"))
                {
                    for (var i = 0; i < 16; i++)
                        maxOrder = Math.Max(maxOrder, GetInt(track, $"o{i}", -1));
                }
            }

            return maxOrder + 1;
        }

        private static List<string> GetFallbackPlaylistIds(string seriesName)
        {
            if (!VanillaSeries.Contains(seriesName))
                return new List<string> { MusicConstants.InternalIds.PLAYLIST_SMASH_BATTLE };

            var seriesKey = seriesName.ToLowerInvariant();
            return SeriesToPlaylist.ContainsKey(seriesKey)
                ? SeriesToPlaylist[seriesKey]
                : new List<string> { $"bgm{seriesName}" };
        }

        private bool IsManualPlaylistGeneration()
        {
            return _config.CurrentValue.Sma5hMusic?.PlaylistMapping?.GenerationMode == Sma5hMusicOptions.PlaylistGeneration.Manual;
        }

        private int AddToPlaylists(string uiBgmId, JObject songData, JObject playlistOverride, string seriesName, int orderCounter)
        {
            var found = false;
            foreach (var playlistProperty in playlistOverride.Properties())
            {
                var playlistId = playlistProperty.Name;

                foreach (JObject track in GetArray(playlistProperty.Value, "tracks"))
                {
                    if (GetString(track, "ui_bgm_id") != uiBgmId)
                        continue;

                    //bgm found in playlist, adds it to playlist entries

                    found = true;
                    var entries = EnsurePlaylist(songData, playlistId);
                    if (entries.Any(p => GetString(p, "ui_bgm_id") == uiBgmId))
                        continue;

                    var entry = new JObject { ["ui_bgm_id"] = uiBgmId };

                    foreach (var i in GetPlaylistSettingIndices(playlistId))
                    {
                        entry[$"order{i}"] = GetInt(track, $"o{i}", orderCounter);
                        entry[$"incidence{i}"] = GetInt(track, $"i{i}", DefaultPlaylistIncidence);
                    }
                    entries.Add(entry);
                }
            }

            var currentEntry = GetArray(songData, "bgm_database_entries")
                .FirstOrDefault(p => GetString(p, "ui_bgm_id") == uiBgmId) as JObject;

            //if bgm not found we try to add it to a fallback playlist (bgmseries for vanilla, battlefield for custom)
            if (!found && !IsManualPlaylistGeneration() && currentEntry != null && GetInt(currentEntry, "test_disp_order", -1) != -1)
            {
                foreach (var fallbackPlaylistId in GetFallbackPlaylistIds(seriesName))
                {
                    var entries = EnsurePlaylist(songData, fallbackPlaylistId);
                    var entry = new JObject { ["ui_bgm_id"] = uiBgmId };
                    foreach (var i in GetPlaylistSettingIndices(fallbackPlaylistId))
                    {
                        entry[$"order{i}"] = orderCounter; //order counter used to order bgms in fallback playlists
                        entry[$"incidence{i}"] = DefaultPlaylistIncidence;
                    }

                    entries.Add(entry);
                    orderCounter++;
                }
            }

            return orderCounter;
        }

        #endregion
    }
}
