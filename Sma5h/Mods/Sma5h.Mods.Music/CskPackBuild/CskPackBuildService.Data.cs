using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Sma5h.Mods.Music.Interfaces;
using Sma5h.Mods.Music.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Sma5h.Mods.Music.CskPackBuild
{
    public partial class CskPackBuildService
    {
        #region Types

        private sealed class CskModContext
        {
            public IMusicMod Mod { get; init; }
            public MusicModEntries Entries { get; init; }
            public string PackName { get; init; }
            public string SafePackName { get; init; }
            public List<SeriesEntry> SeriesList { get; init; }
            public IReadOnlyList<MusicModSeriesEntries> OrderedSeries { get; init; }
        }

        private sealed class CskBuildState
        {
            public Dictionary<string, SeriesEntry> Series { get; init; }
            public Dictionary<string, GameTitleEntry> Games { get; init; }
            public Dictionary<string, GameTitleEntry> OriginalGames { get; init; }
            public Dictionary<string, BgmDbRootEntry> BgmDbRoots { get; init; }
            public Dictionary<string, BgmDbRootEntry> OriginalBgmDbRoots { get; init; }
            public Dictionary<string, BgmStreamSetEntry> StreamSets { get; init; }
            public Dictionary<string, BgmStreamSetEntry> OriginalStreamSets { get; init; }
            public Dictionary<string, BgmAssignedInfoEntry> AssignedInfos { get; init; }
            public Dictionary<string, BgmAssignedInfoEntry> OriginalAssignedInfos { get; init; }
            public Dictionary<string, BgmStreamPropertyEntry> StreamProperties { get; init; }
            public Dictionary<string, BgmStreamPropertyEntry> OriginalStreamProperties { get; init; }
            public Dictionary<string, BgmPropertyEntry> BgmProperties { get; init; }
            public Dictionary<string, BgmPropertyEntry> OriginalBgmProperties { get; init; }
            public Dictionary<string, PlaylistEntry> Playlists { get; init; }
            public Dictionary<string, PlaylistEntry> OriginalPlaylists { get; init; }
            public List<StageEntry> Stages { get; init; }
            public Dictionary<string, string> SeriesNamesById { get; set; }
            public Dictionary<string, string> CoreGameSeriesById { get; set; }
            public HashSet<string> CoreBgmIds { get; set; }
            public List<string> OverriddenCoreBgmIds { get; set; }
            public HashSet<string> ChangedPlaylistIds { get; set; }
            public List<CoreBgmVolumeEntry> CoreVolumeChanges { get; set; }
            public bool HasCoreChanges => OverriddenCoreBgmIds.Count > 0 || ChangedPlaylistIds.Count > 0 || CoreVolumeChanges.Count > 0;
        }

        private sealed class CoreBgmVolumeEntry
        {
            public string NameId { get; init; }
            public string SeriesName { get; init; }
            public float Volume { get; init; }
        }

        #endregion

        #region Mod Contexts

        private List<IMusicMod> GetMusicMods()
        {
            var mods = _musicModManagerService.MusicMods.ToList();
            return mods.Count > 0 ? mods : _musicModManagerService.RefreshMusicMods().ToList();
        }

        private List<CskModContext> LoadModContexts(IEnumerable<IMusicMod> mods)
        {
            var seriesState = IndexBy(_audioStateService.GetSeriesEntries(), item => item.UiSeriesId);
            var gameState = IndexBy(_audioStateService.GetGameTitleEntries(), item => item.UiGameTitleId);
            var contexts = new List<CskModContext>();

            foreach (var mod in mods)
            {
                var entries = mod.GetMusicModEntries(false);
                var series = entries.SeriesEntries
                    .Where(item => !string.IsNullOrEmpty(item.UiSeriesId))
                    .Select(item => seriesState.TryGetValue(item.UiSeriesId, out var current) && current.IsOverridden ? current : item)
                    .GroupBy(item => item.UiSeriesId, StringComparer.OrdinalIgnoreCase)
                    .Select(group => group.First())
                    .ToList();

                if (series.Count == 0)
                    continue;

                var packName = string.IsNullOrWhiteSpace(mod.Name) ? mod.Id : mod.Name;
                contexts.Add(new CskModContext
                {
                    Mod = mod,
                    Entries = entries,
                    PackName = packName,
                    SafePackName = SanitizePathSegment(packName, mod.Id, "pack folder name"),
                    SeriesList = series,
                    OrderedSeries = entries.OrderedSeries
                });
            }

            return contexts;
        }

        private IEnumerable<(GameTitleEntry Game, IReadOnlyList<MusicModBgmEntries> Bgms)> GetContextGamesForSeries(
            CskModContext context,
            string destinationSeriesId,
            CskBuildState state)
        {
            foreach (var sourceSeries in context.OrderedSeries)
            {
                foreach (var sourceGame in sourceSeries.Games)
                {
                    var game = sourceGame.Game;
                    var targetSeriesId = sourceSeries.Series.UiSeriesId;
                    if (state.Games.TryGetValue(game.UiGameTitleId, out var current))
                    {
                        game = current;
                        targetSeriesId = current.UiSeriesId;
                    }

                    if (string.Equals(targetSeriesId, destinationSeriesId, StringComparison.OrdinalIgnoreCase))
                        yield return (game, sourceGame.Bgms);
                }
            }
        }

        private IEnumerable<string> GetContextBgmIdsForSeries(
            CskModContext context,
            string destinationSeriesId,
            CskBuildState state)
        {
            foreach (var game in GetContextGamesForSeries(context, destinationSeriesId, state))
                foreach (var bgmId in game.Bgms.Select(bgm => bgm.Database.UiBgmId))
                    if (state.BgmDbRoots.ContainsKey(bgmId))
                        yield return bgmId;
        }

        #endregion

        #region Audio State Objects

        private CskBuildState CaptureBuildState()
        {
            var state = new CskBuildState
            {
                Series = IndexBy(_audioStateService.GetSeriesEntries(), item => item.UiSeriesId),
                Games = IndexBy(_audioStateService.GetGameTitleEntries(), item => item.UiGameTitleId),
                OriginalGames = IndexBy(_audioStateService.GetOriginalCoreGameTitleEntries(), item => item.UiGameTitleId),
                BgmDbRoots = IndexBy(_audioStateService.GetBgmDbRootEntries(), item => item.UiBgmId),
                OriginalBgmDbRoots = IndexBy(_audioStateService.GetOriginalCoreBgmDbRootEntries(), item => item.UiBgmId),
                StreamSets = IndexBy(_audioStateService.GetBgmStreamSetEntries(), item => item.StreamSetId),
                OriginalStreamSets = IndexBy(_audioStateService.GetOriginalCoreBgmStreamSetEntries(), item => item.StreamSetId),
                AssignedInfos = IndexBy(_audioStateService.GetBgmAssignedInfoEntries(), item => item.InfoId),
                OriginalAssignedInfos = IndexBy(_audioStateService.GetOriginalCoreBgmAssignedInfoEntries(), item => item.InfoId),
                StreamProperties = IndexBy(_audioStateService.GetBgmStreamPropertyEntries(), item => item.StreamId),
                OriginalStreamProperties = IndexBy(_audioStateService.GetOriginalCoreBgmStreamPropertyEntries(), item => item.StreamId),
                BgmProperties = IndexBy(_audioStateService.GetBgmPropertyEntries(), item => item.NameId),
                OriginalBgmProperties = IndexBy(_audioStateService.GetOriginalCoreBgmPropertyEntries(), item => item.NameId),
                Playlists = IndexBy(_audioStateService.GetPlaylists(), item => item.Id),
                OriginalPlaylists = IndexBy(_audioStateService.GetOriginalCorePlaylists(), item => item.Id),
                Stages = _audioStateService.GetStagesEntries().ToList()
            };

            state.SeriesNamesById = state.Series.Values
                .Where(item => !string.IsNullOrEmpty(item.UiSeriesId))
                .ToDictionary(item => item.UiSeriesId, item => item.NameId, StringComparer.OrdinalIgnoreCase);
            state.CoreGameSeriesById = state.Games.Values
                .Where(item => state.OriginalGames.ContainsKey(item.UiGameTitleId) && !string.IsNullOrEmpty(item.UiGameTitleId))
                .ToDictionary(
                    item => item.UiGameTitleId,
                    item => GetSeriesName(item.UiSeriesId, state.SeriesNamesById),
                    StringComparer.OrdinalIgnoreCase);
            state.CoreBgmIds = state.OriginalBgmDbRoots.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
            state.OverriddenCoreBgmIds = state.BgmDbRoots.Values
                .Where(current => current.MusicMod == null &&
                    state.OriginalBgmDbRoots.ContainsKey(current.UiBgmId) && current.IsOverridden)
                .OrderBy(current => current.OverrideOrder)
                .Select(current => current.UiBgmId)
                .ToList();
            state.ChangedPlaylistIds = state.Playlists.Values
                .Where(playlist => !state.OriginalPlaylists.TryGetValue(playlist.Id, out var original) || !PlaylistEquals(playlist, original))
                .Select(playlist => playlist.Id)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            state.CoreVolumeChanges = BuildCoreVolumeChanges(state);
            return state;
        }

        private static bool DictionaryEquals(Dictionary<string, string> current, Dictionary<string, string> original)
        {
            current ??= new Dictionary<string, string>();
            original ??= new Dictionary<string, string>();
            return current.Count == original.Count && current.All(pair =>
                original.TryGetValue(pair.Key, out var value) && string.Equals(pair.Value, value, StringComparison.Ordinal));
        }

        private static Dictionary<string, T> IndexBy<T>(IEnumerable<T> entries, Func<T, string> keySelector)
        {
            return entries
                .Where(item => !string.IsNullOrEmpty(keySelector(item)))
                .GroupBy(keySelector, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        }

        private static bool PlaylistEquals(PlaylistEntry current, PlaylistEntry original)
        {
            if (current.Tracks.Count != original.Tracks.Count)
                return false;
            for (var i = 0; i < current.Tracks.Count; i++)
            {
                var a = current.Tracks[i];
                var b = original.Tracks[i];
                if (a.UiBgmId != b.UiBgmId)
                    return false;
            }
            for (var setting = 0; setting < 16; setting++)
            {
                var normalizedOrders = original.Tracks
                    .Select((track, index) => new { Index = index, Order = GetTrackOrder(track, setting) })
                    .Where(item => item.Order != -1)
                    .OrderBy(item => item.Order)
                    .Select((item, order) => new { item.Index, Order = order })
                    .ToDictionary(item => item.Index, item => item.Order);
                for (var i = 0; i < current.Tracks.Count; i++)
                {
                    var expectedOrder = normalizedOrders.TryGetValue(i, out var order)
                        ? order
                        : GetTrackOrder(original.Tracks[i], setting);
                    if (GetTrackOrder(current.Tracks[i], setting) != expectedOrder ||
                        GetTrackIncidence(current.Tracks[i], setting) != GetTrackIncidence(original.Tracks[i], setting))
                        return false;
                }
            }
            return true;
        }

        private List<CoreBgmVolumeEntry> BuildCoreVolumeChanges(CskBuildState state)
        {
            var output = new List<CoreBgmVolumeEntry>();
            foreach (var current in state.BgmProperties.Values)
            {
                if (current.MusicMod != null || !state.OriginalBgmProperties.TryGetValue(current.NameId, out var original) ||
                    Math.Abs(current.AudioVolume - original.AudioVolume) < 0.0001f)
                    continue;
                var db = FindOriginalCoreDbRootByNameId(current.NameId, state);
                if (db == null || !state.Games.TryGetValue(db.UiGameTitleId ?? string.Empty, out var game))
                    continue;
                output.Add(new CoreBgmVolumeEntry
                {
                    NameId = current.NameId,
                    SeriesName = GetSeriesName(game.UiSeriesId, state.SeriesNamesById),
                    Volume = current.AudioVolume
                });
            }
            return output;
        }

        private static BgmDbRootEntry FindOriginalCoreDbRootByNameId(string nameId, CskBuildState state)
        {
            return state.OriginalBgmDbRoots.Values.FirstOrDefault(item =>
                string.Equals(item.NameId, nameId, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrEmpty(item.UiBgmId) && item.UiBgmId.StartsWith("ui_bgm_", StringComparison.OrdinalIgnoreCase) &&
                 string.Equals(item.UiBgmId.Substring("ui_bgm_".Length), nameId, StringComparison.OrdinalIgnoreCase)));
        }

        private static string GetSeriesName(string uiSeriesId, Dictionary<string, string> seriesNamesById)
        {
            if (string.IsNullOrEmpty(uiSeriesId))
                return null;
            if (seriesNamesById.TryGetValue(uiSeriesId, out var name))
                return name;
            return uiSeriesId.StartsWith("ui_series_", StringComparison.OrdinalIgnoreCase)
                ? uiSeriesId.Substring("ui_series_".Length)
                : uiSeriesId;
        }

        private static string[] GetInfoIds(BgmStreamSetEntry item)
        {
            return new[] { item.Info0, item.Info1, item.Info2, item.Info3, item.Info4, item.Info5, item.Info6, item.Info7,
                           item.Info8, item.Info9, item.Info10, item.Info11, item.Info12, item.Info13, item.Info14, item.Info15 };
        }

        private static string[] GetDataNames(BgmStreamPropertyEntry item)
        {
            return new[] { item.DataName0, item.DataName1, item.DataName2, item.DataName3, item.DataName4 };
        }

        #endregion

        #region JSON

        private static JObject CreateSongData()
        {
            return new JObject
            {
                ["series_database_entries"] = new JArray(),
                ["gametitle_database_entries"] = new JArray(),
                ["bgm_database_entries"] = new JArray(),
                ["stream_set_entries"] = new JArray(),
                ["assigned_info_entries"] = new JArray(),
                ["stream_property_entries"] = new JArray(),
                ["bgm_property_entries"] = new JArray(),
                ["playlist_entries"] = new JObject(),
                ["stage_database_entries"] = new JArray()
            };
        }

        private static JArray GetArray(JObject data, string name)
        {
            if (data[name] is JArray array)
                return array;
            array = new JArray();
            data[name] = array;
            return array;
        }

        private static JArray EnsurePlaylist(JObject songData, string playlistId)
        {
            if (songData["playlist_entries"] is not JObject playlists)
                songData["playlist_entries"] = playlists = new JObject();
            if (playlists[playlistId] is not JArray tracks)
                playlists[playlistId] = tracks = new JArray();
            return tracks;
        }

        private static void AddOrReplaceByKey(JObject songData, string arrayName, string key, string value, JObject entry)
        {
            var entries = GetArray(songData, arrayName);
            for (var i = entries.Count - 1; i >= 0; i--)
                if (string.Equals((string)entries[i]?[key], value, StringComparison.OrdinalIgnoreCase))
                    entries.RemoveAt(i);
            entries.Add(entry);
        }

        private static void AddIfMissingByKey(JObject songData, string arrayName, string key, string value, JObject entry)
        {
            if (!HasEntry(songData, arrayName, key, value))
                GetArray(songData, arrayName).Add(entry);
        }

        private static bool HasEntry(JObject songData, string arrayName, string key, string value)
        {
            return GetArray(songData, arrayName).Any(item => string.Equals((string)item?[key], value, StringComparison.OrdinalIgnoreCase));
        }

        #endregion

        #region Game Entries

        private static JObject CreateGameEntry(GameTitleEntry game)
        {
            return new JObject
            {
                ["ui_gametitle_id"] = game.UiGameTitleId,
                ["clone_from_gametitle_id"] = CloneGameTitleId,
                ["name_id"] = game.NameId,
                ["ui_series_id"] = game.UiSeriesId,
                ["shown_as_series_in_directory"] = game.Unk1
            };
        }

        #endregion

        #region BGM Entries

        private static JObject CreateStreamSetEntry(BgmStreamSetEntry streamSet)
        {
            var entry = new JObject { ["stream_set_id"] = streamSet.StreamSetId };
            var infos = GetInfoIds(streamSet);
            for (var i = 0; i < infos.Length; i++)
                if (!string.IsNullOrEmpty(infos[i]))
                    entry[$"info{i}"] = infos[i];
            var specialCategory = streamSet.SerializedSpecialCategory ?? streamSet.SpecialCategory;
            if (!string.IsNullOrWhiteSpace(specialCategory) &&
                (!string.Equals(specialCategory, "sf_situationlink", StringComparison.OrdinalIgnoreCase) || !string.IsNullOrWhiteSpace(streamSet.Info1)))
                entry["special_category"] = specialCategory;
            return entry;
        }

        private static JObject CreateAssignedInfoEntry(BgmAssignedInfoEntry entry, bool full)
        {
            if (!full)
            {
                return new JObject
                {
                    ["info_id"] = entry.InfoId,
                    ["stream_id"] = entry.StreamId,
                    ["condition"] = entry.Condition,
                    ["condition_process"] = "sound_condition_process_add",
                    ["change_fadeout_frame"] = 60,
                    ["menu_change_fadeout_frame"] = 60
                };
            }
            return new JObject
            {
                ["info_id"] = entry.InfoId, ["stream_id"] = entry.StreamId, ["condition"] = entry.Condition,
                ["condition_process"] = entry.ConditionProcess, ["start_frame"] = entry.StartFrame,
                ["change_fadein_frame"] = entry.ChangeFadeInFrame, ["change_start_delay_frame"] = entry.ChangeStartDelayFrame,
                ["change_fadeout_frame"] = entry.ChangeFadoutFrame, ["change_stop_delay_frame"] = entry.ChangeStopDelayFrame,
                ["menu_change_fadein_frame"] = entry.MenuChangeFadeInFrame,
                ["menu_change_start_delay_frame"] = entry.MenuChangeStartDelayFrame,
                ["menu_change_fadeout_frame"] = entry.MenuChangeFadeOutFrame,
                ["menu_change_stop_delay_frame"] = entry.MenuChangeStopDelayFrame
            };
        }

        private static JObject CreateStreamPropertyEntry(BgmStreamPropertyEntry entry, bool full)
        {
            if (!full)
                return new JObject { ["stream_id"] = entry.StreamId, ["data_name0"] = entry.DataName0 };
            return new JObject
            {
                ["stream_id"] = entry.StreamId, ["data_name0"] = entry.DataName0, ["data_name1"] = entry.DataName1,
                ["data_name2"] = entry.DataName2, ["data_name3"] = entry.DataName3, ["data_name4"] = entry.DataName4,
                ["loop"] = entry.Loop, ["end_point"] = entry.EndPoint, ["fadeout_frame"] = entry.FadeOutFrame,
                ["start_point_suddendeath"] = entry.StartPointSuddenDeath, ["start_point_transition"] = entry.StartPointTransition,
                ["start_point0"] = entry.StartPoint0, ["start_point1"] = entry.StartPoint1, ["start_point2"] = entry.StartPoint2,
                ["start_point3"] = entry.StartPoint3, ["start_point4"] = entry.StartPoint4
            };
        }

        private static JObject CreateBgmPropertyEntry(BgmPropertyEntry entry, string streamName = null)
        {
            return new JObject
            {
                ["stream_name"] = streamName ?? entry.NameId, ["loop_start_ms"] = entry.LoopStartMs,
                ["loop_start_sample"] = entry.LoopStartSample, ["loop_end_ms"] = entry.LoopEndMs,
                ["loop_end_sample"] = entry.LoopEndSample == uint.MaxValue ? 0 : entry.LoopEndSample, ["duration_ms"] = entry.TotalTimeMs,
                ["duration_sample"] = entry.TotalSamples
            };
        }

        private static void AddCoreBgmDatabaseEntry(JObject songData, BgmDbRootEntry db)
        {
            AddOrReplaceByKey(songData, "bgm_database_entries", "ui_bgm_id", db.UiBgmId, new JObject
            {
                ["ui_bgm_id"] = db.UiBgmId, ["clone_from_ui_bgm_id"] = CloneBgmId,
                ["stream_set_id"] = db.StreamSetId, ["name_id"] = db.NameId,
                ["ui_gametitle_id"] = db.UiGameTitleId, ["test_disp_order"] = db.TestDispOrder,
                ["record_type"] = string.IsNullOrEmpty(db.RecordType) ? "record_original" : db.RecordType
            });
        }

        #endregion

        #region BGM Processing

        private int AddBgmToPack(
            BgmDbRootEntry db,
            JObject songData,
            List<string> msgBgmEntries,
            string seriesName,
            string packFolderName,
            string outputRoot,
            string generatedBgmFolder,
            HashSet<string> writtenModBgmIds,
            bool includeAudio,
            bool fullMetadata,
            bool includeMessages,
            MusicModBgmEntries modBgm,
            int orderCounter,
            CskBuildState state)
        {
            if (db == null || string.IsNullOrEmpty(db.UiBgmId) || string.IsNullOrEmpty(db.StreamSetId))
                return orderCounter;
            var outputDb = modBgm?.Database ?? db;
            var streamSet = modBgm?.StreamSet;
            if (streamSet == null && !state.StreamSets.TryGetValue(outputDb.StreamSetId, out streamSet))
                return orderCounter;

            var properties = modBgm == null
                ? GetBgmProperties(db, state).ToList()
                : new List<BgmPropertyEntry> { modBgm.Property };
            var audioNameId = properties.FirstOrDefault()?.NameId;
            //get name id from audioservice if available, otherwise use the one from the metadata
            var resolvedNameId = string.IsNullOrEmpty(db.NameId) ? audioNameId : db.NameId;
            if (includeAudio && !string.IsNullOrEmpty(audioNameId) && _unavailableBgmNameIds.Value?.Contains(audioNameId) == true)
            {
                _logger.LogWarning("[CSK] Excluding unavailable song {NameId} from pack metadata.", audioNameId);
                return orderCounter;
            }

            var alreadyWrittenAsMod = writtenModBgmIds?.Contains(db.UiBgmId) == true;
            var testDispOrder = modBgm != null && db.TestDispOrder == short.MaxValue && !state.BgmDbRoots.ContainsKey(db.UiBgmId)
                ? 0
                : db.TestDispOrder;
            //add to json
            if (!HasEntry(songData, "bgm_database_entries", "ui_bgm_id", db.UiBgmId) || !alreadyWrittenAsMod)
            {
                AddOrReplaceByKey(songData, "bgm_database_entries", "ui_bgm_id", db.UiBgmId, new JObject
                {
                    ["ui_bgm_id"] = outputDb.UiBgmId, ["clone_from_ui_bgm_id"] = CloneBgmId,
                    ["stream_set_id"] = outputDb.StreamSetId, ["name_id"] = resolvedNameId,
                    ["ui_gametitle_id"] = outputDb.UiGameTitleId, ["test_disp_order"] = testDispOrder,
                    ["record_type"] = string.IsNullOrEmpty(outputDb.RecordType) ? "record_original" : outputDb.RecordType
                });
                if (fullMetadata)
                    AddIfMissingByKey(songData, "stream_set_entries", "stream_set_id", streamSet.StreamSetId, CreateStreamSetEntry(streamSet));
                else
                    AddOrReplaceByKey(songData, "stream_set_entries", "stream_set_id", streamSet.StreamSetId, CreateStreamSetEntry(streamSet));

                if (modBgm != null)
                {
                    var assigned = modBgm.AssignedInfo;
                    var streamProperty = modBgm.StreamProperty;
                    if (assigned != null && streamProperty != null)
                    {
                        var streamName = string.IsNullOrEmpty(streamProperty.DataName0) ? modBgm.Property.NameId : streamProperty.DataName0;
                        AddOrReplaceByKey(songData, "assigned_info_entries", "info_id", assigned.InfoId, CreateAssignedInfoEntry(assigned, false));
                        AddOrReplaceByKey(songData, "stream_property_entries", "stream_id", streamProperty.StreamId, CreateStreamPropertyEntry(streamProperty, false));
                        AddOrReplaceByKey(songData, "bgm_property_entries", "stream_name", streamName, CreateBgmPropertyEntry(modBgm.Property, streamName));
                    }
                }
                else
                {
                    foreach (var infoId in GetInfoIds(streamSet).Where(id => !string.IsNullOrEmpty(id)))
                    {
                        if (!state.AssignedInfos.TryGetValue(infoId, out var assigned))
                            continue;
                        AddIfMissingByKey(songData, "assigned_info_entries", "info_id", assigned.InfoId, CreateAssignedInfoEntry(assigned, true));
                        if (!state.StreamProperties.TryGetValue(assigned.StreamId ?? string.Empty, out var streamProperty))
                            continue;
                        AddIfMissingByKey(songData, "stream_property_entries", "stream_id", streamProperty.StreamId, CreateStreamPropertyEntry(streamProperty, true));
                        var nameId = streamProperty.DataName0;
                        if (!string.IsNullOrEmpty(nameId) && state.BgmProperties.TryGetValue(nameId, out var property))
                            AddIfMissingByKey(songData, "bgm_property_entries", "stream_name", property.NameId, CreateBgmPropertyEntry(property));
                    }
                }

                if (includeMessages)
                {
                    AddOrReplaceMessage(msgBgmEntries, $"bgm_title_{resolvedNameId}", GetLocalizedString(outputDb.Title, resolvedNameId));
                    AddOrReplaceMessage(msgBgmEntries, $"bgm_author_{resolvedNameId}", GetLocalizedString(outputDb.Author));
                    AddOrReplaceMessage(msgBgmEntries, $"bgm_copyright_{resolvedNameId}", GetLocalizedString(outputDb.Copyright));
                }
                writtenModBgmIds?.Add(db.UiBgmId);
            }

            //add playlist entries
            orderCounter = AddToPlaylists(db.UiBgmId, songData, seriesName, orderCounter, state);
            //copy audio files
            if (includeAudio && modBgm != null && !string.IsNullOrEmpty(audioNameId))
                MoveGeneratedBgmFiles(audioNameId, packFolderName, outputRoot, generatedBgmFolder);
            return orderCounter;
        }

        private void AddCoreBgmTextChanges(BgmDbRootEntry current, List<string> messages)
        {
            if (current == null || string.IsNullOrEmpty(current.NameId))
                return;
            if (current.Title.Count > 0)
                AddOrReplaceMessage(messages, $"bgm_title_{current.NameId}", GetLocalizedString(current.Title, current.NameId));
            if (current.Author.Count > 0)
                AddOrReplaceMessage(messages, $"bgm_author_{current.NameId}", GetLocalizedString(current.Author));
            if (current.Copyright.Count > 0)
                AddOrReplaceMessage(messages, $"bgm_copyright_{current.NameId}", GetLocalizedString(current.Copyright));
        }
        #endregion

        #region Normalization

        private static void NormalizeCombinedSongData(JObject songData)
        {
            foreach (var pair in new[]
            {
                ("series_database_entries", "ui_series_id"), ("gametitle_database_entries", "ui_gametitle_id"),
                ("bgm_database_entries", "ui_bgm_id"), ("stream_set_entries", "stream_set_id"),
                ("assigned_info_entries", "info_id"), ("stream_property_entries", "stream_id"),
                ("bgm_property_entries", "stream_name"), ("stage_database_entries", "ui_stage_id")
            })
            {
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var output = new JArray();
                foreach (var item in GetArray(songData, pair.Item1).OfType<JObject>())
                {
                    var id = (string)item[pair.Item2];
                    if (string.IsNullOrEmpty(id) || seen.Add(id))
                        output.Add(item);
                }
                songData[pair.Item1] = output;
            }

            if (songData["playlist_entries"] is not JObject playlists)
                return;
            foreach (var playlist in playlists.Properties().ToList())
            {
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                playlist.Value = new JArray((playlist.Value as JArray ?? new JArray()).OfType<JObject>()
                    .Where(item => string.IsNullOrEmpty((string)item["ui_bgm_id"]) || seen.Add((string)item["ui_bgm_id"])));
            }
        }

        #endregion
    }
}
