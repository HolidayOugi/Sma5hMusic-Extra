using NAudio.Wave;
using Sma5hMusic.GUI.Models;
using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;

namespace Sma5hMusic.GUI.Services
{
    public partial class AudioImportService
    {
        //create a temporary WAV file from the original audio file
        public Task<string> PrepareAudioTrimWav(string filename)
        {
            return Task.Run(() =>
            {
                if (!File.Exists(filename))
                    throw new FileNotFoundException($"The audio file '{filename}' could not be found.", filename);

                var trimPath = GetAudioTrimTempPath();
                Directory.CreateDirectory(trimPath);
                var outputFile = Path.Combine(trimPath, $"{Guid.NewGuid():N}_source.wav");

                try
                {
                    RunFfmpeg(
                        "-y",
                        "-i", filename,
                        "-map", "0:a:0",
                        "-vn",
                        "-c:a", "pcm_s16le",
                        outputFile);

                    EnsureTrimFileCreated(outputFile, "Audio conversion");
                    return outputFile;
                }
                catch
                {
                    DeleteTempFile(outputFile);
                    throw;
                }
            });
        }

        public Task<float[]> GetAudioWaveformPeaks(string wavFilename, int pointCount)
        {
            return Task.Run(() =>
            {
                if (!File.Exists(wavFilename))
                    throw new FileNotFoundException($"The WAV file '{wavFilename}' could not be found.", wavFilename);
                if (pointCount < 2)
                    throw new ArgumentOutOfRangeException(nameof(pointCount), "At least two waveform points are required.");

                //math stuff to get the waveform peaks from the audio file
                using var reader = new WaveFileReader(wavFilename);
                var sampleProvider = reader.ToSampleProvider();
                var channels = Math.Max(1, reader.WaveFormat.Channels);
                var totalFrames = Math.Max(1L, reader.Length / reader.WaveFormat.BlockAlign);
                var peaks = new float[pointCount]; //sampled, not all frames
                var sampleBuffer = new float[4096 * channels];
                long frameIndex = 0;
                int samplesRead;

                while ((samplesRead = sampleProvider.Read(sampleBuffer, 0, sampleBuffer.Length)) > 0)
                {
                    for (var sampleIndex = 0; sampleIndex < samplesRead; sampleIndex += channels)
                    {
                        var framePeak = 0f;
                        var channelsInFrame = Math.Min(channels, samplesRead - sampleIndex);
                        for (var channel = 0; channel < channelsInFrame; channel++)
                            //get the absolute peak value for all channels
                            //ie stereo track, loudest channel is the peak
                            framePeak = Math.Max(framePeak, Math.Abs(sampleBuffer[sampleIndex + channel]));

                        //bucket for graphing later
                        var bucket = (int)Math.Min(pointCount - 1L, frameIndex * pointCount / totalFrames);
                        peaks[bucket] = Math.Max(peaks[bucket], framePeak);
                        frameIndex++;
                    }
                }

                //normalize the peaks to 0-1 range
                var maximumPeak = 0f;
                foreach (var peak in peaks)
                    maximumPeak = Math.Max(maximumPeak, peak);

                if (maximumPeak > 0)
                {
                    for (var index = 0; index < peaks.Length; index++)
                        peaks[index] = Math.Min(1f, peaks[index] / maximumPeak);
                }

                return peaks;
            });
        }

        //mirrors Choose Loops preview
        public Task<string> CreateAudioTrimPreview(
            string wavFilename,
            uint startSample,
            uint endSample,
            bool previewStart)
        {
            return Task.Run(() =>
            {
                var info = ValidateTrimRange(wavFilename, startSample, endSample);
                var previewSampleCount = Math.Max(1u, Math.Min(
                    endSample - startSample,
                    SaturatingMultiply(info.SampleRate, GetLoopPreviewSeconds())));
                var previewStartSample = previewStart
                    ? startSample
                    : endSample - previewSampleCount;
                var previewEndSample = previewStartSample + previewSampleCount;
                var outputFile = Path.Combine(GetAudioTrimTempPath(), $"{Guid.NewGuid():N}_preview.wav");

                Directory.CreateDirectory(GetAudioTrimTempPath());
                try
                {
                    TrimWavWithFfmpeg(wavFilename, outputFile, previewStartSample, previewEndSample);
                    EnsureTrimFileCreated(outputFile, "Trim preview");
                    return outputFile;
                }
                catch
                {
                    DeleteTempFile(outputFile);
                    throw;
                }
            });
        }

        //trims source audio file
        public Task<string> TrimAudioWav(string wavFilename, uint startSample, uint endSample)
        {
            return Task.Run(() =>
            {
                ValidateTrimRange(wavFilename, startSample, endSample);
                var outputFile = Path.Combine(GetAudioTrimTempPath(), $"{Guid.NewGuid():N}_trimmed.wav");

                Directory.CreateDirectory(GetAudioTrimTempPath());
                try
                {
                    TrimWavWithFfmpeg(wavFilename, outputFile, startSample, endSample);
                    EnsureTrimFileCreated(outputFile, "Audio trim");
                    return outputFile;
                }
                catch
                {
                    DeleteTempFile(outputFile);
                    throw;
                }
            });
        }

        private AudioImportInfo ValidateTrimRange(string wavFilename, uint startSample, uint endSample)
        {
            if (!File.Exists(wavFilename))
                throw new FileNotFoundException($"The WAV file '{wavFilename}' could not be found.", wavFilename);

            var info = GetAudioInfo(wavFilename).GetAwaiter().GetResult();
            if (endSample == 0 || endSample > info.TotalSamples)
                throw new InvalidOperationException($"Trim end sample must be between 1 and {info.TotalSamples}.");
            if (startSample >= endSample)
                throw new InvalidOperationException("Trim start sample must be lower than trim end sample.");

            return info;
        }

        private void TrimWavWithFfmpeg(string inputFile, string outputFile, uint startSample, uint endSample)
        {
            var filter = string.Format(
                CultureInfo.InvariantCulture,
                "atrim=start_sample={0}:end_sample={1},asetpts=PTS-STARTPTS",
                startSample,
                endSample);

            RunFfmpeg(
                "-y",
                "-i", inputFile,
                "-map", "0:a:0",
                "-vn",
                "-af", filter,
                "-c:a", "pcm_s16le",
                outputFile);
        }

        private string GetAudioTrimTempPath()
        {
            return Path.Combine(GetTempPath(), "AudioTrim");
        }

        //workaround for uint overflow when multiplying sample rate by seconds
        private static uint SaturatingMultiply(uint left, uint right)
        {
            var value = (ulong)left * right;
            return value >= uint.MaxValue ? uint.MaxValue : (uint)value;
        }

        private static void EnsureTrimFileCreated(string filename, string operation)
        {
            if (!File.Exists(filename) || new FileInfo(filename).Length == 0)
                throw new InvalidOperationException($"{operation} completed, but the WAV file could not be created.");
        }
    }
}
