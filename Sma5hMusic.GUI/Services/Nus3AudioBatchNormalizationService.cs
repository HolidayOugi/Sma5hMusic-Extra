using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Sma5h.Mods.Music;
using Sma5h.Mods.Music.Helpers;
using Sma5h.Mods.Music.Interfaces;
using Sma5h.Mods.Music.Models;
using Sma5hMusic.GUI.Helpers;
using Sma5hMusic.GUI.Interfaces;
using Sma5hMusic.GUI.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Sma5hMusic.GUI.Services
{
    public class Nus3AudioBatchNormalizationService : INus3AudioBatchNormalizationService
    {
        private const string REGEX_REPLACE = @"[^a-zA-Z0-9_]";

        private readonly IAudioImportService _audioImportService;
        private readonly IAudioMetadataService _audioMetadataService;
        private readonly ILogger _logger;
        private readonly IOptionsMonitor<ApplicationSettings> _config;

        public Nus3AudioBatchNormalizationService(
            IAudioImportService audioImportService,
            IAudioMetadataService audioMetadataService,
            IOptionsMonitor<ApplicationSettings> config,
            ILogger<Nus3AudioBatchNormalizationService> logger)
        {
            _audioImportService = audioImportService;
            _audioMetadataService = audioMetadataService;
            _config = config;
            _logger = logger;
        }

        public IReadOnlyList<string> GetNormalizableAudioFiles(string musicModsPath)
        {
            if (string.IsNullOrWhiteSpace(musicModsPath) || !Directory.Exists(musicModsPath))
                return new List<string>();

            return Directory
                .EnumerateFiles(musicModsPath, "*.*", SearchOption.AllDirectories)
                .Where(p => _audioImportService.IsNus3Audio(p) ||
                    MusicConstants.VALID_MUSIC_EXTENSIONS.Any(extension => string.Equals(Path.GetExtension(p), extension, StringComparison.OrdinalIgnoreCase)))
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public Task<Nus3AudioBatchNormalizationResult> NormalizeFiles(
            IReadOnlyList<string> files,
            string musicModsPath,
            Action<int, int, string> onProgress = null,
            CancellationToken cancellationToken = default)
        {
            return Task.Run(async () =>
            {
                var result = new Nus3AudioBatchNormalizationResult
                {
                    TotalFiles = files?.Count ?? 0
                };

                if (files == null || files.Count == 0)
                    return result;

                for (var i = 0; i < files.Count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var file = files[i];
                    var relativeFile = GetRelativePath(musicModsPath, file);

                    onProgress?.Invoke(i, files.Count, relativeFile);

                    try
                    {
                        await NormalizeFile(file, cancellationToken);
                        result.NormalizedFiles++;
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception e)
                    {
                        result.FailedFiles.Add(relativeFile);
                        _logger.LogError(e, "Could not normalize NUS3AUDIO file {Filename}", file);
                    }

                    onProgress?.Invoke(i + 1, files.Count, relativeFile);
                }

                return result;
            }, cancellationToken);
        }

        private async Task NormalizeFile(string filename, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var toneId = LoadToneId(Path.GetFileNameWithoutExtension(filename));
            //create temp directory
            var tempDirectory = Path.Combine(
                _config.CurrentValue.TempPath,
                "Nus3AudioBatchNormalization",
                Guid.NewGuid().ToString("N")
            );

            Directory.CreateDirectory(tempDirectory);

            try
            {
                //call normalize from audioImportService
                var normalizedFile = await _audioImportService.NormalizeNus3Audio(
                    toneId,
                    filename,
                    tempDirectory,
                    cancellationToken
                );

                cancellationToken.ThrowIfCancellationRequested();

                if (string.IsNullOrWhiteSpace(normalizedFile) || !File.Exists(normalizedFile))
                    throw new InvalidOperationException($"The normalized file for '{Path.GetFileName(filename)}' was not created.");

                var outputFile = _audioImportService.IsNus3Audio(filename)
                    ? filename
                    : Path.Combine(Path.GetDirectoryName(filename) ?? string.Empty, $"{toneId}.nus3audio");

                File.Copy(normalizedFile, outputFile, true);

                var metadataUpdated = false;
                try
                {
                    //get new cue points after normalization
                    var cuePoints = await _audioMetadataService.GetCuePoints(outputFile);

                    var metadataFilename = _audioImportService.IsNus3Audio(filename)
                        ? filename
                        : Path.Combine(
                            Path.GetDirectoryName(filename) ?? string.Empty,
                            $"{toneId}{Path.GetExtension(filename).ToLowerInvariant()}"
                        );
                    //update metadata_mod if needed
                    metadataUpdated = UpdateMetadata(metadataFilename, outputFile, cuePoints);
                }
                catch (Exception e)
                {
                    _logger.LogWarning(e, "Could not update metadata for normalized file {Filename}", outputFile);
                }

                if (!string.Equals(outputFile, filename, StringComparison.OrdinalIgnoreCase))
                {
                    if (metadataUpdated)
                        DeleteOriginalFile(filename);
                }
            }
            finally
            {
                DeleteTempDirectory(tempDirectory);
                TempDirectoryHelper.DeleteIfEmpty(
                    Path.Combine(_config.CurrentValue.TempPath, "Nus3AudioBatchNormalization")
                );
            }
        }

        //TODO: can this be done better?
        private bool UpdateMetadata(string oldFilename, string newFilename, AudioCuePoints cuePoints)
        {
            var modPath = Path.GetDirectoryName(Path.GetFullPath(oldFilename));
            var musicModsPath = Path.GetFullPath(_config.CurrentValue.Sma5hMusic.ModPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string metadataFile = null;

            while (!string.IsNullOrWhiteSpace(modPath))
            {
                metadataFile = Path.Combine(modPath, MusicConstants.MusicModFiles.MUSIC_MOD_METADATA_JSON_FILE);
                if (File.Exists(metadataFile))
                    break;

                var current = modPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (string.Equals(current, musicModsPath, StringComparison.OrdinalIgnoreCase))
                {
                    metadataFile = null;
                    break;
                }

                modPath = Path.GetDirectoryName(current);
            }

            if (string.IsNullOrWhiteSpace(metadataFile))
            {
                _logger.LogWarning("Could not find metadata_mod.json for normalized file {Filename}", oldFilename);
                return false;
            }

            var oldRelative = GetRelativePath(modPath, oldFilename);
            var newRelative = GetRelativePath(modPath, newFilename);
            var file = File.ReadAllText(metadataFile);
            var json = JObject.Parse(file);
            var bgm = json["series"]?
                .SelectMany(series => series["games"]?.Children() ?? Enumerable.Empty<JToken>())
                .SelectMany(game => game["bgms"]?.Children() ?? Enumerable.Empty<JToken>())
                .OfType<JObject>()
                .FirstOrDefault(p => FilenameEquals(GetString(p, "filename"), oldRelative));

            if (bgm == null)
            {
                _logger.LogWarning("Could not update metadata filename from {OldFilename} to {NewFilename}", oldRelative, newRelative);
                return false;
            }

            bgm["filename"] = newRelative; //replace filename if it changed
            UpdateBgmProperties(bgm["bgm_properties"] as JObject, cuePoints); //update cue points

            File.WriteAllText(metadataFile, JsonConvert.SerializeObject(json, Formatting.Indented));
            return true;
        }

        private void DeleteOriginalFile(string filename)
        {
            try
            {
                File.Delete(filename);

                if (File.Exists(filename))
                    _logger.LogWarning("Could not delete normalized file {Filename}", filename);
            }
            catch (Exception e)
            {
                _logger.LogWarning(e, "Could not delete normalized file {Filename}", filename);
            }
        }

        private static void UpdateBgmProperties(JObject bgmProperties, AudioCuePoints cuePoints)
        {
            if (bgmProperties == null || cuePoints == null)
                return;

            bgmProperties["loop_start_ms"] = cuePoints.LoopStartMs;
            bgmProperties["loop_start_sample"] = cuePoints.LoopStartSample;
            bgmProperties["loop_end_ms"] = cuePoints.LoopEndMs;
            bgmProperties["loop_end_sample"] = cuePoints.LoopEndSample;
            bgmProperties["total_time_ms"] = cuePoints.TotalTimeMs;
            bgmProperties["total_samples"] = cuePoints.TotalSamples;
        }

        private static bool FilenameEquals(string left, string right)
        {
            return string.Equals(NormalizeRelativePath(left), NormalizeRelativePath(right), StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeRelativePath(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/');
        }

        private static string GetString(JToken token, string propertyName)
        {
            return token?[propertyName]?.Value<string>() ?? string.Empty;
        }

        private static string LoadToneId(string toneId)
        {
            var sanitizedToneId = Regex.Replace(toneId.Replace(" ", "_"), REGEX_REPLACE, string.Empty).ToLower();
            return string.IsNullOrEmpty(sanitizedToneId) ? Guid.NewGuid().ToString("N") : sanitizedToneId;
        }

        private static string GetRelativePath(string rootPath, string filename)
        {
            var fullRootPath = Path.GetFullPath(rootPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            var fullFilename = Path.GetFullPath(filename);

            return fullFilename.StartsWith(fullRootPath, StringComparison.OrdinalIgnoreCase)
                ? fullFilename.Substring(fullRootPath.Length)
                : filename;
        }

        private static void DeleteTempDirectory(string directory)
        {
            try
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, true);
            }
            catch
            {
                // Best effort cleanup.
            }
        }
    }
}
