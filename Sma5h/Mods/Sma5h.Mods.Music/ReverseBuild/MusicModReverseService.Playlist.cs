using Microsoft.Extensions.Logging;
using Sma5h.Mods.Music.Helpers;
using Sma5h.Mods.Music.MusicOverride.MusicOverrideConfigModels;
using Sma5h.Mods.Music.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Sma5h.Mods.Music.ReverseBuild
{
    public partial class MusicModReverseService
    {
        private void GeneratePlaylistOverride(ResourceSnapshot core, ResourceSnapshot output, string overrideOutputPath)
        {
            var path = Path.Combine(overrideOutputPath, MusicConstants.MusicModFiles.MUSIC_OVERRIDE_PLAYLIST_JSON_FILE);
            var existingValues = LoadDictionary<PlaylistConfig>(path);
            var newValues = output.PlaylistEntries
                .ToDictionary(
                    p => p.Key,
                    p => CreatePlaylistConfig(p.Value, existingValues.GetValueOrDefault(p.Key)?.Title));

            var merged = MergePlaylistOverrides(existingValues, newValues);
            WriteJson(path, merged);

            if (newValues.Count > 0)
                _logger.LogInformation("Reverse MusicMod: wrote {OverridePath}.", path);
        }

        private PlaylistConfig CreatePlaylistConfig(PlaylistEntry playlistEntry, string existingTitle)
        {
            var playlistConfig = _mapper.Map<PlaylistConfig>(playlistEntry);
            if (string.IsNullOrWhiteSpace(playlistConfig.Title))
                playlistConfig.Title = string.IsNullOrWhiteSpace(existingTitle) ? playlistEntry.Id : existingTitle;

            return playlistConfig;
        }

        private static Dictionary<string, PlaylistConfig> MergePlaylistOverrides(Dictionary<string, PlaylistConfig> existingValues, Dictionary<string, PlaylistConfig> newValues)
        {
            var merged = new Dictionary<string, PlaylistConfig>(existingValues);
            foreach (var newValue in newValues)
            {
                if (existingValues.TryGetValue(newValue.Key, out var existingValue))
                    merged[newValue.Key] = MergePlaylistOverride(existingValue, newValue.Value);
                else
                    merged[newValue.Key] = newValue.Value;
            }

            return merged;
        }

        private static PlaylistConfig MergePlaylistOverride(PlaylistConfig existingValue, PlaylistConfig newValue)
        {
            newValue.Tracks ??= new List<PlaylistValueConfig>();
            if (existingValue?.Tracks == null || existingValue.Tracks.Count == 0)
                return newValue;

            var newTrackIds = new HashSet<string>(
                newValue.Tracks
                    .Select(p => p.UiBgmId)
                    .Where(p => !string.IsNullOrEmpty(p)),
                StringComparer.OrdinalIgnoreCase);

            var existingOnlyTracks = existingValue.Tracks
                .Where(p => !string.IsNullOrEmpty(p.UiBgmId) && !newTrackIds.Contains(p.UiBgmId))
                .ToList();

            if (existingOnlyTracks.Count == 0)
                return newValue;

            newValue.Tracks.AddRange(existingOnlyTracks);
            MoveConflictingPlaylistOrders(newValue.Tracks, existingOnlyTracks);
            return newValue;
        }

        private static void MoveConflictingPlaylistOrders(List<PlaylistValueConfig> mergedTracks, List<PlaylistValueConfig> existingOnlyTracks)
        {
            var orderAccessors = new (Func<PlaylistValueConfig, short> Get, Action<PlaylistValueConfig, short> Set)[]
            {
                (p => p.Order0, (p, value) => p.Order0 = value),
                (p => p.Order1, (p, value) => p.Order1 = value),
                (p => p.Order2, (p, value) => p.Order2 = value),
                (p => p.Order3, (p, value) => p.Order3 = value),
                (p => p.Order4, (p, value) => p.Order4 = value),
                (p => p.Order5, (p, value) => p.Order5 = value),
                (p => p.Order6, (p, value) => p.Order6 = value),
                (p => p.Order7, (p, value) => p.Order7 = value),
                (p => p.Order8, (p, value) => p.Order8 = value),
                (p => p.Order9, (p, value) => p.Order9 = value),
                (p => p.Order10, (p, value) => p.Order10 = value),
                (p => p.Order11, (p, value) => p.Order11 = value),
                (p => p.Order12, (p, value) => p.Order12 = value),
                (p => p.Order13, (p, value) => p.Order13 = value),
                (p => p.Order14, (p, value) => p.Order14 = value),
                (p => p.Order15, (p, value) => p.Order15 = value)
            };

            foreach (var orderAccessor in orderAccessors)
            {
                var usedOrders = new HashSet<short>(
                    mergedTracks
                        .Except(existingOnlyTracks)
                        .Select(orderAccessor.Get)
                        .Where(p => p >= 0));

                var conflictingTracks = new List<PlaylistValueConfig>();
                foreach (var track in existingOnlyTracks)
                {
                    var order = orderAccessor.Get(track);
                    if (order < 0)
                        continue;

                    if (usedOrders.Contains(order))
                    {
                        conflictingTracks.Add(track);
                        continue;
                    }

                    usedOrders.Add(order);
                }

                var nextOrder = usedOrders.Count == 0 ? 0 : usedOrders.Max() + 1;
                foreach (var track in conflictingTracks)
                {
                    orderAccessor.Set(track, (short)nextOrder);
                    nextOrder++;
                }
            }
        }
    }
}
