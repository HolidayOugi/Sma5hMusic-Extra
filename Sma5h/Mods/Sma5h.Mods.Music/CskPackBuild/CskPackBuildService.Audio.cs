using Microsoft.Extensions.Logging;
using Sma5h.Mods.Music.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Sma5h.Mods.Music.CskPackBuild
{
    public partial class CskPackBuildService
    {
        #region Types

        private sealed class BgmBuildEntry
        {
            public string NameId { get; init; }
            public float AudioVolume { get; init; }
            public string Filename { get; init; }
            public bool BankOnly { get; init; }
        }

        #endregion

        #region BGM Files

        private string GenerateBgmFiles(IEnumerable<CskModContext> contexts, string tempRoot, HashSet<string> selectedSeriesKeys, CskBuildState state)
        {
            ClearDirectory(tempRoot);
            var outputFolder = Path.Combine(tempRoot, "stream;", "sound", "bgm");
            Directory.CreateDirectory(outputFolder);
            //reset nus3bank ids to fix bug with high id numbers
            _nus3AudioService.ResetGeneratedNus3BankIds();

            var selectedSeriesIds = contexts
                .SelectMany(context => context.SeriesList
                    .Where(series => selectedSeriesKeys.Contains(CreateSeriesKey(context.Mod, series)))
                    .Select(series => series.UiSeriesId))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var selectedSeriesNames = selectedSeriesIds
                .Select(id => state.Series.TryGetValue(id, out var series) ? series.NameId : null)
                .Where(name => !string.IsNullOrEmpty(name))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var bgm in contexts.SelectMany(context => context.OrderedSeries)
                .SelectMany(series => series.Games)
                .SelectMany(game => game.Bgms)
                .Where(bgm => state.OverriddenCoreBgmIds.Contains(bgm.Database.UiBgmId) && bgm.Property != null && !File.Exists(bgm.Property.Filename)))
                _unavailableBgmNameIds.Value?.Add(bgm.Property.NameId);
            //get all bgm entries for the selected series plus volume overrides for core songs
            var entries = contexts
                .SelectMany(context => GetSelectedBgmBuildEntries(context, selectedSeriesKeys, state))
                .Concat(GetSelectedCoreVolumeEntries(selectedSeriesNames, state))
                .Where(entry => !string.IsNullOrEmpty(entry.NameId) && (entry.BankOnly || !string.IsNullOrEmpty(entry.Filename)))
                .GroupBy(entry => entry.NameId, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();

            _logger.LogInformation("Generating {Count} nus3audio/nus3bank file(s) for CSK packs.", entries.Count);
            foreach (var entry in entries)
            {
                var bankFile = Path.Combine(outputFolder, $"bgm_{entry.NameId}.nus3bank");
                var audioFile = Path.Combine(outputFolder, $"bgm_{entry.NameId}.nus3audio");
                //skip is song not found 
                if (!entry.BankOnly && !File.Exists(entry.Filename))
                {
                    _unavailableBgmNameIds.Value?.Add(entry.NameId);
                    _logger.LogWarning("[CSK] Skipping song {NameId}: source file {Filename} was not found.", entry.NameId, entry.Filename);
                    continue;
                }

                _logger.LogInformation("Generating Nus3Bank for {NameId} with volume {Volume}", entry.NameId, entry.AudioVolume);
                _nus3AudioService.GenerateNus3Bank(entry.NameId, entry.AudioVolume, bankFile);
                if (entry.BankOnly)
                    continue;
                if (File.Exists(audioFile))
                    File.Delete(audioFile);
                _logger.LogInformation("Generating or copying Nus3Audio for {NameId}", entry.NameId);
                if (!_nus3AudioService.GenerateNus3Audio(entry.NameId, entry.Filename, audioFile) || !File.Exists(audioFile))
                {
                    _unavailableBgmNameIds.Value?.Add(entry.NameId);
                    DeleteIfExists(bankFile);
                    DeleteIfExists(audioFile);
                    _logger.LogWarning("[CSK] Skipping song {NameId}: source file {Filename} could not be processed.", entry.NameId, entry.Filename);
                }
            }
            return outputFolder;
        }

        private IEnumerable<BgmBuildEntry> GetSelectedBgmBuildEntries(CskModContext context, HashSet<string> selectedSeriesKeys, CskBuildState state)
        {
            foreach (var series in context.SeriesList.Where(series => selectedSeriesKeys.Contains(CreateSeriesKey(context.Mod, series))))
            {
                var directBgms = context.OrderedSeries
                    .Where(item => string.Equals(item.Series.UiSeriesId, series.UiSeriesId, StringComparison.OrdinalIgnoreCase))
                    .SelectMany(item => item.Games)
                    .SelectMany(game => game.Bgms);
                var movedBgms = GetContextGamesForSeries(context, series.UiSeriesId, state)
                    .SelectMany(game => game.Bgms);
                foreach (var bgm in directBgms.Concat(movedBgms)
                    .Where(item => item.Property != null)
                    .GroupBy(item => item.Property.NameId, StringComparer.OrdinalIgnoreCase)
                    .Select(group => group.First()))
                {
                    var property = bgm.Property;
                    yield return new BgmBuildEntry
                    {
                        NameId = property.NameId,
                        Filename = property.Filename,
                        AudioVolume = property.AudioVolume
                    };
                }
            }
        }

        private IEnumerable<BgmBuildEntry> GetSelectedCoreVolumeEntries(HashSet<string> selectedSeriesNames, CskBuildState state)
        {
            if (_config.CurrentValue.Sma5hMusicGUI?.BuildNus3bankForCoreSongs != true)
                yield break;
            foreach (var entry in state.CoreVolumeChanges)
            {
                if (!string.IsNullOrEmpty(entry.SeriesName) && !selectedSeriesNames.Contains(entry.SeriesName))
                    continue;
                yield return new BgmBuildEntry { NameId = entry.NameId, AudioVolume = entry.Volume, BankOnly = true };
            }
        }

        private static IEnumerable<BgmPropertyEntry> GetBgmProperties(BgmDbRootEntry db, CskBuildState state)
        {
            if (!state.StreamSets.TryGetValue(db.StreamSetId ?? string.Empty, out var streamSet))
                yield break;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var infoId in GetInfoIds(streamSet).Where(id => !string.IsNullOrEmpty(id)))
            {
                if (!state.AssignedInfos.TryGetValue(infoId, out var assigned) ||
                    !state.StreamProperties.TryGetValue(assigned.StreamId ?? string.Empty, out var streamProperty))
                    continue;
                foreach (var nameId in GetDataNames(streamProperty).Where(id => !string.IsNullOrEmpty(id)))
                    if (seen.Add(nameId) && state.BgmProperties.TryGetValue(nameId, out var property))
                        yield return property;
            }
        }

        private static void DeleteIfExists(string path)
        {
            if (File.Exists(path))
                File.Delete(path);
        }

        #endregion
    }
}
