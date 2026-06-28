using Newtonsoft.Json;
using Microsoft.Extensions.Logging;
using Sma5h.Mods.Music.Helpers;
using Sma5h.Mods.Music.Models;
using Sma5h.Mods.Music.MusicMods.MusicModModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Sma5h.Mods.Music.ReverseBuild
{
    public partial class MusicModReverseService
    {
        private MusicModConfig GenerateMetadata(
            ResourceSnapshot core,
            ResourceSnapshot output,
            string outputPath,
            string modOutputPath,
            string modName,
            MusicModInformation modInformation)
        {
            var newBgmIds = output.BgmDbRootEntries.Keys.Except(core.BgmDbRootEntries.Keys).OrderBy(p => p).ToList();
            _logger.LogInformation("Reverse MusicMod: found {BgmCount} new BGM entry/entries.", newBgmIds.Count);

            //create new mod from folder name
            var metadata = new MusicModConfig(Guid.NewGuid().ToString())
            {
                Name = !string.IsNullOrWhiteSpace(modInformation?.Name)
                    ? modInformation.Name
                    : string.IsNullOrWhiteSpace(modName) ? Path.GetFileName(Path.TrimEndingDirectorySeparator(modOutputPath)) : modName,
                Author = modInformation?.Author,
                Website = modInformation?.Website,
                Description = modInformation?.Description,
                Series = new List<SeriesConfig>()
            };

            var seriesById = new Dictionary<string, SeriesConfig>();
            var gameById = new Dictionary<string, GameConfig>();

            foreach (var uiBgmId in newBgmIds)
            {
                var dbRoot = output.BgmDbRootEntries[uiBgmId];
                if (!output.GameTitleEntries.TryGetValue(dbRoot.UiGameTitleId, out var gameTitle))
                {
                    _logger.LogWarning("Skipping {UiBgmId}: game title {GameTitleId} was not found.", uiBgmId, dbRoot.UiGameTitleId);
                    continue;
                }

                if (!output.SeriesEntries.TryGetValue(gameTitle.UiSeriesId, out var seriesEntry))
                {
                    _logger.LogWarning("Skipping {UiBgmId}: series {SeriesId} was not found.", uiBgmId, gameTitle.UiSeriesId);
                    continue;
                }

                //get bgm info
                var streamSet = output.StreamSetEntries.GetValueOrDefault(dbRoot.StreamSetId);
                var infoId = GetFirstInfoId(streamSet);
                var assignedInfo = infoId != null ? output.AssignedInfoEntries.GetValueOrDefault(infoId) : null;
                var streamProperty = assignedInfo != null ? output.StreamPropertyEntries.GetValueOrDefault(assignedInfo.StreamId) : null;
                var toneId = GetToneId(streamProperty, dbRoot);
                var bgmProperty = toneId != null ? output.BgmPropertyEntries.GetValueOrDefault(toneId) : null;

                if (streamSet == null || assignedInfo == null || streamProperty == null || bgmProperty == null)
                {
                    _logger.LogWarning("Skipping {UiBgmId}: linked stream/property records are incomplete. StreamSetId={StreamSetId} HasStreamSet={HasStreamSet} InfoId={InfoId} HasAssignedInfo={HasAssignedInfo} StreamId={StreamId} HasStreamProperty={HasStreamProperty} ToneId={ToneId} HasBgmProperty={HasBgmProperty}",
                        uiBgmId,
                        dbRoot.StreamSetId,
                        streamSet != null,
                        infoId,
                        assignedInfo != null,
                        assignedInfo?.StreamId,
                        streamProperty != null,
                        toneId,
                        bgmProperty != null);
                    continue;
                }

                var seriesConfig = GetOrAddSeriesConfig(metadata, seriesById, seriesEntry);
                var gameConfig = GetOrAddGameConfig(gameById, seriesConfig, gameTitle);

                var filename = $"{toneId}.nus3audio";
                CopyNus3Audio(outputPath, toneId, Path.Combine(modOutputPath, filename));

                //add bgm to metadata
                gameConfig.Bgms.Add(new BgmConfig
                {
                    ToneId = toneId,
                    Filename = filename,
                    NUS3BankConfig = new NUS3BankConfig
                    {
                        AudioVolume = ReadNus3BankVolume(outputPath, toneId)
                    },
                    BgmProperties = _mapper.Map<BgmPropertyEntryConfig>(bgmProperty),
                    DbRoot = _mapper.Map<BgmDbRootConfig>(dbRoot),
                    AssignedInfo = _mapper.Map<BgmAssignedInfoConfig>(assignedInfo),
                    StreamSet = _mapper.Map<BgmStreamSetConfig>(streamSet),
                    StreamProperty = _mapper.Map<BgmStreamPropertyConfig>(streamProperty)
                });
            }

            var metadataPath = Path.Combine(modOutputPath, MusicConstants.MusicModFiles.MUSIC_MOD_METADATA_JSON_FILE);
            File.WriteAllText(metadataPath, JsonConvert.SerializeObject(metadata, DefaultFormatting), new UTF8Encoding(false));
            _logger.LogInformation("Reverse MusicMod: wrote {MetadataPath}.", metadataPath);

            return metadata;
        }

        private SeriesConfig GetOrAddSeriesConfig(MusicModConfig metadata, Dictionary<string, SeriesConfig> seriesById, SeriesEntry seriesEntry)
        {
            if (!seriesById.TryGetValue(seriesEntry.UiSeriesId, out var seriesConfig))
            {
                seriesConfig = _mapper.Map<SeriesConfig>(seriesEntry);
                seriesConfig.Games = new List<GameConfig>();
                seriesById.Add(seriesEntry.UiSeriesId, seriesConfig);
                metadata.Series.Add(seriesConfig);
            }

            return seriesConfig;
        }

        private GameConfig GetOrAddGameConfig(Dictionary<string, GameConfig> gameById, SeriesConfig seriesConfig, GameTitleEntry gameTitle)
        {
            if (!gameById.TryGetValue(gameTitle.UiGameTitleId, out var gameConfig))
            {
                gameConfig = _mapper.Map<GameConfig>(gameTitle);
                gameConfig.Bgms = new List<BgmConfig>();
                gameById.Add(gameTitle.UiGameTitleId, gameConfig);
                seriesConfig.Games.Add(gameConfig);
            }

            return gameConfig;
        }

        private static string GetFirstInfoId(BgmStreamSetEntry streamSet)
        {
            if (streamSet == null)
                return null;

            return new[]
            {
                streamSet.Info0, streamSet.Info1, streamSet.Info2, streamSet.Info3,
                streamSet.Info4, streamSet.Info5, streamSet.Info6, streamSet.Info7,
                streamSet.Info8, streamSet.Info9, streamSet.Info10, streamSet.Info11,
                streamSet.Info12, streamSet.Info13, streamSet.Info14, streamSet.Info15
            }.FirstOrDefault(p => !string.IsNullOrEmpty(p));
        }

        private static string GetToneId(BgmStreamPropertyEntry streamProperty, BgmDbRootEntry dbRoot)
        {
            if (!string.IsNullOrEmpty(streamProperty?.DataName0))
                return streamProperty.DataName0;

            var streamId = streamProperty?.StreamId;
            if (!string.IsNullOrEmpty(streamId) && streamId.StartsWith(MusicConstants.InternalIds.STREAM_PREFIX))
                return streamId.Substring(MusicConstants.InternalIds.STREAM_PREFIX.Length);

            if (!string.IsNullOrEmpty(dbRoot?.UiBgmId) && dbRoot.UiBgmId.StartsWith(MusicConstants.InternalIds.UI_BGM_ID_PREFIX))
                return dbRoot.UiBgmId.Substring(MusicConstants.InternalIds.UI_BGM_ID_PREFIX.Length);

            return null;
        }

        private static void CopyNus3Audio(string outputPath, string toneId, string destinationFile)
        {
            var sourceFile = Path.Combine(outputPath, "stream;", "sound", "bgm", string.Format(MusicConstants.GameResources.NUS3AUDIO_FILE, toneId));
            if (!File.Exists(sourceFile))
                return;

            Directory.CreateDirectory(Path.GetDirectoryName(destinationFile));
            File.Copy(sourceFile, destinationFile, true);
        }

        private static float ReadNus3BankVolume(string outputPath, string toneId)
        {
            var bankFile = Path.Combine(outputPath, "stream;", "sound", "bgm", string.Format(MusicConstants.GameResources.NUS3BANK_FILE, toneId));
            if (!File.Exists(bankFile))
                return 2.7f;

            var bytes = File.ReadAllBytes(bankFile);
            var matches = Locate(bytes, new byte[] { 0xE8, 0x22, 0x00, 0x00 }).ToList();
            if (matches.Count != 3 || matches[1] + 8 > bytes.Length)
                return 2.7f;

            return (float)Math.Round(BitConverter.ToSingle(bytes, matches[1] + 4), 2, MidpointRounding.AwayFromZero);
        }

        private static IEnumerable<int> Locate(byte[] haystack, byte[] needle)
        {
            for (var i = 0; i <= haystack.Length - needle.Length; i++)
            {
                var found = true;
                for (var j = 0; j < needle.Length; j++)
                {
                    if (haystack[i + j] != needle[j])
                    {
                        found = false;
                        break;
                    }
                }

                if (found)
                    yield return i;
            }
        }
    }
}
