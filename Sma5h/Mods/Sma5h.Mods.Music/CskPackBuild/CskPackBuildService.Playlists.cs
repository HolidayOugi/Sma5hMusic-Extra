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
        #region Playlist Resources

        private static readonly HashSet<string> VanillaSeries = new HashSet<string>(new[]
        {
            "mario", "mariokart", "donkeykong", "zelda", "metroid", "yoshi", "kirby", "starfox", "pokemon", "fzero",
            "mother", "fireemblem", "gamewatch", "palutena", "wario", "pikmin", "doubutsu", "wiifit", "punchout",
            "xenoblade", "metalgear", "sonic", "rockman", "pacman", "streetfighter", "finalfantasy", "bayonetta",
            "splatoon", "castlevania", "smashbros", "arms", "persona", "dragonquest", "banjokazooie", "fatalfury",
            "minecraft", "tekken", "kingdomhearts", "etc"
        }, StringComparer.OrdinalIgnoreCase);

        private static readonly HashSet<string> VanillaNonSeriesPlaylists = new HashSet<string>(new[]
        {
            "bgmsmashmenu", "bgmplaylist", "bgmboss", "bgmsmashmode", "bgmadventure", "bgmstageedit"
        }, StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, List<string>> SeriesToPlaylist = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["doubutsu"] = new() { "bgmanimal" }, ["bayonetta"] = new() { "bgmbeyo" }, ["dragonquest"] = new() { "bgmbrave" },
            ["banjokazooie"] = new() { "bgmbuddy" }, ["tekken"] = new() { "bgmdemon" }, ["donkeykong"] = new() { "bgmdk" },
            ["fatalfury"] = new() { "bgmdolly" }, ["castlevania"] = new() { "bgmdracula" },
            ["finalfantasy"] = new() { "bgmedge", "bgmff" }, ["xenoblade"] = new() { "bgmelement", "bgmxenoblade" },
            ["fireemblem"] = new() { "bgmfe", "bgmmaster" }, ["starfox"] = new() { "bgmfox" }, ["fzero"] = new() { "bgmfzero" },
            ["gamewatch"] = new() { "bgmgamewatch" }, ["palutena"] = new() { "bgmicaros" }, ["persona"] = new() { "bgmjack" },
            ["kirby"] = new() { "bgmkirby" }, ["mario"] = new() { "bgmmario" }, ["metalgear"] = new() { "bgmmetalgear" },
            ["metroid"] = new() { "bgmmetroid" }, ["mariokart"] = new() { "bgmmkart" }, ["mother"] = new() { "bgmmother" },
            ["etc"] = new() { "bgmother" }, ["pacman"] = new() { "bgmpacman" }, ["minecraft"] = new() { "bgmpickel" },
            ["pikmin"] = new() { "bgmpikmin" }, ["pokemon"] = new() { "bgmpokemon" }, ["punchout"] = new() { "bgmpunchout" },
            ["rockman"] = new() { "bgmrockman" }, ["streetfighter"] = new() { "bgmsf" }, ["smashbros"] = new() { "bgmsmashbtl" },
            ["sonic"] = new() { "bgmsonic" }, ["splatoon"] = new() { "bgmspla" }, ["arms"] = new() { "bgmtantan" },
            ["kingdomhearts"] = new() { "bgmtrail" }, ["wario"] = new() { "bgmwario" }, ["wiifit"] = new() { "bgmwiifit" },
            ["yoshi"] = new() { "bgmyoshi" }, ["zelda"] = new() { "bgmzelda" }
        };

        private int DefaultPlaylistIncidence => _config.CurrentValue.Sma5hMusicGUI?.PlaylistIncidenceDefault ?? 0;

        #endregion

        #region Vanilla Playlists

        private void PopulateVanillaPlaylists(JObject songData, string seriesName, CskBuildState state, bool changedOnly = false)
        {
            if (!VanillaSeries.Contains(seriesName) || !SeriesToPlaylist.TryGetValue(seriesName, out var playlistIds))
                return;
            foreach (var playlistId in playlistIds)
            {
                if (changedOnly && !state.ChangedPlaylistIds.Contains(playlistId))
                    continue;
                if (!state.Playlists.TryGetValue(playlistId, out var playlist))
                    continue;
                var entries = EnsurePlaylist(songData, playlistId);
                foreach (var track in playlist.Tracks.Where(track => state.CoreBgmIds.Contains(track.UiBgmId)))
                {
                    AddOriginalCoreBgm(songData, track.UiBgmId, state);
                    entries.Add(CreatePlaylistOutputTrack(track, playlistId));
                }
            }
        }

        #endregion

        //adds entries for any core bgm that are part of a custom playlist
        private void PopulateCustomPlaylists(JObject songData, string seriesName, CskBuildState state)
        {
            //get all core bgms that are part of the series
            var seriesCoreBgmIds = state.OriginalBgmDbRoots.Values
                .Where(db => state.CoreGameSeriesById.TryGetValue(db.UiGameTitleId ?? string.Empty, out var bgmSeries) &&
                             string.Equals(bgmSeries, seriesName, StringComparison.OrdinalIgnoreCase))
                .Select(db => db.UiBgmId)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var vanillaIds = SeriesToPlaylist.Values.SelectMany(ids => ids).Concat(VanillaNonSeriesPlaylists).ToHashSet(StringComparer.OrdinalIgnoreCase);
            //for every custom playlist, check if any of the tracks are part of the series and add them to the song data if they are
            foreach (var playlist in state.Playlists.Values.Where(playlist => !vanillaIds.Contains(playlist.Id)))
            {
                foreach (var track in playlist.Tracks.Where(track => seriesCoreBgmIds.Contains(track.UiBgmId)))
                {
                    var entries = EnsurePlaylist(songData, playlist.Id);
                    if (entries.Any(entry => string.Equals((string)entry["ui_bgm_id"], track.UiBgmId, StringComparison.OrdinalIgnoreCase)))
                        continue;
                    AddOriginalCoreBgm(songData, track.UiBgmId, state);
                    var output = new JObject { ["ui_bgm_id"] = track.UiBgmId };
                    foreach (var setting in GetPlaylistSettingIndices(playlist.Id))
                    {
                        output[$"order{setting}"] = track.Order0;
                        output[$"incidence{setting}"] = track.Incidence0;
                    }
                    entries.Add(output);
                }
            }
        }

        #region Playlist Helpers

        private void AddOriginalCoreBgm(JObject songData, string uiBgmId, CskBuildState state)
        {
            if (!state.OriginalBgmDbRoots.TryGetValue(uiBgmId, out var original))
                return;
            state.BgmDbRoots.TryGetValue(uiBgmId, out var current);
            if (state.OverriddenCoreBgmIds.Contains(uiBgmId) || current?.MusicMod != null ||
                HasEntry(songData, "bgm_database_entries", "ui_bgm_id", uiBgmId))
                return;
            var order = current?.TestDispOrder ?? original.TestDispOrder;
            GetArray(songData, "bgm_database_entries").Add(new JObject
            {
                ["ui_bgm_id"] = original.UiBgmId, ["clone_from_ui_bgm_id"] = CloneBgmId,
                ["stream_set_id"] = original.StreamSetId, ["name_id"] = original.NameId,
                ["ui_gametitle_id"] = original.UiGameTitleId, ["test_disp_order"] = order,
                ["record_type"] = original.RecordType
            });
        }

        private JObject CreatePlaylistOutputTrack(PlaylistValueEntry track, string playlistId)
        {
            var output = new JObject { ["ui_bgm_id"] = track.UiBgmId };
            foreach (var setting in GetPlaylistSettingIndices(playlistId))
            {
                output[$"order{setting}"] = GetTrackOrder(track, setting);
                output[$"incidence{setting}"] = GetTrackIncidence(track, setting);
            }
            return output;
        }

        private Dictionary<string, HashSet<int>> BuildPlaylistSettingIndexMap()
        {
            var result = new Dictionary<string, HashSet<int>>(StringComparer.OrdinalIgnoreCase);
            foreach (var stage in _audioStateService.GetStagesEntries())
            {
                if (string.IsNullOrEmpty(stage.BgmSetId) || stage.BgmSettingNo >= 16)
                    continue;
                if (!result.TryGetValue(stage.BgmSetId, out var settings))
                    result[stage.BgmSetId] = settings = new HashSet<int>();
                settings.Add(stage.BgmSettingNo);
            }
            return result;
        }

        private IEnumerable<int> GetPlaylistSettingIndices(string playlistId)
        {
            return _playlistSettingIndices.Value != null && _playlistSettingIndices.Value.TryGetValue(playlistId, out var settings)
                ? settings.OrderBy(value => value)
                : Enumerable.Empty<int>();
        }

        private int GetNextPlaylistOrder(string seriesName, CskBuildState state)
        {
            var max = -1;
            foreach (var playlistId in GetFallbackPlaylistIds(seriesName))
                if (state.Playlists.TryGetValue(playlistId, out var playlist))
                    foreach (var track in playlist.Tracks)
                        for (var setting = 0; setting < 16; setting++)
                            max = Math.Max(max, GetTrackOrder(track, setting));
            return max + 1;
        }

        private static List<string> GetFallbackPlaylistIds(string seriesName)
        {
            if (!VanillaSeries.Contains(seriesName))
                return new List<string> { MusicConstants.InternalIds.PLAYLIST_SMASH_BATTLE };
            return SeriesToPlaylist.TryGetValue(seriesName, out var ids) ? ids : new List<string> { $"bgm{seriesName}" };
        }

        private bool IsManualPlaylistGeneration()
        {
            return _config.CurrentValue.Sma5hMusic?.PlaylistMapping?.GenerationMode == Sma5hMusicOptions.PlaylistGeneration.Manual;
        }

        private int AddToPlaylists(string uiBgmId, JObject songData, string seriesName, int orderCounter, CskBuildState state)
        {
            var found = false;
            foreach (var playlist in state.Playlists.Values)
            {
                var track = playlist.Tracks.FirstOrDefault(item => string.Equals(item.UiBgmId, uiBgmId, StringComparison.OrdinalIgnoreCase));
                if (track == null)
                    continue;
                found = true;
                var entries = EnsurePlaylist(songData, playlist.Id);
                if (!entries.Any(entry => string.Equals((string)entry["ui_bgm_id"], uiBgmId, StringComparison.OrdinalIgnoreCase)))
                    //bgm found in playlist, adds it to playlist entries
                    entries.Add(CreatePlaylistOutputTrack(track, playlist.Id));
            }

            var current = GetArray(songData, "bgm_database_entries").OfType<JObject>()
                .FirstOrDefault(entry => string.Equals((string)entry["ui_bgm_id"], uiBgmId, StringComparison.OrdinalIgnoreCase));
            //if bgm not found we try to add it to a fallback playlist (bgmseries for vanilla, battlefield for custom)
            if (!found && !IsManualPlaylistGeneration() && current != null && (int?)current["test_disp_order"] != -1)
            {
                foreach (var playlistId in GetFallbackPlaylistIds(seriesName))
                {
                    var output = new JObject { ["ui_bgm_id"] = uiBgmId };
                    foreach (var setting in GetPlaylistSettingIndices(playlistId))
                    {
                        output[$"order{setting}"] = orderCounter;
                        output[$"incidence{setting}"] = DefaultPlaylistIncidence;
                    }
                    EnsurePlaylist(songData, playlistId).Add(output);
                    orderCounter++;
                }
            }
            return orderCounter;
        }

        private static int GetTrackOrder(PlaylistValueEntry track, int setting)
        {
            return setting switch
            {
                0 => track.Order0, 1 => track.Order1, 2 => track.Order2, 3 => track.Order3,
                4 => track.Order4, 5 => track.Order5, 6 => track.Order6, 7 => track.Order7,
                8 => track.Order8, 9 => track.Order9, 10 => track.Order10, 11 => track.Order11,
                12 => track.Order12, 13 => track.Order13, 14 => track.Order14, 15 => track.Order15,
                _ => -1
            };
        }

        private static int GetTrackIncidence(PlaylistValueEntry track, int setting)
        {
            return setting switch
            {
                0 => track.Incidence0, 1 => track.Incidence1, 2 => track.Incidence2, 3 => track.Incidence3,
                4 => track.Incidence4, 5 => track.Incidence5, 6 => track.Incidence6, 7 => track.Incidence7,
                8 => track.Incidence8, 9 => track.Incidence9, 10 => track.Incidence10, 11 => track.Incidence11,
                12 => track.Incidence12, 13 => track.Incidence13, 14 => track.Incidence14, 15 => track.Incidence15,
                _ => 0
            };
        }

        #endregion
    }
}
