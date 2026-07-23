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
            //merge new values into existing override
            var merged = MergeOrderOverride(path, newValues);
            WriteJson(path, merged);

            if (newValues.Count > 0)
                _logger.LogInformation("Reverse MusicMod: wrote {OverridePath}.", path);
        }

        //Order from reverse mod is preserved, the songs already present are pushed to the end of the list
        private Dictionary<string, short> MergeOrderOverride(string path, Dictionary<string, short> newValues)
        {
            var existingValues = LoadDictionary<short>(path);
            if (existingValues.Count == 0)
                return newValues;

            var merged = new Dictionary<string, short>(newValues);

            foreach (var hiddenExistingValue in existingValues.Where(p => p.Value < 0 && !newValues.ContainsKey(p.Key)))
                merged[hiddenExistingValue.Key] = hiddenExistingValue.Value;

            var existingOnlyVisibleValues = existingValues
                .Where(p => p.Value >= 0 && !newValues.ContainsKey(p.Key))
                .OrderBy(p => p.Value)
                .ToList();

            var visibleNewOrders = newValues.Values.Where(p => p >= 0).ToList();
            var nextOrder = visibleNewOrders.Count == 0 ? 0 : visibleNewOrders.Max() + 1;
            foreach (var existingValue in existingOnlyVisibleValues)
            {
                merged[existingValue.Key] = (short)nextOrder;
                nextOrder++;
            }

            return merged;
        }
    }
}
