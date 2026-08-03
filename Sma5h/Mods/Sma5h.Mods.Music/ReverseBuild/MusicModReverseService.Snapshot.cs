using paracobNET;
using Microsoft.Extensions.Logging;
using Sma5h.Data;
using Sma5h.Data.Ui.Param.Database;
using Sma5h.Helpers;
using Sma5h.Interfaces;
using Sma5h.Mods.Data.Sound.Config;
using Sma5h.Mods.Music.Helpers;
using Sma5h.Mods.Music.Models;
using Sma5h.ResourceProviders.Constants;
using Sma5h.ResourceProviders.Prc.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Sma5h.Mods.Music.ReverseBuild
{
    public partial class MusicModReverseService
    {
        private ResourceSnapshot LoadSnapshot(string rootPath, string fallbackRootPath = null, ISet<string> audioFallbackToneIds = null)
        {
            //loads either from build or resources
            var bgmDbPath = ResolveSnapshotFilePath(rootPath, PrcExtConstants.PRC_UI_BGM_DB_PATH, fallbackRootPath);
            var gameTitleDbPath = ResolveSnapshotFilePath(rootPath, PrcExtConstants.PRC_UI_GAMETITLE_DB_PATH, fallbackRootPath);
            var seriesDbPath = ResolveSnapshotFilePath(rootPath, PrcExtConstants.PRC_UI_SERIES_DB_PATH, fallbackRootPath);
            var stageDbPath = ResolveSnapshotFilePath(rootPath, PrcExtConstants.PRC_UI_STAGE_DB_PATH, fallbackRootPath);
            var bgmPropertyPath = Path.Combine(rootPath, BgmPropertyFileConstants.BGM_PROPERTY_PATH);

            //read build files
            var bgmDb = _prcProvider.ReadFile<PrcUiBgmDatabase>(bgmDbPath, true);
            var gameTitleDb = _prcProvider.ReadFile<PrcUiGameTitleDatabase>(gameTitleDbPath);
            var seriesDb = ReadSeriesDatabase(seriesDbPath);
            var stageDb = _prcProvider.ReadFile<PrcUiStageDatabase>(stageDbPath);
            var bgmProperty = File.Exists(bgmPropertyPath)
                ? _bgmPropertyProvider.ReadFile<BinBgmProperty>(bgmPropertyPath)
                : null;
            if (bgmProperty != null && !string.IsNullOrWhiteSpace(fallbackRootPath))
                _logger.LogInformation("Reverse MusicMod: loaded {RelativePath} from output: {Path}.", BgmPropertyFileConstants.BGM_PROPERTY_PATH, bgmPropertyPath);

            if (bgmDb == null || gameTitleDb == null || seriesDb == null || stageDb == null)
                throw new InvalidOperationException($"Could not read required music resources from {rootPath}.");

            var snapshot = new ResourceSnapshot();
            //get bgm and title names from MSBTs
            var bgmMsbts = LoadMsbtDatabases(rootPath, MsbtExtConstants.MSBT_BGM, fallbackRootPath);
            var titleMsbts = LoadMsbtDatabases(rootPath, MsbtExtConstants.MSBT_TITLE, fallbackRootPath);

            //get toneIDs from filenames
            var toneIds = GetToneIds(rootPath, bgmProperty);
            var seriesIds = new Dictionary<string, string>();
            var gameTitleIds = new Dictionary<string, string>();

            //nameIDs are in plain text inside PRCs

            //generate series IDs from nameID
            foreach (var value in seriesDb.DbRootEntries.Values)
                AddGeneratedId(seriesIds, value.UiSeriesId, MusicConstants.InternalIds.SERIES_ID_PREFIX, value.NameId);

            //generate game title IDs from nameID
            foreach (var value in gameTitleDb.DbRootEntries.Values)
                AddGeneratedId(gameTitleIds, value.UiGameTitleId, MusicConstants.InternalIds.GAME_TITLE_ID_PREFIX, value.NameId);

            //add entries to snapshot
            LoadBgmEntries(snapshot, bgmDb, bgmMsbts, toneIds, gameTitleIds);
            var skippedFallbackToneIds = LoadBgmPropertyEntries(snapshot, rootPath, bgmProperty, toneIds, audioFallbackToneIds);
            LoadGameEntries(snapshot, gameTitleDb, titleMsbts, gameTitleIds, seriesIds);
            LoadSeriesEntries(snapshot, seriesDb, titleMsbts, seriesIds);
            //generate playlist IDs
            var playlistIdHints = BuildStagePlaylistIdHints(stageDb);
            var playlistIds = LoadPlaylistEntries(snapshot, bgmDb, toneIds, playlistIdHints);
            LoadStageEntries(snapshot, stageDb, seriesIds, playlistIds);
            //if some tracks are invalid, remove them
            RemoveSkippedBgmEntries(snapshot, skippedFallbackToneIds);

            return snapshot;
        }

        private void LoadBgmEntries(
            ResourceSnapshot snapshot,
            PrcUiBgmDatabase bgmDb,
            Dictionary<string, MsbtDatabase> bgmMsbts,
            List<string> toneIds,
            Dictionary<string, string> gameTitleIds)
        {
            foreach (var value in bgmDb.DbRootEntries.Values)
            {
                //generate all IDs from tone IDs and different prefixes
                //input is hash40, returns plain text nameID with prefix if available
                value.UiBgmId = ResolveGeneratedId(value.UiBgmId, toneIds, MusicConstants.InternalIds.UI_BGM_ID_PREFIX);
                value.StreamSetId = ResolveGeneratedId(value.StreamSetId, toneIds, MusicConstants.InternalIds.STREAM_SET_PREFIX);
                value.UiGameTitleId = ResolveKnownId(value.UiGameTitleId, gameTitleIds);
                value.UiGameTitleId1 = ResolveKnownId(value.UiGameTitleId1, gameTitleIds);
                value.UiGameTitleId2 = ResolveKnownId(value.UiGameTitleId2, gameTitleIds);
                value.UiGameTitleId3 = ResolveKnownId(value.UiGameTitleId3, gameTitleIds);
                value.UiGameTitleId4 = ResolveKnownId(value.UiGameTitleId4, gameTitleIds);

                var entry = _mapper.Map(value, new BgmDbRootEntry(value.UiBgmId));
                FillBgmMsbt(entry, bgmMsbts);
                snapshot.BgmDbRootEntries.Add(entry.UiBgmId, entry);
            }

            foreach (var value in bgmDb.StreamSetEntries.Values)
            {
                //generate stream set and info IDs from tone IDs
                value.StreamSetId = ResolveGeneratedId(value.StreamSetId, toneIds, MusicConstants.InternalIds.STREAM_SET_PREFIX);
                value.Info0 = ResolveGeneratedId(value.Info0, toneIds, MusicConstants.InternalIds.INFO_ID_PREFIX);
                value.Info1 = ResolveGeneratedId(value.Info1, toneIds, MusicConstants.InternalIds.INFO_ID_PREFIX);
                value.Info2 = ResolveGeneratedId(value.Info2, toneIds, MusicConstants.InternalIds.INFO_ID_PREFIX);
                value.Info3 = ResolveGeneratedId(value.Info3, toneIds, MusicConstants.InternalIds.INFO_ID_PREFIX);
                value.Info4 = ResolveGeneratedId(value.Info4, toneIds, MusicConstants.InternalIds.INFO_ID_PREFIX);
                value.Info5 = ResolveGeneratedId(value.Info5, toneIds, MusicConstants.InternalIds.INFO_ID_PREFIX);
                value.Info6 = ResolveGeneratedId(value.Info6, toneIds, MusicConstants.InternalIds.INFO_ID_PREFIX);
                value.Info7 = ResolveGeneratedId(value.Info7, toneIds, MusicConstants.InternalIds.INFO_ID_PREFIX);
                value.Info8 = ResolveGeneratedId(value.Info8, toneIds, MusicConstants.InternalIds.INFO_ID_PREFIX);
                value.Info9 = ResolveGeneratedId(value.Info9, toneIds, MusicConstants.InternalIds.INFO_ID_PREFIX);
                value.Info10 = ResolveGeneratedId(value.Info10, toneIds, MusicConstants.InternalIds.INFO_ID_PREFIX);
                value.Info11 = ResolveGeneratedId(value.Info11, toneIds, MusicConstants.InternalIds.INFO_ID_PREFIX);
                value.Info12 = ResolveGeneratedId(value.Info12, toneIds, MusicConstants.InternalIds.INFO_ID_PREFIX);
                value.Info13 = ResolveGeneratedId(value.Info13, toneIds, MusicConstants.InternalIds.INFO_ID_PREFIX);
                value.Info14 = ResolveGeneratedId(value.Info14, toneIds, MusicConstants.InternalIds.INFO_ID_PREFIX);
                value.Info15 = ResolveGeneratedId(value.Info15, toneIds, MusicConstants.InternalIds.INFO_ID_PREFIX);

                snapshot.StreamSetEntries.Add(value.StreamSetId, _mapper.Map(value, new BgmStreamSetEntry(value.StreamSetId)));
            }

            foreach (var value in bgmDb.AssignedInfoEntries.Values)
            {
                //generate info and stream IDs from tone IDs
                value.InfoId = ResolveGeneratedId(value.InfoId, toneIds, MusicConstants.InternalIds.INFO_ID_PREFIX);
                value.StreamId = ResolveGeneratedId(value.StreamId, toneIds, MusicConstants.InternalIds.STREAM_PREFIX);
                snapshot.AssignedInfoEntries.Add(value.InfoId, _mapper.Map(value, new BgmAssignedInfoEntry(value.InfoId)));
            }

            foreach (var value in bgmDb.StreamPropertyEntries.Values)
            {
                //generate stream ID from tone ID
                value.StreamId = ResolveGeneratedId(value.StreamId, toneIds, MusicConstants.InternalIds.STREAM_PREFIX);
                snapshot.StreamPropertyEntries.Add(value.StreamId, _mapper.Map(value, new BgmStreamPropertyEntry(value.StreamId)));
            }
        }

        private HashSet<string> LoadBgmPropertyEntries(
            ResourceSnapshot snapshot,
            string rootPath,
            BinBgmProperty bgmProperty,
            List<string> toneIds,
            ISet<string> audioFallbackToneIds)
        {
            if (bgmProperty == null)
            {
                //if bgm property is missing, try to read info from nus3audio files in the bgm folder
                return LoadBgmPropertyEntriesFromAudioFallback(snapshot, rootPath);
            }

            foreach (var value in bgmProperty.Entries.Values)
            {
                //generate bgm property nameID from tone ID
                value.NameId = ResolveGeneratedId(value.NameId, toneIds, string.Empty);
                if (audioFallbackToneIds?.Contains(value.NameId) == true)
                    continue;

                var filename = Path.Combine(rootPath, "stream;", "sound", "bgm", string.Format(MusicConstants.GameResources.NUS3AUDIO_FILE, value.NameId));
                snapshot.BgmPropertyEntries.Add(value.NameId, _mapper.Map(value, new BgmPropertyEntry(value.NameId, filename)));
            }

            return audioFallbackToneIds == null || audioFallbackToneIds.Count == 0
                ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                : LoadBgmPropertyEntriesFromAudioFallback(snapshot, rootPath, audioFallbackToneIds);
        }

        private HashSet<string> LoadBgmPropertyEntriesFromAudioFallback(ResourceSnapshot snapshot, string rootPath, ISet<string> toneIds = null)
        {
            var skippedToneIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var bgmPath = Path.Combine(rootPath, "stream;", "sound", "bgm");
            if (!Directory.Exists(bgmPath))
                return skippedToneIds;

            foreach (var file in Directory.EnumerateFiles(bgmPath, "*.nus3audio"))
            {
                //get nameID from nus3audio filename
                var nameId = Path.GetFileNameWithoutExtension(file);
                if (nameId.StartsWith(MusicConstants.InternalIds.NUS3AUDIO_FILE_PREFIX, StringComparison.OrdinalIgnoreCase))
                    nameId = nameId.Substring(MusicConstants.InternalIds.NUS3AUDIO_FILE_PREFIX.Length);
                if (nameId.StartsWith(MusicConstants.InternalIds.UI_BGM_ID_PREFIX, StringComparison.OrdinalIgnoreCase))
                    nameId = nameId.Substring(MusicConstants.InternalIds.UI_BGM_ID_PREFIX.Length);

                if (toneIds != null && !toneIds.Contains(nameId))
                    continue;

                //read audio metadata from nus3audio file
                _logger.LogInformation("Reverse MusicMod: scanning {AudioFile} with vgmstream.", file);
                AudioCuePoints audioCuePoints;
                try
                {
                    audioCuePoints = _audioMetadataService.GetCuePoints(file).GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Reverse MusicMod: skipping {AudioFile} because vgmstream could not read audio metadata.", file);
                    skippedToneIds.Add(nameId);
                    continue;
                }

                if (audioCuePoints == null || audioCuePoints.TotalSamples == 0)
                {
                    _logger.LogWarning("Reverse MusicMod: skipping {AudioFile} because vgmstream could not read audio metadata.", file);
                    skippedToneIds.Add(nameId);
                    continue;
                }

                var entry = new BgmPropertyEntry(nameId, file)
                {
                    TotalSamples = audioCuePoints.TotalSamples,
                    TotalTimeMs = audioCuePoints.TotalTimeMs,
                    LoopStartSample = audioCuePoints.LoopStartSample,
                    LoopEndSample = audioCuePoints.LoopEndSample,
                    LoopStartMs = audioCuePoints.LoopStartMs,
                    LoopEndMs = audioCuePoints.LoopEndMs
                };

                snapshot.BgmPropertyEntries[nameId] = entry;
            }

            return skippedToneIds;
        }

        private void RemoveSkippedBgmEntries(ResourceSnapshot snapshot, HashSet<string> skippedToneIds)
        {
            if (skippedToneIds == null || skippedToneIds.Count == 0)
                return;

            //get ui_bgm_ IDs for skipped tone IDs
            var skippedUiBgmIds = skippedToneIds
                .Select(p => $"{MusicConstants.InternalIds.UI_BGM_ID_PREFIX}{p}")
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            //remove from snapshot
            foreach (var toneId in skippedToneIds)
            {
                snapshot.BgmPropertyEntries.Remove(toneId);
                snapshot.BgmDbRootEntries.Remove($"{MusicConstants.InternalIds.UI_BGM_ID_PREFIX}{toneId}");
                snapshot.StreamSetEntries.Remove($"{MusicConstants.InternalIds.STREAM_SET_PREFIX}{toneId}");
                snapshot.AssignedInfoEntries.Remove($"{MusicConstants.InternalIds.INFO_ID_PREFIX}{toneId}");
                snapshot.StreamPropertyEntries.Remove($"{MusicConstants.InternalIds.STREAM_PREFIX}{toneId}");
            }

            //remove from playlists
            foreach (var playlist in snapshot.PlaylistEntries.Values)
                playlist.Tracks.RemoveAll(p => skippedUiBgmIds.Contains(p.UiBgmId));
        }

        private void LoadGameEntries(
            ResourceSnapshot snapshot,
            PrcUiGameTitleDatabase gameTitleDb,
            Dictionary<string, MsbtDatabase> titleMsbts,
            Dictionary<string, string> gameTitleIds,
            Dictionary<string, string> seriesIds)
        {
            foreach (var value in gameTitleDb.DbRootEntries.Values)
            {
                value.UiGameTitleId = ResolveKnownId(value.UiGameTitleId, gameTitleIds);
                value.UiSeriesId = ResolveKnownId(value.UiSeriesId, seriesIds);

                var entry = _mapper.Map(value, new GameTitleEntry(value.UiGameTitleId));
                FillTitleMsbt(entry.MSBTTitle, entry.MSBTTitleKey, titleMsbts);
                snapshot.GameTitleEntries.Add(entry.UiGameTitleId, entry);
            }
        }

        private void LoadSeriesEntries(
            ResourceSnapshot snapshot,
            ReversePrcUiSeriesDatabase seriesDb,
            Dictionary<string, MsbtDatabase> titleMsbts,
            Dictionary<string, string> seriesIds)
        {
            foreach (var value in seriesDb.DbRootEntries.Values)
            {
                value.UiSeriesId = ResolveKnownId(value.UiSeriesId, seriesIds);

                //can't map directly because of old Sma5hMusic bug
                //saveno was saved as short instead of sbyte
                //workaround: convert to sbyte when mapping
                var entry = new SeriesEntry(value.UiSeriesId)
                {
                    NameId = value.NameId,
                    DispOrder = value.DispOrder,
                    DispOrderSound = value.DispOrderSound,
                    SaveNo = ToSignedByte(value.SaveNo),
                    Unk1 = value.Unk1,
                    IsDlc = value.IsDlc,
                    IsPatch = value.IsPatch,
                    DlcCharaId = value.DlcCharaId,
                    IsUseAmiiboBg = value.IsUseAmiiboBg
                };
                FillTitleMsbt(entry.MSBTTitle, entry.MSBTTitleKey, titleMsbts);
                snapshot.SeriesEntries.Add(entry.UiSeriesId, entry);
            }
        }

        private Dictionary<string, string> LoadPlaylistEntries(
            ResourceSnapshot snapshot,
            PrcUiBgmDatabase bgmDb,
            List<string> toneIds,
            Dictionary<string, string> playlistIdHints)
        {
            var playlistIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var usedPlaylistIds = new HashSet<string>(
                bgmDb.PlaylistEntries
                    .Select(p => p.Id)
                    .Where(p => !IsHexId(p)),
                StringComparer.OrdinalIgnoreCase);
            var unknownPlaylistIndex = 1;

            foreach (var value in bgmDb.PlaylistEntries)
            {
                //resolve hashed playlist IDs using stage names
                var playlistId = ResolvePlaylistId(value.Id, playlistIds, playlistIdHints, usedPlaylistIds, ref unknownPlaylistIndex);
                var playlist = new PlaylistEntry(playlistId);
                foreach (var track in value.Values)
                {
                    track.UiBgmId = ResolveGeneratedId(track.UiBgmId, toneIds, MusicConstants.InternalIds.UI_BGM_ID_PREFIX);
                    playlist.Tracks.Add(_mapper.Map<Models.PlaylistEntryModels.PlaylistValueEntry>(track));
                }

                snapshot.PlaylistEntries.Add(playlist.Id, playlist);
            }

            return playlistIds;
        }

        //get custom playlist ID from stage
        private static Dictionary<string, string> BuildStagePlaylistIdHints(PrcUiStageDatabase stageDb)
        {
            var output = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var value in stageDb.DbRootEntries.Values)
            {
                var bgmSetId = value.BgmSetId;
                if (string.IsNullOrEmpty(bgmSetId) || !IsHexId(bgmSetId) || output.ContainsKey(bgmSetId))
                    continue;

                var playlistId = CreatePlaylistIdFromStageId(value.UiStageId);
                if (!string.IsNullOrEmpty(playlistId))
                    output[bgmSetId] = playlistId;
            }

            return output;
        }

        private static string CreatePlaylistIdFromStageId(string stageId)
        {
            if (string.IsNullOrWhiteSpace(stageId) || IsHexId(stageId))
                return null;

            const string stagePrefix = "ui_stage_";
            var name = stageId.StartsWith(stagePrefix, StringComparison.OrdinalIgnoreCase)
                ? stageId.Substring(stagePrefix.Length)
                : stageId;

            name = name.Replace("_", string.Empty);
            return string.IsNullOrWhiteSpace(name) ? null : $"bgm{name}";
        }

        private void LoadStageEntries(ResourceSnapshot snapshot, PrcUiStageDatabase stageDb, Dictionary<string, string> seriesIds, Dictionary<string, string> playlistIds)
        {
            foreach (var value in stageDb.DbRootEntries.Values)
            {
                //resolve generated series and playlist IDs
                value.UiSeriesId = ResolveKnownId(value.UiSeriesId, seriesIds);
                value.BgmSetId = ResolveKnownId(value.BgmSetId, playlistIds);
                var entry = _mapper.Map<StageEntry>(value);
                snapshot.StageEntries.Add(entry.UiStageId, entry);
            }
        }

        private static void EnsureSnapshotFileExists(string file)
        {
            if (!File.Exists(file))
                throw new FileNotFoundException($"Required music resource file was not found: {file}", file);
        }

        private string ResolveSnapshotFilePath(string rootPath, string relativePath, string fallbackRootPath)
        {
            //if it exists, read from the build output
            var path = Path.Combine(rootPath, relativePath);
            if (File.Exists(path))
            {
                if (!string.IsNullOrWhiteSpace(fallbackRootPath))
                    _logger.LogInformation("Reverse MusicMod: loaded {RelativePath} from output: {Path}.", relativePath, path);
                return path;
            }

            //else read from the core resources
            if (!string.IsNullOrWhiteSpace(fallbackRootPath))
            {
                var fallbackPath = Path.Combine(fallbackRootPath, relativePath);
                if (File.Exists(fallbackPath))
                {
                    _logger.LogInformation("Reverse MusicMod: loaded {RelativePath} from fallback: {Path}.", relativePath, fallbackPath);
                    return fallbackPath;
                }
            }

            throw new FileNotFoundException($"Required music resource file was not found: {path}", path);
        }

        private Dictionary<string, MsbtDatabase> LoadMsbtDatabases(string rootPath, string resourcePattern, string fallbackRootPath = null)
        {
            var output = new Dictionary<string, MsbtDatabase>();
            foreach (var locale in LocaleHelper.ValidLocales)
            {
                var file = Path.Combine(rootPath, string.Format(resourcePattern, locale));
                //if only msg_title or msg_bgm exist, use them for every locale
                if (!File.Exists(file))
                {
                    var defaultFile = Path.Combine(rootPath, string.Format(resourcePattern, string.Empty).Replace("+.msbt", ".msbt"));
                    if (File.Exists(defaultFile))
                        file = defaultFile;
                }

                //use core fallback if either file is missing in the build output
                if (!File.Exists(file) && !string.IsNullOrWhiteSpace(fallbackRootPath))
                    file = Path.Combine(fallbackRootPath, string.Format(resourcePattern, locale));

                if (File.Exists(file))
                {
                    if (!string.IsNullOrWhiteSpace(fallbackRootPath))
                    {
                        var source = file.StartsWith(fallbackRootPath, StringComparison.OrdinalIgnoreCase) ? "fallback" : "output";
                        _logger.LogInformation("Reverse MusicMod: loaded {ResourcePattern} locale {Locale} from {Source}: {Path}.", resourcePattern, locale, source, file);
                    }
                    output.Add(locale, _msbtProvider.ReadFile<MsbtDatabase>(file));
                }
            }
            return output;
        }

        private static List<string> GetToneIds(string rootPath, BinBgmProperty bgmProperty)
        {
            var toneIds = new HashSet<string>(bgmProperty?.Entries?.Keys.Where(p => !string.IsNullOrEmpty(p)) ?? Enumerable.Empty<string>());
            var bgmPath = Path.Combine(rootPath, "stream;", "sound", "bgm");
            if (Directory.Exists(bgmPath))
            {
                //recover tone IDs from nus3audio files
                foreach (var file in Directory.EnumerateFiles(bgmPath, "*.nus3audio"))
                {
                    var name = Path.GetFileNameWithoutExtension(file);
                    if (name.StartsWith(MusicConstants.InternalIds.NUS3AUDIO_FILE_PREFIX, StringComparison.OrdinalIgnoreCase))
                        toneIds.Add(name.Substring(MusicConstants.InternalIds.NUS3AUDIO_FILE_PREFIX.Length));
                }
            }

            return toneIds.ToList();
        }

        private static void FillBgmMsbt(BgmDbRootEntry entry, Dictionary<string, MsbtDatabase> msbts)
        {
            if (string.IsNullOrEmpty(entry.NameId))
                return;

            //get bgm title, author and copyright
            foreach (var msbt in msbts)
            {
                AddMsbtValue(entry.Title, msbt.Key, msbt.Value, entry.TitleKey, ConvertFromGameTextTag);
                AddMsbtValue(entry.Author, msbt.Key, msbt.Value, entry.AuthorKey);
                AddMsbtValue(entry.Copyright, msbt.Key, msbt.Value, entry.CopyrightKey);
            }
        }

        private static void FillTitleMsbt(Dictionary<string, string> target, string key, Dictionary<string, MsbtDatabase> msbts)
        {
            if (string.IsNullOrEmpty(key))
                return;

            //get series or game title
            foreach (var msbt in msbts)
                AddMsbtValue(target, msbt.Key, msbt.Value, key);
        }

        private static void AddMsbtValue(Dictionary<string, string> target, string locale, MsbtDatabase database, string key, Func<string, string> converter = null)
        {
            if (database?.Entries != null && database.Entries.TryGetValue(key, out var value))
                target[locale] = converter == null ? value : converter(value);
        }

        private static string ConvertFromGameTextTag(string input)
        {
            return input.Replace("\u000e\u0000\u0002\u0002P", "{{").Replace("\u000e\u0000\u0002\u0002d", "}}");
        }

        private static string ResolveGeneratedId(string value, IEnumerable<string> toneIds, string prefix)
        {
            if (string.IsNullOrEmpty(value) || !value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                return value;

            foreach (var toneId in toneIds)
            {
                var candidate = prefix + toneId;
                if (Hash40Equals(value, candidate))
                    return candidate;
            }

            return value;
        }

        private static void AddGeneratedId(IDictionary<string, string> output, string value, string prefix, string nameId)
        {
            if (string.IsNullOrEmpty(value) || string.IsNullOrEmpty(nameId))
                return;

            //store generated IDs
            var candidate = prefix + nameId;
            if (value.Equals(candidate, StringComparison.OrdinalIgnoreCase) || Hash40Matches(value, candidate))
                output[value] = candidate;
        }

        private static string ResolveKnownId(string value, IDictionary<string, string> knownIds)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            return knownIds.TryGetValue(value, out var resolved) ? resolved : value;
        }

        private static string ResolvePlaylistId(
            string value,
            IDictionary<string, string> playlistIds,
            IDictionary<string, string> playlistIdHints,
            ISet<string> usedPlaylistIds,
            ref int unknownPlaylistIndex)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            if (!IsHexId(value))
            {
                //already readable playlist ID from PRC
                playlistIds[value] = value;
                usedPlaylistIds.Add(value);
                return value;
            }

            if (playlistIds.TryGetValue(value, out var knownPlaylistId))
                return knownPlaylistId;

            if (playlistIdHints != null && playlistIdHints.TryGetValue(value, out var hintedPlaylistId) && !usedPlaylistIds.Contains(hintedPlaylistId))
            {
                //use stage name for Playlist ID
                playlistIds[value] = hintedPlaylistId;
                usedPlaylistIds.Add(hintedPlaylistId);
                return hintedPlaylistId;
            }

            //fallback when no playlist ID can be recovered
            string playlistId;
            do
            {
                playlistId = $"bgmunknown{unknownPlaylistIndex}";
                unknownPlaylistIndex++;
            }
            while (usedPlaylistIds.Contains(playlistId));

            playlistIds[value] = playlistId;
            usedPlaylistIds.Add(playlistId);
            return playlistId;
        }

        private static bool IsHexId(string value)
        {
            return value != null && value.StartsWith("0x", StringComparison.OrdinalIgnoreCase);
        }

        private static bool Hash40Equals(string hashValue, string candidate)
        {
            return Convert.ToUInt64(hashValue, 16) == Hash40Util.StringToHash40(candidate);
        }

        private static bool Hash40Matches(string value, string candidate)
        {
            return value.StartsWith("0x", StringComparison.OrdinalIgnoreCase) && Hash40Equals(value, candidate);
        }

        private static sbyte ToSignedByte(short value)
        {
            if (value >= sbyte.MinValue && value <= sbyte.MaxValue)
                return (sbyte)value;

            if (value >= byte.MinValue && value <= byte.MaxValue)
                return unchecked((sbyte)(byte)value);

            if (value < sbyte.MinValue)
                return sbyte.MinValue;

            return sbyte.MaxValue;
        }

        //determine if music pack was made using old Sma5hMusic or Extra
        private ReversePrcUiSeriesDatabase ReadSeriesDatabase(string seriesDbPath)
        {
            if (HasShortSeriesSaveNo(seriesDbPath))
                return _prcProvider.ReadFile<ReversePrcUiSeriesDatabase>(seriesDbPath);

            var seriesDb = _prcProvider.ReadFile<PrcUiSeriesDatabase>(seriesDbPath);
            if (seriesDb == null)
                return null;

            return new ReversePrcUiSeriesDatabase
            {
                DbRootEntries = seriesDb.DbRootEntries.ToDictionary(p => p.Key, p => new ReversePrcSeriesDbRootEntry
                {
                    UiSeriesId = p.Value.UiSeriesId,
                    NameId = p.Value.NameId,
                    DispOrder = p.Value.DispOrder,
                    DispOrderSound = p.Value.DispOrderSound,
                    SaveNo = p.Value.SaveNo,
                    Unk1 = p.Value.Unk1,
                    IsDlc = p.Value.IsDlc,
                    IsPatch = p.Value.IsPatch,
                    DlcCharaId = p.Value.DlcCharaId,
                    IsUseAmiiboBg = p.Value.IsUseAmiiboBg
                })
            };
        }

        private static bool HasShortSeriesSaveNo(string seriesDbPath)
        {
            var paramFile = new ParamFile();
            paramFile.Open(seriesDbPath);

            var dbRoot = (ParamList)paramFile.Root.Nodes[Hash40Util.StringToHash40("db_root")];
            var saveNoHash = Hash40Util.StringToHash40("save_no");
            return dbRoot.Nodes
                .OfType<ParamStruct>()
                .Any(p => ((ParamValue)p.Nodes[saveNoHash]).TypeKey == ParamType.@short);
        }

        private class ReversePrcUiSeriesDatabase : IStateManagerDb
        {
            [PrcDictionary("ui_series_id")]
            [PrcHexMapping("db_root")]
            public Dictionary<string, ReversePrcSeriesDbRootEntry> DbRootEntries { get; set; }
        }

        private class ReversePrcSeriesDbRootEntry
        {
            [PrcHexMapping("ui_series_id", true)]
            public string UiSeriesId { get; set; }
            [PrcHexMapping("name_id")]
            public string NameId { get; set; }
            [PrcHexMapping("disp_order")]
            public sbyte DispOrder { get; set; }
            [PrcHexMapping("disp_order_sound")]
            public sbyte DispOrderSound { get; set; }
            [PrcHexMapping("save_no")]
            public short SaveNo { get; set; }
            [PrcHexMapping(0x1c38302364)]
            public bool Unk1 { get; set; }
            [PrcHexMapping("is_dlc")]
            public bool IsDlc { get; set; }
            [PrcHexMapping("is_patch")]
            public bool IsPatch { get; set; }
            [PrcHexMapping("dlc_chara_id", true)]
            public string DlcCharaId { get; set; }
            [PrcHexMapping("is_use_amiibo_bg")]
            public bool IsUseAmiiboBg { get; set; }
        }

        private class ResourceSnapshot
        {
            public Dictionary<string, BgmDbRootEntry> BgmDbRootEntries { get; } = new Dictionary<string, BgmDbRootEntry>();
            public Dictionary<string, BgmStreamSetEntry> StreamSetEntries { get; } = new Dictionary<string, BgmStreamSetEntry>();
            public Dictionary<string, BgmAssignedInfoEntry> AssignedInfoEntries { get; } = new Dictionary<string, BgmAssignedInfoEntry>();
            public Dictionary<string, BgmStreamPropertyEntry> StreamPropertyEntries { get; } = new Dictionary<string, BgmStreamPropertyEntry>();
            public Dictionary<string, BgmPropertyEntry> BgmPropertyEntries { get; } = new Dictionary<string, BgmPropertyEntry>();
            public Dictionary<string, GameTitleEntry> GameTitleEntries { get; } = new Dictionary<string, GameTitleEntry>();
            public Dictionary<string, SeriesEntry> SeriesEntries { get; } = new Dictionary<string, SeriesEntry>();
            public Dictionary<string, PlaylistEntry> PlaylistEntries { get; } = new Dictionary<string, PlaylistEntry>();
            public Dictionary<string, StageEntry> StageEntries { get; } = new Dictionary<string, StageEntry>();
        }
    }
}
