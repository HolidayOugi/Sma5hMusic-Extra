using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sma5h.Mods.Music.Interfaces;
using Sma5h.Mods.Music.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Sma5h.Mods.Music.CskPackBuild
{
    public partial class CskPackBuildService : ICskPackBuildService
    {
        private const string CskTempFolder = "_csk_temp";
        private const string CloneBgmId = "ui_bgm_a29_ppm_medley";
        private const string CloneSeriesId = "ui_series_mario";
        private const string CloneGameTitleId = "ui_gametitle_paper_mario_series";
        private const string SmashBattlePlaylistId = "bgmsmashbtl";
        private const string SinglePackFolderName = "CSK Music Pack";

        private readonly IOptionsMonitor<CskPackBuildOptions> _config;
        private readonly IMusicModManagerService _musicModManagerService;
        private readonly INus3AudioService _nus3AudioService;
        private readonly IAudioStateService _audioStateService;
        private readonly ILogger _logger;
        private readonly AsyncLocal<string> _currentBuildLocale = new AsyncLocal<string>();
        private readonly AsyncLocal<HashSet<string>> _unavailableBgmNameIds = new AsyncLocal<HashSet<string>>();

        private enum CskPackBuildMode
        {
            Modular,
            MetadataOnly,
            Single
        }

        public CskPackBuildService(
            IOptionsMonitor<CskPackBuildOptions> config,
            IMusicModManagerService musicModManagerService,
            INus3AudioService nus3AudioService,
            IAudioStateService audioStateService,
            ILogger<CskPackBuildService> logger)
        {
            _config = config;
            _musicModManagerService = musicModManagerService;
            _nus3AudioService = nus3AudioService;
            _audioStateService = audioStateService;
            _logger = logger;
        }

        #region Public

        public Task Build(string locale = null)
        {
            return Task.Run(() => BuildInternal(null, CskPackBuildMode.Modular, locale));
        }

        public Task Build(IEnumerable<string> selectedSeriesKeys, string locale = null)
        {
            var selected = new HashSet<string>(selectedSeriesKeys ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            return Task.Run(() => BuildInternal(selected, CskPackBuildMode.Modular, locale));
        }

        public Task BuildMetadataOnly(string locale = null)
        {
            return Task.Run(() => BuildInternal(null, CskPackBuildMode.MetadataOnly, locale));
        }

        public Task BuildSingle(IEnumerable<string> selectedSeriesKeys, string locale = null)
        {
            var selected = new HashSet<string>(selectedSeriesKeys ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            return Task.Run(() => BuildInternal(selected, CskPackBuildMode.Single, locale));
        }

        //get all series from all mods
        public Task<IReadOnlyList<CskPackSeriesOption>> GetAvailableSeries(string locale = null)
        {
            return Task.Run<IReadOnlyList<CskPackSeriesOption>>(() =>
            {
                //get the build locale from GUI
                _currentBuildLocale.Value = locale;
                try
                {
                    var mods = GetMusicMods();
                    var contexts = LoadModContexts(mods);

                    //returns all series from all mods
                    return contexts
                        .SelectMany(context => context.SeriesList.Select(series => CreateSeriesOption(context, series)))
                        .OrderBy(p => p.DisplayName, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(p => p.ModName, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                }
                finally
                {
                    _currentBuildLocale.Value = null;
                }
            });
        }

        #endregion

        #region Build

        private void BuildInternal(HashSet<string> selectedSeriesKeys, CskPackBuildMode buildMode, string locale)
        {
            _currentBuildLocale.Value = locale;

            try
            {
                var mods = GetMusicMods();
                var buildResources = LoadBuildResources();

                var contexts = LoadModContexts(mods);
                var hasCoreBgmOverride = HasJsonValues(buildResources.RawCoreBgmOverride);
                if (contexts.Count == 0 && !hasCoreBgmOverride)
                {
                    if (mods.Count == 0)
                        throw new InvalidOperationException("No music mods were found.");

                    throw new InvalidOperationException("No metadata_mod.json files were found in the currently loaded music mods.");
                }

                if (selectedSeriesKeys == null)
                {
                    selectedSeriesKeys = contexts
                        .SelectMany(context => context.SeriesList.Select(series => CreateSeriesKey(context.Mod, series)))
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);
                }

                if (contexts.Count > 0 && selectedSeriesKeys.Count == 0)
                    throw new InvalidOperationException("No CSK pack series were selected.");

                var outputRoot = PrepareOutputRoot();
                var tempRoot = Path.Combine(outputRoot, CskTempFolder);

                try
                {
                    var contextList = contexts.ToList();
                    //for metadata only builds
                    var includeAudio = buildMode != CskPackBuildMode.MetadataOnly;
                    _unavailableBgmNameIds.Value = includeAudio
                        ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                        : null;
                    var generatedBgmFolder = includeAudio
                        ? GenerateBgmFiles(contextList, tempRoot, selectedSeriesKeys, buildResources)
                        : null;

                    if (contextList.Count == 0)
                        GenerateVanillaSongsChangesPack(contextList, outputRoot, selectedSeriesKeys, generatedBgmFolder, buildResources, includeAudio);
                    else if (buildMode == CskPackBuildMode.Single)
                        GenerateSingleCskPack(contextList, generatedBgmFolder, outputRoot, selectedSeriesKeys, buildResources, includeAudio);
                    else
                        GenerateCskPacks(contextList, generatedBgmFolder, outputRoot, selectedSeriesKeys, buildResources, includeAudio);
                }
                finally
                {
                    if (Directory.Exists(tempRoot))
                        Directory.Delete(tempRoot, true);
                }
            }
            finally
            {
                _currentBuildLocale.Value = null;
                _unavailableBgmNameIds.Value = null;
            }
        }

        #endregion

    }
}
