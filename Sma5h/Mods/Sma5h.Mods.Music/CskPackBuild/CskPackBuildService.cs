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
        private const string SinglePackFolderName = "CSK Music Pack";

        private readonly IOptionsMonitor<CskPackBuildOptions> _config;
        private readonly IMusicModManagerService _musicModManagerService;
        private readonly INus3AudioService _nus3AudioService;
        private readonly IAudioStateService _audioStateService;
        private readonly ILogger _logger;
        private readonly AsyncLocal<string> _currentBuildLocale = new AsyncLocal<string>();
        private readonly AsyncLocal<HashSet<string>> _unavailableBgmNameIds = new AsyncLocal<HashSet<string>>();
        private readonly AsyncLocal<Dictionary<string, HashSet<int>>> _playlistSettingIndices = new AsyncLocal<Dictionary<string, HashSet<int>>>();

        private enum CskPackBuildMode
        {
            Modular,
            ModularByMod,
            MetadataOnly,
            Single
        }

        #region Public

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

        public Task Build(string locale = null)
        {
            return Task.Run(() => BuildInternal(null, CskPackBuildMode.Modular, locale));
        }

        public Task Build(IEnumerable<string> selectedSeriesKeys, string locale = null)
        {
            var selected = new HashSet<string>(selectedSeriesKeys ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            return Task.Run(() => BuildInternal(selected, CskPackBuildMode.Modular, locale));
        }

        public Task BuildByMod(IEnumerable<string> selectedModKeys, string locale = null)
        {
            var selected = new HashSet<string>(selectedModKeys ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            return Task.Run(() => BuildInternal(null, CskPackBuildMode.ModularByMod, locale, selected));
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

        public Task<IReadOnlyList<CskPackModOption>> GetAvailableMods(string locale = null)
        {
            return Task.Run<IReadOnlyList<CskPackModOption>>(() => WithLocale(locale, () =>
                LoadModContexts(GetMusicMods())
                    .Where(context => context.SeriesList.Count > 0)
                    .Select(context => new CskPackModOption
                    {
                        Key = CreateModKey(context.Mod),
                        DisplayName = context.Mod.Name
                    })
                    .OrderBy(option => option.DisplayName, StringComparer.OrdinalIgnoreCase)
                    .ToList()));
        }

        //get all series from all mods
        public Task<IReadOnlyList<CskPackSeriesOption>> GetAvailableSeries(string locale = null)
        {
            return Task.Run<IReadOnlyList<CskPackSeriesOption>>(() => WithLocale(locale, () =>
                LoadModContexts(GetMusicMods())
                    .SelectMany(context => context.SeriesList.Select(series => CreateSeriesOption(context, series)))
                    .OrderBy(option => option.DisplayName, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(option => option.ModName, StringComparer.OrdinalIgnoreCase)
                    .ToList()));
        }

        #endregion

        #region Build

        private T WithLocale<T>(string locale, Func<T> action)
        {
            _currentBuildLocale.Value = locale;
            try
            {
                return action();
            }
            finally
            {
                _currentBuildLocale.Value = null;
            }
        }

        private void BuildInternal(HashSet<string> selectedSeriesKeys, CskPackBuildMode mode, string locale, HashSet<string> selectedModKeys = null)
        {
            _currentBuildLocale.Value = locale;
            try
            {
                var mods = GetMusicMods();
                var contexts = LoadModContexts(mods);
                var state = CaptureBuildState();
                _playlistSettingIndices.Value = BuildPlaylistSettingIndexMap();

                if (contexts.Count == 0 && !state.HasCoreChanges)
                {
                    if (mods.Count == 0)
                        throw new InvalidOperationException("No music mods were found.");
                    throw new InvalidOperationException("No music entries were found in the currently loaded music mods.");
                }

                if (mode == CskPackBuildMode.ModularByMod)
                {
                    selectedSeriesKeys = contexts
                        .Where(context => selectedModKeys != null && selectedModKeys.Contains(CreateModKey(context.Mod)))
                        .SelectMany(context => context.SeriesList.Select(series => CreateSeriesKey(context.Mod, series)))
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);
                }
                else if (selectedSeriesKeys == null)
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
                    //for metadata only builds
                    var includeAudio = mode != CskPackBuildMode.MetadataOnly;
                    _unavailableBgmNameIds.Value = includeAudio ? new HashSet<string>(StringComparer.OrdinalIgnoreCase) : null;
                    var generatedBgmFolder = includeAudio
                        ? GenerateBgmFiles(contexts, tempRoot, selectedSeriesKeys, state)
                        : null;

                    if (contexts.Count == 0)
                        GenerateVanillaSongsChangesPack(contexts, outputRoot, selectedSeriesKeys, generatedBgmFolder, state, includeAudio);
                    else if (mode == CskPackBuildMode.Single)
                        GenerateSingleCskPack(contexts, generatedBgmFolder, outputRoot, selectedSeriesKeys, state, includeAudio);
                    else if (mode == CskPackBuildMode.ModularByMod)
                        GenerateCskPacksByMod(contexts, generatedBgmFolder, outputRoot, selectedSeriesKeys, state, includeAudio);
                    else
                        GenerateCskPacks(contexts, generatedBgmFolder, outputRoot, selectedSeriesKeys, state, includeAudio);
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
                _playlistSettingIndices.Value = null;
            }
        }

        #endregion
    }
}
