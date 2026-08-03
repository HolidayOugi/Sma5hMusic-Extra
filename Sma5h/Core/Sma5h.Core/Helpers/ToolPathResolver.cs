using System;
using System.IO;
using System.Linq;

namespace Sma5h.Helpers
{
    public static class ToolPathResolver
    {
        public static string Resolve(string toolsPath, string windowsRelativePath, params string[] unixRelativePaths)
        {
            if (string.IsNullOrWhiteSpace(toolsPath))
                throw new ArgumentException("The tools path must be configured.", nameof(toolsPath));

            var preferredPaths = OperatingSystem.IsWindows()
                ? new[] { windowsRelativePath }
                : unixRelativePaths.Concat(new[] { windowsRelativePath });

            var candidates = preferredPaths
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(path => Path.Combine(toolsPath, NormalizeSeparators(path)))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var resolvedPath = candidates.FirstOrDefault(File.Exists) ?? candidates[0];

            if (!OperatingSystem.IsWindows() && File.Exists(resolvedPath))
            //if on linux, set executable permission for programs
                EnsureExecutable(resolvedPath);

            return resolvedPath;
        }

        private static void EnsureExecutable(string path)
        {
            if (OperatingSystem.IsWindows())
                return;

            var mode = File.GetUnixFileMode(path);
            if ((mode & UnixFileMode.UserExecute) == 0)
                File.SetUnixFileMode(path, mode | UnixFileMode.UserExecute);
        }

        private static string NormalizeSeparators(string path)
        {
            return path
                .Replace('\\', Path.DirectorySeparatorChar)
                .Replace('/', Path.DirectorySeparatorChar);
        }
    }
}
