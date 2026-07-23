using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Sma5h.Mods.Music;
using Sma5h.Mods.Music.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Sma5h.Mods.Music.CskPackBuild
{
    public partial class CskPackBuildService
    {
        #region Output

        private string PrepareOutputRoot()
        {
            var configuredOutputPath = _config.CurrentValue.OutputPath;
            if (string.IsNullOrWhiteSpace(configuredOutputPath))
                throw new InvalidOperationException("Output path is not configured.");

            var outputRoot = Path.GetFullPath(configuredOutputPath);
            if (string.Equals(outputRoot, Path.GetPathRoot(outputRoot), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Refusing to clear the drive root: {outputRoot}");

            _logger.LogInformation("[CSK] Clearing output folder {OutputPath}", outputRoot);
            ClearDirectory(outputRoot);
            return outputRoot;
        }

        private static void ClearDirectory(string path)
        {
            Directory.CreateDirectory(path);

            foreach (var file in Directory.GetFiles(path))
                File.Delete(file);

            foreach (var directory in Directory.GetDirectories(path))
                Directory.Delete(directory, true);
        }

        #endregion

        #region Audio Files

        private void CopyBgmFiles(JObject bgm, string seriesFolderName, string outputRoot, string generatedBgmFolder)
        {
            var filename = GetString(bgm, "filename");
            var filenameNoExt = Path.GetFileNameWithoutExtension(filename);
            var nus3AudioSrc = Path.Combine(generatedBgmFolder, $"bgm_{filenameNoExt}.nus3audio");
            var nus3BankSrc = Path.Combine(generatedBgmFolder, $"bgm_{filenameNoExt}.nus3bank");
            var destFolder = Path.Combine(outputRoot, seriesFolderName, "stream;", "sound", "bgm");
            Directory.CreateDirectory(destFolder);

            MoveIfExists(nus3AudioSrc, Path.Combine(destFolder, Path.GetFileName(nus3AudioSrc)));
            MoveIfExists(nus3BankSrc, Path.Combine(destFolder, Path.GetFileName(nus3BankSrc)));
        }

        private void CopyGeneratedBgmFiles(string generatedBgmFolder, string destinationFolder)
        {
            if (string.IsNullOrEmpty(generatedBgmFolder) || !Directory.Exists(generatedBgmFolder))
                return;

            Directory.CreateDirectory(destinationFolder);
            foreach (var file in Directory.GetFiles(generatedBgmFolder, "bgm_*.nus3*", SearchOption.TopDirectoryOnly))
                CopyIfExists(file, Path.Combine(destinationFolder, Path.GetFileName(file)));
        }

        private void CopyCoreVolumeOverrideBankFiles(
            string seriesName,
            string seriesFolderName,
            string outputRoot,
            string generatedBgmFolder,
            CskBuildResources buildResources)
        {
            if (string.IsNullOrEmpty(generatedBgmFolder) || !Directory.Exists(generatedBgmFolder))
                return;

            var entries = GetCoreVolumeOverrideEntries(buildResources)
                .Where(p => string.Equals(p.SeriesName, seriesName, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (entries.Count == 0)
                return;

            var destFolder = Path.Combine(outputRoot, seriesFolderName, "stream;", "sound", "bgm");
            Directory.CreateDirectory(destFolder);
            foreach (var entry in entries)
            {
                var source = Path.Combine(generatedBgmFolder, string.Format(MusicConstants.GameResources.NUS3BANK_FILE, entry.NameId));
                //check nus3audio exists before copying (ie replacement song)
                var audioSource = Path.Combine(generatedBgmFolder, string.Format(MusicConstants.GameResources.NUS3AUDIO_FILE, entry.NameId));
                if (!File.Exists(source) || File.Exists(audioSource))
                    continue;

                CopyIfExists(source, Path.Combine(destFolder, Path.GetFileName(source)));
            }
        }

        #endregion

        #region Series Icons

        private bool CopySeriesIcon(JObject series, string packRoot)
        {
            var iconFile = GetSeriesIconPath(series);
            if (string.IsNullOrEmpty(iconFile))
                return false;

            var destinationFolder = Path.Combine(packRoot, "ui", "replace", "series", "series_0");
            Directory.CreateDirectory(destinationFolder);

            var destination = Path.Combine(destinationFolder, Path.GetFileName(iconFile));
            File.Copy(iconFile, destination, true);
            _logger.LogInformation("[CSK] Copied series icon {IconFile} to {Destination}", iconFile, destination);
            return true;
        }

        private string GetSeriesIconPath(JObject series)
        {
            var iconFolder = GetMusicIconsFolder();
            if (!Directory.Exists(iconFolder))
                return null;

            foreach (var fileName in GetSeriesIconFileNames(series))
            {
                var path = Path.Combine(iconFolder, fileName);
                if (File.Exists(path))
                    return path;
            }

            return null;
        }

        private IEnumerable<string> GetSeriesIconFileNames(JObject series)
        {
            foreach (var value in new[] { GetString(series, "name_id"), GetString(series, "ui_series_id") })
            {
                var sanitized = GetSeriesIconNamePart(value);
                if (!string.IsNullOrEmpty(sanitized))
                    yield return $"series_0_{sanitized}.bntx";
            }
        }

        private string GetMusicIconsFolder()
        {
            var modPath = _config.CurrentValue.Sma5hMusic.ModPath;
            var fullModPath = Path.GetFullPath(modPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            var modsFolder = Path.GetDirectoryName(fullModPath);
            if (string.IsNullOrEmpty(modsFolder))
                modsFolder = Path.GetFullPath("Mods");

            return Path.Combine(modsFolder, "MusicIcons");
        }

        private static string GetSeriesIconNamePart(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            var seriesName = value.StartsWith(MusicConstants.InternalIds.SERIES_ID_PREFIX, StringComparison.OrdinalIgnoreCase)
                ? value.Substring(MusicConstants.InternalIds.SERIES_ID_PREFIX.Length)
                : value;

            return Regex.Replace(seriesName, @"[^a-zA-Z0-9_]", string.Empty).ToLowerInvariant();
        }

        #endregion

        #region Utils

        private void MoveIfExists(string source, string destination)
        {
            if (File.Exists(source))
            {
                if (File.Exists(destination))
                    File.Delete(destination);

                File.Move(source, destination);
            }
            else
            {
                _logger.LogWarning("[CSK] File missing: {Source}", source);
            }
        }

        private void CopyIfExists(string source, string destination)
        {
            if (File.Exists(source))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                File.Copy(source, destination, true);
            }
            else
            {
                _logger.LogWarning("[CSK] File missing: {Source}", source);
            }
        }

        #endregion

    }
}
