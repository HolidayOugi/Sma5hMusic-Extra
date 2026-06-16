using Microsoft.Extensions.Logging;
using Sma5h.Mods.Music.Helpers;
using Sma5h.Mods.Music.MusicOverride.MusicOverrideConfigModels;
using Sma5h.Mods.Music.Models;
using System.IO;
using System.Linq;

namespace Sma5h.Mods.Music.ReverseBuild
{
    public partial class MusicModReverseService
    {
        private void GenerateStageOverride(ResourceSnapshot core, ResourceSnapshot output, string overrideOutputPath)
        {
            var newValues = output.StageEntries
                .Where(p => IsNewOrChangedConfig<StageConfig, StageEntry>(core.StageEntries, p.Key, p.Value))
                .ToDictionary(p => p.Key, p => _mapper.Map<StageConfig>(p.Value));

            var path = Path.Combine(overrideOutputPath, MusicConstants.MusicModFiles.MUSIC_OVERRIDE_STAGE_JSON_FILE);
            MergeDictionaryFile(path, newValues);

            if (newValues.Count > 0)
                _logger.LogInformation("Reverse MusicMod: wrote {OverridePath}.", path);
        }
    }
}
