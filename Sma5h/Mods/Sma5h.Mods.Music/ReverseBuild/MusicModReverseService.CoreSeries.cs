using Microsoft.Extensions.Logging;
using Sma5h.Mods.Music.Helpers;
using Sma5h.Mods.Music.MusicMods.MusicModModels;
using Sma5h.Mods.Music.Models;
using System.IO;
using System.Linq;

namespace Sma5h.Mods.Music.ReverseBuild
{
    public partial class MusicModReverseService
    {
        private void GenerateCoreSeriesOverride(ResourceSnapshot core, ResourceSnapshot output, string overrideOutputPath)
        {
            var newValues = output.SeriesEntries
                .Where(p => IsNewOrChangedConfig<SeriesConfig, SeriesEntry>(core.SeriesEntries, p.Key, p.Value))
                .ToDictionary(p => p.Key, p => _mapper.Map<SeriesConfig>(p.Value));

            var path = Path.Combine(overrideOutputPath, MusicConstants.MusicModFiles.MUSIC_OVERRIDE_CORE_SERIES_JSON_FILE);
            MergeDictionaryFile(path, newValues);

            if (newValues.Count > 0)
                _logger.LogInformation("Reverse MusicMod: wrote {OverridePath}.", path);
        }
    }
}
