using Microsoft.Extensions.Logging;
using Sma5h.Mods.Music.Helpers;
using Sma5h.Mods.Music.MusicMods.MusicModModels;
using Sma5h.Mods.Music.MusicOverride.MusicOverrideConfigModels;
using Sma5h.Mods.Music.Models;
using System.Collections.Generic;
using System.IO;

namespace Sma5h.Mods.Music.ReverseBuild
{
    public partial class MusicModReverseService
    {
        private void GenerateCoreBgmOverride(ResourceSnapshot core, ResourceSnapshot output, string overrideOutputPath)
        {
            var newValues = new CoreBgmOverrides();

            foreach (var outputDbRoot in output.BgmDbRootEntries)
            {
                if (!core.BgmDbRootEntries.ContainsKey(outputDbRoot.Key))
                    continue;
                //if there are changes to core songs, add them to override
                AddCoreBgmOverrideIfChanged(core, output, outputDbRoot.Key, newValues);
            }

            if (!HasCoreBgmValues(newValues))
                return;

            var path = Path.Combine(overrideOutputPath, MusicConstants.MusicModFiles.MUSIC_OVERRIDE_CORE_BGM_JSON_FILE);
            var merged = LoadCoreBgmOverrides(path);
            //merge new values into existing override
            MergeCoreBgmOverrides(merged, newValues);
            WriteJson(path, merged);
            _logger.LogInformation("Reverse MusicMod: wrote {OverridePath}.", path);
        }

        private void AddCoreBgmOverrideIfChanged(ResourceSnapshot core, ResourceSnapshot output, string uiBgmId, CoreBgmOverrides newValues)
        {
            var outputDbRoot = output.BgmDbRootEntries[uiBgmId];
            var outputStreamSet = output.StreamSetEntries.GetValueOrDefault(outputDbRoot.StreamSetId);
            var outputInfoId = GetFirstInfoId(outputStreamSet);
            var outputAssignedInfo = outputInfoId != null ? output.AssignedInfoEntries.GetValueOrDefault(outputInfoId) : null;
            var outputStreamProperty = outputAssignedInfo != null ? output.StreamPropertyEntries.GetValueOrDefault(outputAssignedInfo.StreamId) : null;
            var outputToneId = GetToneId(outputStreamProperty, outputDbRoot);
            var outputBgmProperty = outputToneId != null ? output.BgmPropertyEntries.GetValueOrDefault(outputToneId) : null;

            if (outputStreamSet == null || outputAssignedInfo == null || outputStreamProperty == null || outputBgmProperty == null)
                return;

            var dbRootChanged = IsCoreBgmConfigChanged<BgmDbRootEntry, BgmDbRootConfig>(core.BgmDbRootEntries, uiBgmId, outputDbRoot);
            var streamSetChanged = IsCoreBgmConfigChanged<BgmStreamSetEntry, BgmStreamSetConfig>(core.StreamSetEntries, outputStreamSet.StreamSetId, outputStreamSet);
            var assignedInfoChanged = IsCoreBgmConfigChanged<BgmAssignedInfoEntry, BgmAssignedInfoConfig>(core.AssignedInfoEntries, outputAssignedInfo.InfoId, outputAssignedInfo);
            var streamPropertyChanged = IsCoreBgmConfigChanged<BgmStreamPropertyEntry, BgmStreamPropertyConfig>(core.StreamPropertyEntries, outputStreamProperty.StreamId, outputStreamProperty);
            var bgmPropertyChanged = IsCoreBgmConfigChanged<BgmPropertyEntry, BgmPropertyEntryConfig>(core.BgmPropertyEntries, outputBgmProperty.NameId, outputBgmProperty);

            if (!dbRootChanged && !streamSetChanged && !assignedInfoChanged && !streamPropertyChanged && !bgmPropertyChanged)
                return;

            newValues.CoreBgmDbRootOverrides[uiBgmId] = _mapper.Map<BgmDbRootConfig>(outputDbRoot);
            newValues.CoreBgmStreamSetOverrides[outputStreamSet.StreamSetId] = _mapper.Map<BgmStreamSetConfig>(outputStreamSet);
            newValues.CoreBgmAssignedInfoOverrides[outputAssignedInfo.InfoId] = _mapper.Map<BgmAssignedInfoConfig>(outputAssignedInfo);
            newValues.CoreBgmStreamPropertyOverrides[outputStreamProperty.StreamId] = _mapper.Map<BgmStreamPropertyConfig>(outputStreamProperty);
            newValues.CoreBgmPropertyOverrides[outputBgmProperty.NameId] = _mapper.Map<BgmPropertyEntryConfig>(outputBgmProperty);
        }

        private bool IsCoreBgmConfigChanged<TEntry, TConfig>(Dictionary<string, TEntry> coreEntries, string id, TEntry outputEntry)
        {
            if (!coreEntries.TryGetValue(id, out var coreEntry))
                return true;

            return !JsonEquals(_mapper.Map<TConfig>(coreEntry), _mapper.Map<TConfig>(outputEntry));
        }

        private static CoreBgmOverrides LoadCoreBgmOverrides(string path)
        {
            if (!File.Exists(path))
                return new CoreBgmOverrides();

            return Newtonsoft.Json.JsonConvert.DeserializeObject<CoreBgmOverrides>(File.ReadAllText(path)) ?? new CoreBgmOverrides();
        }

        private static void MergeCoreBgmOverrides(CoreBgmOverrides target, CoreBgmOverrides source)
        {
            EnsureCoreBgmOverrides(target);
            EnsureCoreBgmOverrides(source);

            foreach (var value in source.CoreBgmDbRootOverrides)
                target.CoreBgmDbRootOverrides[value.Key] = value.Value;
            foreach (var value in source.CoreBgmStreamSetOverrides)
                target.CoreBgmStreamSetOverrides[value.Key] = value.Value;
            foreach (var value in source.CoreBgmAssignedInfoOverrides)
                target.CoreBgmAssignedInfoOverrides[value.Key] = value.Value;
            foreach (var value in source.CoreBgmStreamPropertyOverrides)
                target.CoreBgmStreamPropertyOverrides[value.Key] = value.Value;
            foreach (var value in source.CoreBgmPropertyOverrides)
                target.CoreBgmPropertyOverrides[value.Key] = value.Value;
        }

        private static bool HasCoreBgmValues(CoreBgmOverrides values)
        {
            EnsureCoreBgmOverrides(values);
            return values.CoreBgmDbRootOverrides.Count > 0 ||
                   values.CoreBgmStreamSetOverrides.Count > 0 ||
                   values.CoreBgmAssignedInfoOverrides.Count > 0 ||
                   values.CoreBgmStreamPropertyOverrides.Count > 0 ||
                   values.CoreBgmPropertyOverrides.Count > 0;
        }

        private static void EnsureCoreBgmOverrides(CoreBgmOverrides values)
        {
            values.CoreBgmDbRootOverrides ??= new Dictionary<string, BgmDbRootConfig>();
            values.CoreBgmStreamSetOverrides ??= new Dictionary<string, BgmStreamSetConfig>();
            values.CoreBgmAssignedInfoOverrides ??= new Dictionary<string, BgmAssignedInfoConfig>();
            values.CoreBgmStreamPropertyOverrides ??= new Dictionary<string, BgmStreamPropertyConfig>();
            values.CoreBgmPropertyOverrides ??= new Dictionary<string, BgmPropertyEntryConfig>();
        }
    }
}
