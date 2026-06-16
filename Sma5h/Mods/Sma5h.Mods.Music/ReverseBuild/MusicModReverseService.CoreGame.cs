using Microsoft.Extensions.Logging;
using Sma5h.Mods.Music.Helpers;
using Sma5h.Mods.Music.MusicMods.MusicModModels;
using Sma5h.Mods.Music.Models;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Sma5h.Mods.Music.ReverseBuild
{
    public partial class MusicModReverseService
    {
        private void GenerateCoreGameOverride(ResourceSnapshot core, ResourceSnapshot output, string overrideOutputPath)
        {
            var newValues = output.GameTitleEntries
                .Where(p => IsNewOrChangedConfig<GameConfig, GameTitleEntry>(core.GameTitleEntries, p.Key, p.Value))
                .ToDictionary(p => p.Key, p => _mapper.Map<GameConfig>(p.Value));

            var path = Path.Combine(overrideOutputPath, MusicConstants.MusicModFiles.MUSIC_OVERRIDE_CORE_GAME_JSON_FILE);
            MergeDictionaryFile(path, newValues);

            if (newValues.Count > 0)
                _logger.LogInformation("Reverse MusicMod: wrote {OverridePath}.", path);
        }

        private bool IsNewOrChangedConfig<TConfig, TEntry>(Dictionary<string, TEntry> coreEntries, string id, TEntry outputEntry)
        {
            if (!coreEntries.TryGetValue(id, out var coreEntry))
                return true;

            return !JsonEquals(_mapper.Map<TConfig>(coreEntry), _mapper.Map<TConfig>(outputEntry));
        }
    }
}
