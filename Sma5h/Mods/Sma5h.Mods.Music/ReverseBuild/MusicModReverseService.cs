using AutoMapper;
using Microsoft.Extensions.Logging;
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
        private readonly PrcResourceProvider _prcProvider;
        private readonly MsbtResourceProvider _msbtProvider;
        private readonly BgmPropertyProvider _bgmPropertyProvider;

        public MusicModReverseService(IEnumerable<IResourceProvider> resourceProviders, IMapper mapper, ILogger<MusicModReverseService> logger)
        {
            _logger = logger;
            _mapper = mapper;
            _prcProvider = resourceProviders.OfType<PrcResourceProvider>().First();
            _msbtProvider = resourceProviders.OfType<MsbtResourceProvider>().First();
            _bgmPropertyProvider = resourceProviders.OfType<BgmPropertyProvider>().First();
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

            var core = LoadSnapshot(coreResourcesPath);
            var output = LoadSnapshot(outputPath, core);

            Directory.CreateDirectory(modOutputPath);
            Directory.CreateDirectory(overrideOutputPath);

            var metadata = GenerateMetadata(core, output, outputPath, modOutputPath, modName, modInformation);
            GenerateCoreBgmOverride(core, output, overrideOutputPath);
            GenerateCoreGameOverride(core, output, overrideOutputPath);
            GenerateCoreSeriesOverride(core, output, overrideOutputPath);
            GenerateOrderOverride(core, output, overrideOutputPath);
            GeneratePlaylistOverride(core, output, overrideOutputPath);
            GenerateStageOverride(core, output, overrideOutputPath);

            return metadata;
        }

    }
}
