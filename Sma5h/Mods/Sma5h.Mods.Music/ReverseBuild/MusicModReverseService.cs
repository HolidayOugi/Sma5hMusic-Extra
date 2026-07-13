using AutoMapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sma5h;
using Sma5h.Interfaces;
using Sma5h.Mods.Music.Interfaces;
using Sma5h.Mods.Music.Models;
using Sma5h.Mods.Music.MusicMods;
using Sma5h.Mods.Music.MusicMods.MusicModModels;
using Sma5h.ResourceProviders;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Sma5h.Mods.Music.ReverseBuild
{
    public partial class MusicModReverseService : IMusicModReverseService
    {
        private const Newtonsoft.Json.Formatting DefaultFormatting = Newtonsoft.Json.Formatting.Indented;

        private readonly ILogger _logger;
        private readonly IMapper _mapper;
        private readonly IServiceProvider _serviceProvider;
        private readonly IOptionsMonitor<Sma5hOptions> _config;
        private readonly IAudioMetadataService _audioMetadataService;
        private PrcResourceProvider _prcProvider;
        private MsbtResourceProvider _msbtProvider;
        private BgmPropertyProvider _bgmPropertyProvider;

        public MusicModReverseService(IServiceProvider serviceProvider, IMapper mapper, IOptionsMonitor<Sma5hOptions> config, IAudioMetadataService audioMetadataService, ILogger<MusicModReverseService> logger)
        {
            _logger = logger;
            _mapper = mapper;
            _config = config;
            _audioMetadataService = audioMetadataService;
            _serviceProvider = serviceProvider;
        }

        public MusicModConfig Reverse(string coreResourcesPath, string outputPath, string modOutputPath, string modName = null, MusicModInformation modInformation = null)
        {
            var overrideOutputPath = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(modOutputPath)) ?? string.Empty, "MusicOverride");
            return Reverse(coreResourcesPath, outputPath, modOutputPath, overrideOutputPath, modName, modInformation);
        }

        public MusicModConfig Reverse(string coreResourcesPath, string outputPath, string modOutputPath, string overrideOutputPath, string modName = null, MusicModInformation modInformation = null)
        {
            if (string.IsNullOrWhiteSpace(coreResourcesPath))
                throw new ArgumentException("Core resources path is required.", nameof(coreResourcesPath));
            if (string.IsNullOrWhiteSpace(outputPath))
                throw new ArgumentException("Output path is required.", nameof(outputPath));
            if (string.IsNullOrWhiteSpace(modOutputPath))
                throw new ArgumentException("Mod output path is required.", nameof(modOutputPath));
            if (string.IsNullOrWhiteSpace(overrideOutputPath))
                throw new ArgumentException("Override output path is required.", nameof(overrideOutputPath));

            EnsureResourceProviders();
            //read vanilla files
            var core = LoadSnapshot(coreResourcesPath);
            //read the build output files to find the diff
            var output = LoadSnapshot(outputPath, coreResourcesPath);

            Directory.CreateDirectory(modOutputPath);
            Directory.CreateDirectory(overrideOutputPath);

            var metadata = GenerateMetadata(core, output, outputPath, modOutputPath, modName, modInformation);
            GenerateCoreBgmOverride(core, output, outputPath, overrideOutputPath);
            GenerateCoreGameOverride(core, output, overrideOutputPath);
            GenerateCoreSeriesOverride(core, output, overrideOutputPath);
            GenerateOrderOverride(core, output, overrideOutputPath);
            GeneratePlaylistOverride(core, output, overrideOutputPath);
            GenerateStageOverride(core, output, overrideOutputPath);

            return metadata;
        }

        private void EnsureResourceProviders()
        {
            if (_prcProvider != null && _msbtProvider != null && _bgmPropertyProvider != null)
                return;

            var resourceProviders = _serviceProvider.GetServices<IResourceProvider>();
            _prcProvider = resourceProviders.OfType<PrcResourceProvider>().First();
            _msbtProvider = resourceProviders.OfType<MsbtResourceProvider>().First();
            _bgmPropertyProvider = resourceProviders.OfType<BgmPropertyProvider>().First();
        }

    }
}
