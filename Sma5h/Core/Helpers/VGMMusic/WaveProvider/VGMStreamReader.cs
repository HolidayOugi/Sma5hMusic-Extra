//From lioncash
//https://github.com/lioncash/vgmstreamSharp
using NAudio.Wave;
using System;
using VGMMusic.Native;

namespace VGMMusic
{
    /// <summary>
    /// Class for VGMStream playback.
    /// </summary>
    public sealed class VGMStreamReader : WaveStream, IDisposable
    {
        private readonly WaveFormat _waveFormat;
        private IntPtr _vgmstream;

        private readonly int _totalSamplesToPlay; // Total samples to play
        private readonly int _channels;      // Number of channels this VGMSTREAM uses.
        private readonly int _sampleRate;    // Sample rate of this VGMSTREAM.
        private readonly int _bytesPerSampleFrame;
        private readonly int _loopStartSample;
        private readonly int _loopEndSample;
        private readonly int _totalSamples;
        private readonly bool _fileLoaded = false;
        private int _totalPlayed = 0;
        private bool _disposed;

        public int TotalPlayed { get { return _totalPlayed / _sampleRate; } }
        public int TotalPlayedSamples { get { return _totalPlayed; } }

        public int TotalSamplesToPlay { get { return _totalSamplesToPlay; } }
        public int TotalSecondsToPlay { get { return _totalSamplesToPlay / _sampleRate; } }
        public int LoopStartSample { get { return _loopStartSample; } }
        public int LoopEndSample { get { return _loopEndSample; } }
        public int TotalSamples { get { return _totalSamples; } }
        public int LoopStartMilliseconds { get { return (int)(_loopStartSample / (_sampleRate / 1000.00)); } }
        public int LoopEndMilliseconds { get { return (int)(_loopEndSample / (_sampleRate / 1000.00)); } }
        public int TotalMilliseconds { get { return (int)(_totalSamples / (_sampleRate / 1000.00)); } }
        public bool FileLoaded { get { return _fileLoaded; } }

        public VGMStreamReader(string filename)
        {
            _vgmstream = VGMStreamNative.InitVGMStream(filename);
            if (_vgmstream == IntPtr.Zero)
            {
                _fileLoaded = false;
                return;
            }

            _fileLoaded = true;
            _sampleRate = VGMStreamNative.GetVGMStreamSampleRate(_vgmstream);
            _channels = VGMStreamNative.GetVGMStreamChannelCount(_vgmstream);
            _bytesPerSampleFrame = _channels * sizeof(short);
            _totalSamplesToPlay = ToInt32Sample(VGMStreamNative.GetVGMStreamPlaySamples(_vgmstream));

            _loopStartSample = ToInt32Sample(VGMStreamNative.GetVGMStreamLoopStartSample(_vgmstream));
            var loopEndSample = ToInt32Sample(VGMStreamNative.GetVGMStreamLoopEndSample(_vgmstream));
            _loopEndSample = loopEndSample > 0 ? loopEndSample - 1 : 0; // Smash values are inclusive.
            _totalSamples = ToInt32Sample(VGMStreamNative.GetVGMStreamTotalSamples(_vgmstream));

            _waveFormat = new WaveFormat(_sampleRate, 16, _channels);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (!_fileLoaded || _disposed || count <= 0)
                return 0;

            var requestedSamples = count / _bytesPerSampleFrame;
            if (requestedSamples <= 0)
                return 0;

            var sampleBuffer = new short[requestedSamples * _channels];
            var samplesRead = VGMStreamNative.FillVGMStream(sampleBuffer, requestedSamples, _vgmstream);
            var bytesRead = samplesRead * _bytesPerSampleFrame;

            if (bytesRead > 0)
                Buffer.BlockCopy(sampleBuffer, 0, buffer, offset, bytesRead);

            _totalPlayed += samplesRead;
            return bytesRead;
        }

        public override WaveFormat WaveFormat
        {
            get { return _waveFormat; }
        }

        public override long Length
        {
            get
            {
                return (long)_totalSamplesToPlay * _bytesPerSampleFrame;
            }
        }

        public override long Position
        {
            get
            {
                return _totalPlayed * _bytesPerSampleFrame;
            }
            set
            {
                var sample = _bytesPerSampleFrame == 0 ? 0 : (int)(value / _bytesPerSampleFrame);
                SeekToSample(sample);
            }
        }

        public void ResetVGM()
        {
            _totalPlayed = 0;
            VGMStreamNative.ResetVGMStream(_vgmstream);
        }

        public void SeekToSample(int sample)
        {
            if (!_fileLoaded)
                return;

            if (sample < 0)
                sample = 0;

            if (sample > _totalSamples)
                sample = _totalSamples;

            VGMStreamNative.SeekVGMStream(_vgmstream, sample);
            _totalPlayed = sample;
        }

        protected override void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                _disposed = true;
                VGMStreamNative.CloseVGMStream(_vgmstream);
                _vgmstream = IntPtr.Zero;
            }

            base.Dispose(disposing);
        }

        private static int ToInt32Sample(long sample)
        {
            if (sample <= 0)
                return 0;

            return sample >= int.MaxValue ? int.MaxValue : (int)sample;
        }
    }
}
