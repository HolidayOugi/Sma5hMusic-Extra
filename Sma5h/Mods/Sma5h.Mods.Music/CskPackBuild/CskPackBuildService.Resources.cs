using Newtonsoft.Json.Linq;
using Sma5h.Mods.Music;
using Sma5h.Mods.Music.Helpers;
using System.IO;

namespace Sma5h.Mods.Music.CskPackBuild
{
    public partial class CskPackBuildService
    {
        #region Loading

        private CskBuildResources LoadBuildResources()
        {
            var overridePath = _config.CurrentValue.Sma5hMusicOverride.ModPath;
            var orderOverride = LoadJsonObject(Path.Combine(overridePath, MusicConstants.MusicModFiles.MUSIC_OVERRIDE_ORDER_JSON_FILE));
            var playlistOverride = LoadJsonObject(Path.Combine(overridePath, MusicConstants.MusicModFiles.MUSIC_OVERRIDE_PLAYLIST_JSON_FILE));
            var coreBgmOverride = LoadJsonObject(Path.Combine(overridePath, MusicConstants.MusicModFiles.MUSIC_OVERRIDE_CORE_BGM_JSON_FILE));
            var coreGameOverride = LoadJsonObject(Path.Combine(overridePath, MusicConstants.MusicModFiles.MUSIC_OVERRIDE_CORE_GAME_JSON_FILE));
            var coreSeriesOverride = LoadJsonObject(Path.Combine(overridePath, MusicConstants.MusicModFiles.MUSIC_OVERRIDE_CORE_SERIES_JSON_FILE));
            var stageOverride = LoadJsonObject(Path.Combine(overridePath, MusicConstants.MusicModFiles.MUSIC_OVERRIDE_STAGE_JSON_FILE));
            var effectiveCoreGameOverride = BuildEffectiveCoreGameData(coreGameOverride);

            return new CskBuildResources
            {
                CoreGameSeriesById = BuildCoreGameSeriesById(effectiveCoreGameOverride),
                CoreBgmIds = BuildCoreBgmIds(),
                HasCoreOverrides = HasJsonValues(coreBgmOverride) || HasJsonValues(coreGameOverride) || HasJsonValues(coreSeriesOverride),
                OrderOverride = BuildEffectiveOrderData(orderOverride),
                PlaylistData = BuildEffectivePlaylistData(playlistOverride),
                RawCoreBgmOverride = coreBgmOverride,
                RawCoreGameOverride = coreGameOverride,
                RawCoreSeriesOverride = coreSeriesOverride,
                CoreBgmOverride = BuildEffectiveCoreBgmOverrideData(coreBgmOverride),
                CoreGameOverride = effectiveCoreGameOverride,
                CoreSeriesOverride = BuildEffectiveCoreSeriesData(coreSeriesOverride),
                StageOverride = BuildEffectiveStageData(stageOverride)
            };
        }

        private static bool HasJsonValues(JObject value)
        {
            return value != null && value.HasValues;
        }

        #endregion

    }
}
