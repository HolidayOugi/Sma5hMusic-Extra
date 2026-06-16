using Microsoft.Extensions.Logging;
using Sma5h.Mods.Music.Helpers;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Sma5h.Mods.Music.ReverseBuild
{
    public partial class MusicModReverseService
    {
        private void GenerateOrderOverride(ResourceSnapshot core, ResourceSnapshot output, string overrideOutputPath)
        {
            var newValues = output.BgmDbRootEntries
                .ToDictionary(p => p.Key, p => p.Value.TestDispOrder);

            var path = Path.Combine(overrideOutputPath, MusicConstants.MusicModFiles.MUSIC_OVERRIDE_ORDER_JSON_FILE);
            var merged = MergeOrderOverride(path, newValues);
            WriteJson(path, merged);

            if (newValues.Count > 0)
                _logger.LogInformation("Reverse MusicMod: wrote {OverridePath}.", path);
        }

        private Dictionary<string, short> MergeOrderOverride(string path, Dictionary<string, short> newValues)
        {
            var existingValues = LoadDictionary<short>(path);
            if (existingValues.Count == 0)
                return newValues;

            var merged = new Dictionary<string, short>(newValues);
            var usedOrders = new HashSet<short>(newValues.Values.Where(p => p >= 0));
            var existingOnlyWithConflictingOrder = new List<KeyValuePair<string, short>>();

            foreach (var existingValue in existingValues)
            {
                if (newValues.ContainsKey(existingValue.Key))
                    continue;

                if (existingValue.Value >= 0 && usedOrders.Contains(existingValue.Value))
                {
                    existingOnlyWithConflictingOrder.Add(existingValue);
                    continue;
                }

                merged[existingValue.Key] = existingValue.Value;
                if (existingValue.Value >= 0)
                    usedOrders.Add(existingValue.Value);
            }

            var nextOrder = usedOrders.Count == 0 ? 0 : usedOrders.Max() + 1;
            foreach (var existingValue in existingOnlyWithConflictingOrder)
            {
                merged[existingValue.Key] = (short)nextOrder;
                nextOrder++;
            }

            return merged;
        }
    }
}
