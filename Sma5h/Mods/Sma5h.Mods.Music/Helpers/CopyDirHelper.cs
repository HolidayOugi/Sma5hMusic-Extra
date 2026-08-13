using System.IO;

using System;

namespace Sma5h.Mods.Music.Helpers
{
    public static class CopyDirHelper
    {
        public static void Copy(string sourceDirectory, string targetDirectory, string searchPattern = "*")
        {
            DirectoryInfo diSource = new DirectoryInfo(sourceDirectory);
            DirectoryInfo diTarget = new DirectoryInfo(targetDirectory);

            var sourcePath = Path.TrimEndingDirectorySeparator(diSource.FullName);
            var targetPath = Path.TrimEndingDirectorySeparator(diTarget.FullName);
            var comparison = OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;

            if (string.Equals(sourcePath, targetPath, comparison) ||
                targetPath.StartsWith(sourcePath + Path.DirectorySeparatorChar, comparison))
            {
                throw new IOException($"The target directory '{diTarget.FullName}' must be outside the source directory '{diSource.FullName}'.");
            }

            CopyAll(diSource, diTarget, searchPattern);
        }

        private static void CopyAll(DirectoryInfo source, DirectoryInfo target, string searchPattern = "*")
        {
            Directory.CreateDirectory(target.FullName);

            foreach (FileInfo fi in source.GetFiles(searchPattern, SearchOption.TopDirectoryOnly))
            {
                fi.CopyTo(Path.Combine(target.FullName, fi.Name), true);
            }

            foreach (DirectoryInfo diSourceSubDir in source.GetDirectories())
            {
                DirectoryInfo nextTargetSubDir =
                    target.CreateSubdirectory(diSourceSubDir.Name);
                CopyAll(diSourceSubDir, nextTargetSubDir, searchPattern);
            }
        }
    }
}
