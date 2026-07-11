using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Sma5h.Mods.Music;
using Sma5h.Mods.Music.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Sma5h.Mods.Music.CskPackBuild
{
    public partial class CskPackBuildService
    {
        #region Types

        private class BgmBuildEntry
        {
            public string NameId { get; set; }
            public float AudioVolume { get; set; }
            public string Filename { get; set; }
            public bool BankOnly { get; set; }
        }

        #endregion

        #region BGM Files

        private string GenerateBgmFiles(IEnumerable<CskModContext> contexts, string tempRoot, HashSet<string> selectedSeriesKeys, CskBuildResources buildResources)
        {
            ClearDirectory(tempRoot);

            var outputBgmFolder = Path.Combine(tempRoot, "stream;", "sound", "bgm");
            Directory.CreateDirectory(outputBgmFolder);

            //reset nus3bank ids to fix bug with high id numbers
            _nus3AudioService.ResetGeneratedNus3BankIds();

            var contextList = contexts.ToList();
            var selectedSeriesNames = GetSelectedSeriesNames(contextList, selectedSeriesKeys);
            //get all bgm entries for the selected series plus volume overrides for core songs
            var bgmEntries = contextList
                .SelectMany(context => GetSelectedBgmBuildEntries(context, selectedSeriesKeys, buildResources.CoreGameOverride))
                .Concat(GetSelectedCoreVolumeOverrideBuildEntries(selectedSeriesNames, buildResources))
                .Where(p => !string.IsNullOrEmpty(p.NameId) && (p.BankOnly || !string.IsNullOrEmpty(p.Filename)))
                .GroupBy(p => p.NameId, StringComparer.OrdinalIgnoreCase)
                .Select(p => p.First())
                .ToList();

            _logger.LogInformation("Generating {Count} nus3audio/nus3bank file(s) for CSK packs.", bgmEntries.Count);

            foreach (var bgmPropertyEntry in bgmEntries)
            {
                var nusBankOutputFile = Path.Combine(outputBgmFolder, string.Format(MusicConstants.GameResources.NUS3BANK_FILE, bgmPropertyEntry.NameId));
                var nusAudioOutputFile = Path.Combine(outputBgmFolder, string.Format(MusicConstants.GameResources.NUS3AUDIO_FILE, bgmPropertyEntry.NameId));

                //skip is song not found 
                if (!bgmPropertyEntry.BankOnly && !File.Exists(bgmPropertyEntry.Filename))
                {
                    _unavailableBgmNameIds.Value?.Add(bgmPropertyEntry.NameId);
                    _logger.LogWarning(
                        "[CSK] Skipping song {NameId}: source file {Filename} was not found.",
                        bgmPropertyEntry.NameId,
                        bgmPropertyEntry.Filename);
                    continue;
                }

                _logger.LogInformation("Generating Nus3Bank for {NameId} with volume {Volume}", bgmPropertyEntry.NameId, bgmPropertyEntry.AudioVolume);
                _nus3AudioService.GenerateNus3Bank(bgmPropertyEntry.NameId, bgmPropertyEntry.AudioVolume, nusBankOutputFile);

                if (bgmPropertyEntry.BankOnly)
                    continue;

                if (File.Exists(nusAudioOutputFile))
                    File.Delete(nusAudioOutputFile);

                _logger.LogInformation("Generating or copying Nus3Audio for {NameId}", bgmPropertyEntry.NameId);
                if (!_nus3AudioService.GenerateNus3Audio(bgmPropertyEntry.NameId, bgmPropertyEntry.Filename, nusAudioOutputFile) ||
                    !File.Exists(nusAudioOutputFile))
                {
                    _unavailableBgmNameIds.Value?.Add(bgmPropertyEntry.NameId);
                    DeleteIfExists(nusBankOutputFile);
                    DeleteIfExists(nusAudioOutputFile);
                    _logger.LogWarning(
                        "[CSK] Skipping song {NameId}: source file {Filename} could not be processed.",
                        bgmPropertyEntry.NameId,
                        bgmPropertyEntry.Filename);
                }
            }

            return outputBgmFolder;
        }

        private static void DeleteIfExists(string path)
        {
            if (File.Exists(path))
                File.Delete(path);
        }

        private IEnumerable<BgmBuildEntry> GetSelectedBgmBuildEntries(CskModContext context, HashSet<string> selectedSeriesKeys, JObject coreGameOverride)
        {
            foreach (var series in context.SeriesList.Where(series => selectedSeriesKeys.Contains(CreateSeriesKey(context.Mod, series))))
            {
                foreach (JObject game in GetArray(series, "games"))
                {
                    foreach (JObject bgm in GetArray(game, "bgms"))
                        yield return CreateBgmBuildEntry(context.Mod.ModPath, bgm);
                }

                foreach (var movedGame in GetCoreGameMovedGames(series, context.Metadata, coreGameOverride))
                {
                    foreach (JObject bgm in GetArray(movedGame, "bgms"))
                        yield return CreateBgmBuildEntry(context.Mod.ModPath, bgm);
                }
            }
        }

        private HashSet<string> GetSelectedSeriesNames(IEnumerable<CskModContext> contexts, HashSet<string> selectedSeriesKeys)
        {
            return contexts
                .SelectMany(context => context.SeriesList.Where(series => selectedSeriesKeys.Contains(CreateSeriesKey(context.Mod, series))))
                .Select(series => GetString(series, "name_id"))
                .Where(p => !string.IsNullOrEmpty(p))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        private bool IsSelectedAudioOnlyBuild(IEnumerable<CskModContext> contexts, HashSet<string> selectedSeriesKeys, CskBuildResources buildResources)
        {
            var selectedBgms = GetSelectedBgms(contexts, selectedSeriesKeys).ToList();
            var selectedSeriesNames = GetSelectedSeriesNames(contexts, selectedSeriesKeys);
            var hasCoreVolumeOverrides = GetSelectedCoreVolumeOverrideBuildEntries(selectedSeriesNames, buildResources).Any();

            return selectedBgms.Count == 0 && hasCoreVolumeOverrides;
        }

        private IEnumerable<JObject> GetSelectedBgms(IEnumerable<CskModContext> contexts, HashSet<string> selectedSeriesKeys)
        {
            foreach (var context in contexts)
            {
                foreach (var series in context.SeriesList.Where(series => selectedSeriesKeys.Contains(CreateSeriesKey(context.Mod, series))))
                {
                    foreach (JObject game in GetArray(series, "games"))
                    {
                        foreach (JObject bgm in GetArray(game, "bgms"))
                            yield return bgm;
                    }
                }
            }
        }

        private IEnumerable<BgmBuildEntry> GetSelectedCoreVolumeOverrideBuildEntries(HashSet<string> selectedSeriesNames, CskBuildResources buildResources)
        {
            if (!ShouldBuildCoreNus3Banks())
                yield break;

            foreach (var entry in GetCoreVolumeOverrideEntries(buildResources))
            {
                if (!string.IsNullOrEmpty(entry.SeriesName) && !selectedSeriesNames.Contains(entry.SeriesName))
                    continue;

                yield return new BgmBuildEntry
                {
                    NameId = entry.NameId,
                    AudioVolume = entry.Volume,
                    BankOnly = true
                };
            }
        }

        private bool ShouldBuildCoreNus3Banks()
        {
            return _config.CurrentValue.Sma5hMusicGUI?.BuildNus3bankForCoreSongs == true;
        }

        private BgmBuildEntry CreateBgmBuildEntry(string modPath, JObject bgm)
        {
            var bgmProperty = bgm["bgm_properties"] as JObject;
            var nameId = GetString(bgmProperty, "name_id");
            var filename = GetString(bgm, "filename");

            return new BgmBuildEntry
            {
                NameId = nameId,
                Filename = Path.Combine(modPath, filename),
                AudioVolume = GetFloat(bgm["nus3bank_config"], "volume", 2.7f)
            };
        }

        #endregion

    }
}
