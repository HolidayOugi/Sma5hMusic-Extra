using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Sma5hMusic.GUI.Services
{
    public partial class AudioImportService
    {

        public bool IsFfmpegConfigured()
        {
            var executable = _config.CurrentValue.FfmpegPath;
            return !string.IsNullOrWhiteSpace(executable) && File.Exists(executable);
        }

        public async Task<string> NormalizeNus3Audio(
            string toneId,
            string filename,
            string modPath,
            CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!IsNus3Audio(filename) && !IsGameAudio(filename))
                    throw new InvalidOperationException($"'{Path.GetFileName(filename)}' is not a supported normalization file.");

                Directory.CreateDirectory(modPath);
                Directory.CreateDirectory(GetTempPath());

                var outputFile = Path.Combine(modPath, $"{toneId}.nus3audio");

                if (File.Exists(outputFile))
                    throw new InvalidOperationException($"The destination file '{Path.GetFileName(outputFile)}' already exists in the selected mod.");

                return NormalizeNus3AudioToFile(toneId, filename, outputFile, cancellationToken);
            }, cancellationToken);
        }

        public async Task<string> NormalizeExistingNus3Audio(
            string toneId,
            string filename)
        {
            return await Task.Run(() =>
            {
                if (!IsNus3Audio(filename) && !IsGameAudio(filename))
                    throw new InvalidOperationException($"'{Path.GetFileName(filename)}' is not a supported normalization file.");

                if (!File.Exists(filename))
                    throw new FileNotFoundException($"The audio file '{filename}' could not be found.", filename);

                Directory.CreateDirectory(GetTempPath());

                var tempId = Guid.NewGuid().ToString("N");
                var normalizedNus3AudioFile = Path.Combine(GetTempPath(), $"{tempId}.nus3audio");
                var outputFile = IsNus3Audio(filename)
                    ? filename
                    : Path.Combine(Path.GetDirectoryName(filename) ?? string.Empty, $"{toneId}.nus3audio");

                try
                {
                    NormalizeNus3AudioToFile(toneId, filename, normalizedNus3AudioFile);
                    File.Copy(normalizedNus3AudioFile, outputFile, true);
                    if (!string.Equals(outputFile, filename, StringComparison.OrdinalIgnoreCase))
                        File.Delete(filename);

                    return outputFile;
                }
                finally
                {
                    DeleteTempFile(normalizedNus3AudioFile);
                }
            });
        }

        public async Task<string> UpdateExistingNus3AudioLoopPoints(
            string toneId,
            string filename,
            uint loopStartSample,
            uint loopEndSample)
        {
            return await Task.Run(() =>
            {
                if (!IsNus3Audio(filename) && !IsGameAudio(filename))
                    throw new InvalidOperationException($"'{Path.GetFileName(filename)}' is not a supported audio file.");

                if (!File.Exists(filename))
                    throw new FileNotFoundException($"The audio file '{filename}' could not be found.", filename);

                Directory.CreateDirectory(GetTempPath());

                var tempId = Guid.NewGuid().ToString("N");
                var updatedNus3AudioFile = Path.Combine(GetTempPath(), $"{tempId}.nus3audio");
                var outputFile = IsNus3Audio(filename)
                    ? filename
                    : Path.Combine(Path.GetDirectoryName(filename) ?? string.Empty, $"{toneId}.nus3audio");

                try
                {
                    //create a new nus3audio file with updated loop points
                    //if original was not nus3audio, create a new nus3audio file and delete the original file
                    UpdateNus3AudioLoopPointsToFile(toneId, filename, updatedNus3AudioFile, loopStartSample, loopEndSample);
                    File.Copy(updatedNus3AudioFile, outputFile, true);
                    if (!string.Equals(outputFile, filename, StringComparison.OrdinalIgnoreCase))
                        File.Delete(filename);

                    return outputFile;
                }
                finally
                {
                    DeleteTempFile(updatedNus3AudioFile);
                }
            });
        }

        private string UpdateNus3AudioLoopPointsToFile(
            string toneId,
            string filename,
            string outputFile,
            uint loopStartSample,
            uint loopEndSample)
        {
            var tempId = Guid.NewGuid().ToString("N");
            //double wav because sample rates may not match
            var extractedWavFile = Path.Combine(GetTempPath(), $"{tempId}_source.wav");
            var tempWavFile = Path.Combine(GetTempPath(), $"{tempId}.wav");
            var tempLopusFile = Path.Combine(GetTempPath(), $"{tempId}.lopus");

            try
            {
                //NUS3AUDIO -> WAV
                ExtractAudioToWavFile(filename, extractedWavFile);

                var extractedInfo = GetAudioInfo(extractedWavFile).GetAwaiter().GetResult();
                var loopStart48k = ConvertSampleRate(loopStartSample, extractedInfo.SampleRate);
                var loopEnd48k = ConvertSampleRate(loopEndSample, extractedInfo.SampleRate);
                var encoderWavFile = extractedWavFile;

                if (extractedInfo.SampleRate != TargetSampleRate)
                {
                    RunTool(
                        GetSoxExe(),
                        extractedWavFile,
                        "-r", TargetSampleRate.ToString(CultureInfo.InvariantCulture),
                        "-b", "16",
                        "-e", "signed-integer",
                        tempWavFile
                    );

                    encoderWavFile = tempWavFile;
                }

                //get new loop points
                (loopStart48k, loopEnd48k) = FitLoopPointsToWav(encoderWavFile, loopStart48k, loopEnd48k);

                _logger.LogInformation(
                    "Re-encoding existing NUS3AUDIO with new loop points {LoopStart}-{LoopEnd}. File={File}.",
                    loopStart48k,
                    loopEnd48k,
                    filename
                );

                //WAV -> LOPUS
                var encoderOutput = RunTool(
                    GetVGAudioCliExe(),
                    encoderWavFile,
                    tempLopusFile,
                    "-l",
                    $"{loopStart48k}-{loopEnd48k}",
                    "--bitrate",
                    "64000",
                    "--cbr",
                    "--opusheader",
                    "namco"
                );

                EnsureLopusCreated(tempLopusFile, encoderOutput);

                _logger.LogInformation("Creating loop-updated NUS3AUDIO {OutputFile}.", outputFile);

                //LOPUS -> NUS3AUDIO
                RunTool(GetNus3AudioExe(), "-n", "-w", outputFile);
                RunTool(GetNus3AudioExe(), "-A", toneId, tempLopusFile, "-w", outputFile);

                return outputFile;
            }
            finally
            {
                DeleteTempFile(extractedWavFile);
                DeleteTempFile(tempWavFile);
                DeleteTempFile(tempLopusFile);
            }
        }

        private string NormalizeNus3AudioToFile(
            string toneId,
            string filename,
            string outputFile,
            CancellationToken cancellationToken = default)
        {
            var tempId = Guid.NewGuid().ToString("N");
            var extractedWavFile = Path.Combine(GetTempPath(), $"{tempId}_source.wav");
            var normalizedWavFile = Path.Combine(GetTempPath(), $"{tempId}_normalized.wav");
            var tempLopusFile = Path.Combine(GetTempPath(), $"{tempId}.lopus");

            try
            {
                //audio file -> WAV
                var sourceInfo = ExtractAudioInfo(filename, cancellationToken);
                ExtractAudioToWavFile(filename, extractedWavFile, cancellationToken);

                _logger.LogInformation(
                    "Extracted audio loop points. File={File}, SampleRate={SampleRate}, LoopStart={LoopStart}, LoopEnd={LoopEnd}.",
                    filename,
                    sourceInfo.SampleRate,
                    sourceInfo.LoopStartSample,
                    sourceInfo.LoopEndSample
                );

                var targetLufs = GetFfmpegLoudnormTarget();

                _logger.LogInformation(
                    "Normalizing extracted NUS3AUDIO WAV. Input={InputFile}, Output={OutputFile}, TargetLUFS={TargetLUFS}.",
                    extractedWavFile,
                    normalizedWavFile,
                    targetLufs
                );

                //normalize WAV
                NormalizeAudioToWav(extractedWavFile, normalizedWavFile, targetLufs, cancellationToken);

                //WAV -> LOPUS
                var encoderArguments = new List<string>
                {
                    normalizedWavFile,
                    tempLopusFile
                };

                if (sourceInfo.HasLoopPoints)
                {
                    var loopStart48k = ConvertSampleRate(sourceInfo.LoopStartSample, sourceInfo.SampleRate);
                    var loopEnd48k = ConvertSampleRate(sourceInfo.LoopEndSample, sourceInfo.SampleRate);

                    _logger.LogInformation(
                        "Encoding normalized NUS3AUDIO WAV to LOPUS with old loop points {LoopStart}-{LoopEnd}.",
                        loopStart48k,
                        loopEnd48k
                    );

                    encoderArguments.Add("-l");
                    encoderArguments.Add($"{loopStart48k}-{loopEnd48k}");
                }

                encoderArguments.AddRange(new[]
                {
                    "--bitrate",
                    "64000",
                    "--cbr",
                    "--opusheader",
                    "namco"
                });

                var encoderOutput = RunTool(cancellationToken, GetVGAudioCliExe(), encoderArguments.ToArray());

                EnsureLopusCreated(tempLopusFile, encoderOutput);

                _logger.LogInformation("Creating normalized NUS3AUDIO {OutputFile}.", outputFile);

                //LOPUS -> NUS3AUDIO
                RunTool(cancellationToken, GetNus3AudioExe(), "-n", "-w", outputFile);
                RunTool(cancellationToken, GetNus3AudioExe(), "-A", toneId, tempLopusFile, "-w", outputFile);

                return outputFile;
            }
            finally
            {
                DeleteTempFile(extractedWavFile);
                DeleteTempFile(normalizedWavFile);
                DeleteTempFile(tempLopusFile);
            }
        }

        //parse loop points from vgmstream output
        //TODO: use built in method directly from NUS3audio import for consistency
        private AudioInfo ExtractAudioInfo(string filename, CancellationToken cancellationToken = default)
        {
            var output = RunTool(cancellationToken, GetVgmStreamExe(), "-m", filename);

            uint? sampleRate = null;
            uint? totalSamples = null;
            uint? loopStart = null;
            uint? loopEnd = null;

            foreach (var line in output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var sampleRateMatch = Regex.Match(line, @"sample rate:\s*(\d+)", RegexOptions.IgnoreCase);
                var totalSamplesMatch = Regex.Match(line, @"stream total samples:\s*(\d+)", RegexOptions.IgnoreCase);
                var startMatch = Regex.Match(line, @"loop start:\s*(\d+)", RegexOptions.IgnoreCase);
                var endMatch = Regex.Match(line, @"loop end:\s*(\d+)", RegexOptions.IgnoreCase);

                if (sampleRateMatch.Success && uint.TryParse(sampleRateMatch.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedSampleRate))
                    sampleRate = parsedSampleRate;

                if (totalSamplesMatch.Success && uint.TryParse(totalSamplesMatch.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedTotalSamples))
                    totalSamples = parsedTotalSamples;

                if (startMatch.Success && uint.TryParse(startMatch.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedLoopStart))
                    loopStart = parsedLoopStart;

                if (endMatch.Success && uint.TryParse(endMatch.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedLoopEnd))
                    loopEnd = parsedLoopEnd;
            }

            if (sampleRate.HasValue)
            {
                var info = new AudioInfo
                {
                    SampleRate = sampleRate.Value,
                    TotalSamples = totalSamples.GetValueOrDefault(),
                    HasLoopPoints = loopStart.HasValue && loopEnd.HasValue
                };

                if (info.HasLoopPoints)
                {
                    info.LoopStartSample = loopStart.Value;
                    info.LoopEndSample = loopEnd.Value;
                }

                return info;
            }

            throw new InvalidOperationException($"Could not read audio metadata from '{Path.GetFileName(filename)}'.");
        }

        private class AudioInfo
        {
            public uint SampleRate { get; set; }
            public uint TotalSamples { get; set; }
            public uint LoopStartSample { get; set; }
            public uint LoopEndSample { get; set; }
            public bool HasLoopPoints { get; set; }
        }

        private double GetAudioNormalizationTargetLufs()
        {
            var value = _config.CurrentValue.Sma5hMusicGUI?.AudioNormalizationTargetLufs ?? 0;

            return value > 0
                ? value
                : 14;
        }

        private double GetFfmpegLoudnormTarget()
        {
            return -Math.Abs(GetAudioNormalizationTargetLufs());
        }

        //normalize to LUFS using FFMpeg loudnorm filter
        private void NormalizeAudioToWav(string inputFile, string outputFile, double targetLufs, CancellationToken cancellationToken = default)
        {
            var outputDirectory = Path.GetDirectoryName(outputFile);
            if (!string.IsNullOrWhiteSpace(outputDirectory))
                Directory.CreateDirectory(outputDirectory);

            var firstPassFilter = string.Format(
                CultureInfo.InvariantCulture,
                "loudnorm=I={0}:TP=-1:LRA=11:print_format=json",
                targetLufs
            );

            var firstPassOutput = RunFfmpeg(
                cancellationToken,
                "-y",
                "-i", inputFile,
                "-af", firstPassFilter,
                "-f", "null",
                "-"
            );

            var stats = ParseLoudnormStats(firstPassOutput);

            var secondPassFilter = string.Format(
                CultureInfo.InvariantCulture,
                "loudnorm=I={0}:TP=-1:LRA=11:measured_I={1}:measured_LRA={2}:measured_TP={3}:measured_thresh={4}:offset={5}:linear=true:print_format=summary",
                targetLufs,
                stats["input_i"],
                stats["input_lra"],
                stats["input_tp"],
                stats["input_thresh"],
                stats["target_offset"]
            );

            RunFfmpeg(
                cancellationToken,
                "-y",
                "-i", inputFile,
                "-af", secondPassFilter,
                "-ar", TargetSampleRate.ToString(CultureInfo.InvariantCulture),
                "-acodec", "pcm_s16le",
                outputFile
            );

            if (!File.Exists(outputFile))
                throw new InvalidOperationException("Audio normalization completed, but the normalized WAV file could not be found.");
        }

        private Dictionary<string, string> ParseLoudnormStats(string output)
        {
            var keys = new[]
            {
                "input_i",
                "input_tp",
                "input_lra",
                "input_thresh",
                "target_offset"
            };

            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var key in keys)
            {
                var match = Regex.Match(
                    output,
                    $"\"{Regex.Escape(key)}\"\\s*:\\s*\"?([^\",\\r\\n}}]+)\"?",
                    RegexOptions.IgnoreCase
                );

                if (!match.Success)
                    throw new InvalidOperationException($"Could not read loudnorm value '{key}' from ffmpeg output.");

                result[key] = match.Groups[1].Value.Trim();
            }

            return result;
        }

        private string RunFfmpeg(params string[] arguments)
        {
            return RunFfmpeg(CancellationToken.None, arguments);
        }

        private string RunFfmpeg(CancellationToken cancellationToken, params string[] arguments)
        {
            var executable = _config.CurrentValue.FfmpegPath;

            if (string.IsNullOrWhiteSpace(executable) || !File.Exists(executable))
                throw new FileNotFoundException("ffmpeg.exe is not configured. Set its path in Global Settings.", executable);

            _logger.LogInformation(
                "Running ffmpeg: {Executable} {Arguments}",
                executable,
                string.Join(" ", arguments.Select(p => $"\"{p}\""))
            );

            var startInfo = new ProcessStartInfo
            {
                FileName = executable,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            // Prevent ffmpeg from trying to read interactive input.
            startInfo.ArgumentList.Add("-nostdin");

            foreach (var argument in arguments)
                startInfo.ArgumentList.Add(argument);

            using var process = new Process
            {
                StartInfo = startInfo,
                EnableRaisingEvents = true
            };

            var stdout = new System.Text.StringBuilder();
            var stderr = new System.Text.StringBuilder();

            process.OutputDataReceived += (sender, e) =>
            {
                if (string.IsNullOrWhiteSpace(e.Data))
                    return;

                stdout.AppendLine(e.Data);
                _logger.LogInformation("ffmpeg stdout: {Line}", e.Data);
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (string.IsNullOrWhiteSpace(e.Data))
                    return;

                stderr.AppendLine(e.Data);
                _logger.LogInformation("ffmpeg stderr: {Line}", e.Data);
            };

            process.Start();

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            using var cancellationRegistration = cancellationToken.Register(() =>
            {
                try
                {
                    if (!process.HasExited)
                        process.Kill(true);
                }
                catch
                {
                }
            });

            process.WaitForExit();
            cancellationToken.ThrowIfCancellationRequested();

            var output = stdout.ToString();
            var error = stderr.ToString();

            _logger.LogInformation(
                "ffmpeg exited. ExitCode={ExitCode}. StdOut={StdOut}. StdErr={StdErr}",
                process.ExitCode,
                output,
                error
            );

            if (process.ExitCode != 0)
                throw new InvalidOperationException($"ffmpeg failed: {error}{output}");

            return string.Join(
                Environment.NewLine,
                new[] { output, error }.Where(p => !string.IsNullOrWhiteSpace(p))
            );
        }

    }
}
