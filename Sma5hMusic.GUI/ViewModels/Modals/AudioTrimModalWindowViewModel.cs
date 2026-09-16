using Avalonia.Controls;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using ReactiveUI.Validation.Extensions;
using ReactiveUI.Validation.Helpers;
using Sma5hMusic.GUI.Interfaces;
using System;
using System.IO;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using VGMMusic;

namespace Sma5hMusic.GUI.ViewModels
{
    public class AudioTrimModalWindowViewModel : ReactiveValidationObject, IDisposable
    {
        private readonly IAudioImportService _audioImportService;
        private readonly IMessageDialog _messageDialog;
        private readonly IVGMMusicPlayer _musicPlayer;
        private readonly CompositeDisposable _subscriptions = new CompositeDisposable();
        private readonly string _sourceWavFilename;
        private bool _isUpdatingFields;
        private bool _isClosing;
        private bool _isCompletingPreview;
        private int _previewVersion;
        private string _previewFilename;
        private uint _previewSourceStartSample;
        private uint _previewLengthSamples;
        private IDisposable _previewProgressSubscription;
        private readonly bool _hasOriginalLoopMarkers;
        private readonly uint _originalLoopStartMarker;
        private readonly uint _originalLoopEndMarker;

        public AudioTrimModalWindowViewModel(
            IAudioImportService audioImportService,
            IMessageDialog messageDialog,
            IVGMMusicPlayer musicPlayer,
            string displayFilename,
            string sourceWavFilename,
            uint sampleRate,
            uint totalSamples,
            float[] waveformPeaks,
            uint? loopStartSample = null,
            uint? loopEndSample = null)
        {
            _audioImportService = audioImportService;
            _messageDialog = messageDialog;
            _musicPlayer = musicPlayer;
            _sourceWavFilename = sourceWavFilename;
            Filename = displayFilename;
            SampleRate = sampleRate;
            TotalSamples = totalSamples;
            TotalTimeMs = SamplesToMs(totalSamples);
            WaveformPeaks = waveformPeaks ?? Array.Empty<float>();
            _hasOriginalLoopMarkers = loopStartSample.HasValue &&
                loopEndSample.HasValue &&
                loopEndSample.Value > 0 &&
                loopEndSample.Value <= totalSamples &&
                loopStartSample.Value <= loopEndSample.Value;
            _originalLoopStartMarker = _hasOriginalLoopMarkers ? loopStartSample.Value : 0;
            _originalLoopEndMarker = _hasOriginalLoopMarkers ? loopEndSample.Value : 0;
            TrimEndSample = totalSamples;

            this.ValidationRule(p => p.TrimStartSample,
                this.WhenAnyValue(p => p.TrimStartSample, p => p.TrimEndSample,
                    (start, end) => start < end),
                "Start sample must be lower than end sample.");
            this.ValidationRule(p => p.TrimEndSample,
                this.WhenAnyValue(p => p.TrimStartSample, p => p.TrimEndSample,
                    (start, end) => start < end),
                "End sample must be greater than start sample.");
            this.ValidationRule(p => p.TrimEndSample,
                p => p > 0 && p <= TotalSamples,
                "End sample cannot exceed the total sample count.");
            this.ValidationRule(p => p.TrimEndMs,
                p => p > 0 && p <= TotalTimeMs,
                "End ms cannot exceed the total length.");

            var canConfirm = this.WhenAnyValue(
                p => p.ValidationContext.IsValid,
                p => p.IsBusy,
                (isValid, isBusy) => isValid && !isBusy);
            var canPreview = this.WhenAnyValue(
                p => p.TrimStartSample,
                p => p.TrimEndSample,
                p => p.IsBusy,
                (start, end, isBusy) => !isBusy && start < end && end <= TotalSamples);

            ActionConfirm = ReactiveCommand.CreateFromTask<Window>(Confirm, canConfirm);
            ActionCancel = ReactiveCommand.CreateFromTask<Window>(Cancel);
            ActionPreviewStart = ReactiveCommand.CreateFromTask(() => Preview(true), canPreview);
            ActionPreviewEnd = ReactiveCommand.CreateFromTask(() => Preview(false), canPreview);
            ActionStopPreview = ReactiveCommand.CreateFromTask(StopPreview);

            _subscriptions.Add(this.WhenAnyValue(p => p.TrimStartSample)
                .Subscribe(value => UpdateStartMs(value)));
            _subscriptions.Add(this.WhenAnyValue(p => p.TrimEndSample)
                .Subscribe(value => UpdateEndMs(value)));
            _subscriptions.Add(this.WhenAnyValue(p => p.TrimStartSample, p => p.TrimEndSample)
                .Subscribe(_ =>
                {
                    this.RaisePropertyChanged(nameof(ShowLoopMarkers));
                    this.RaisePropertyChanged(nameof(LoopStartMarker));
                    this.RaisePropertyChanged(nameof(LoopEndMarker));
                }));
            _subscriptions.Add(this.WhenAnyValue(p => p.TrimStartMs)
                .Subscribe(value => UpdateStartSample(value)));
            _subscriptions.Add(this.WhenAnyValue(p => p.TrimEndMs)
                .Subscribe(value => UpdateEndSample(value)));
            _subscriptions.Add(this.WhenAnyValue(p => p.TrimStartMs, p => p.TrimEndMs)
                .Subscribe(_ => this.RaisePropertyChanged(nameof(TrimRangeText))));
            _subscriptions.Add(this.WhenAnyValue(
                    p => p.TrimStartMinutes,
                    p => p.TrimStartSeconds,
                    p => p.TrimStartMilliseconds)
                .Subscribe(_ => UpdateStartFromTimeParts()));
            _subscriptions.Add(this.WhenAnyValue(
                    p => p.TrimEndMinutes,
                    p => p.TrimEndSeconds,
                    p => p.TrimEndMilliseconds)
                .Subscribe(_ => UpdateEndFromTimeParts()));

            // Initialize both representations together 
            UpdateFields(() =>
            {
                TrimStartSample = 0;
                TrimEndSample = TotalSamples;
                TrimStartMs = 0;
                TrimEndMs = TotalTimeMs;
                SetStartTimeParts(0);
                SetEndTimeParts(TotalTimeMs);
            });
        }

        public ReactiveCommand<Window, Unit> ActionConfirm { get; }
        public ReactiveCommand<Window, Unit> ActionCancel { get; }
        public ReactiveCommand<Unit, Unit> ActionPreviewStart { get; }
        public ReactiveCommand<Unit, Unit> ActionPreviewEnd { get; }
        public ReactiveCommand<Unit, Unit> ActionStopPreview { get; }

        public string Filename { get; }
        public uint SampleRate { get; }
        public uint TotalSamples { get; }
        public uint TotalTimeMs { get; }
        public float[] WaveformPeaks { get; }
        public bool ShowLoopMarkers =>
            _hasOriginalLoopMarkers &&
            TrimStartSample < TrimEndSample &&
            TrimStartSample < _originalLoopEndMarker &&
            TrimEndSample > _originalLoopStartMarker;
        public uint LoopStartMarker => ClampMarkerToTrimRange(_originalLoopStartMarker);
        public uint LoopEndMarker => ClampMarkerToTrimRange(_originalLoopEndMarker);

        public string TrimRangeText => $"{FormatTrimTime(TrimStartMs)} / {FormatTrimTime(TrimEndMs)}";

        [Reactive]
        public uint TrimStartSample { get; set; }

        [Reactive]
        public uint TrimEndSample { get; set; }

        [Reactive]
        public uint TrimStartMs { get; set; }

        [Reactive]
        public uint TrimEndMs { get; set; }

        [Reactive]
        public uint TrimStartMinutes { get; set; }

        [Reactive]
        public uint TrimStartSeconds { get; set; }

        [Reactive]
        public uint TrimStartMilliseconds { get; set; }

        [Reactive]
        public uint TrimEndMinutes { get; set; }

        [Reactive]
        public uint TrimEndSeconds { get; set; }

        [Reactive]
        public uint TrimEndMilliseconds { get; set; }

        [Reactive]
        public bool IsBusy { get; set; }

        [Reactive]
        public bool IsPreviewProgressVisible { get; set; }

        [Reactive]
        public double PreviewProgressMaximum { get; set; }

        [Reactive]
        public double PreviewProgressValue { get; set; }

        [Reactive]
        public string PreviewProgressText { get; set; }

        public async Task PrepareForOpen()
        {
            await MusicPlayerViewModel.StopCurrentSong();
            await _musicPlayer.Stop();
        }

        public async Task CloseAsync()
        {
            if (_isClosing)
                return;

            _isClosing = true;
            await StopPreview();
            DeleteFile(_sourceWavFilename);
        }

        public void Dispose()
        {
            _subscriptions.Dispose();
            _ = CloseAsync();
        }

        private async Task Confirm(Window window)
        {
            try
            {
                IsBusy = true;
                await StopPreview();
                var trimmedFilename = await _audioImportService.TrimAudioWav(
                    _sourceWavFilename,
                    TrimStartSample,
                    TrimEndSample);
                DeleteFile(_sourceWavFilename);
                window.Close(trimmedFilename);
            }
            catch (Exception e)
            {
                await _messageDialog.ShowError("Audio trim failed", e.Message, e);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task Cancel(Window window)
        {
            await CloseAsync();
            window.Close();
        }

        private async Task Preview(bool previewStart)
        {
            try
            {
                IsBusy = true;
                await StopPreview();
                var previewVersion = ++_previewVersion;
                var previewFilename = await _audioImportService.CreateAudioTrimPreview(
                    _sourceWavFilename,
                    TrimStartSample,
                    TrimEndSample,
                    previewStart);

                //delete the preview file if the user has closed the window or started a new preview
                if (_isClosing || previewVersion != _previewVersion)
                {
                    DeleteFile(previewFilename);
                    if (_isClosing)
                        DeleteFile(_sourceWavFilename);
                    return;
                }

                _previewFilename = previewFilename;
                var previewInfo = await _audioImportService.GetAudioInfo(previewFilename);
                _previewLengthSamples = previewInfo.TotalSamples;
                _previewSourceStartSample = previewStart
                    ? TrimStartSample
                    : TrimEndSample - Math.Min(TrimEndSample - TrimStartSample, _previewLengthSamples);

                _musicPlayer.ApplyVolume = false;
                var played = await _musicPlayer.Play(_previewFilename, playForever: false);
                if (!played)
                    await _messageDialog.ShowError("Trim preview failed", "The preview file was created, but vgmstream could not play it.");
                else
                    StartPreviewProgress();
            }
            catch (Exception e)
            {
                await _messageDialog.ShowError("Trim preview failed", e.Message, e);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task StopPreview()
        {
            _previewVersion++;
            _isCompletingPreview = false;
            StopPreviewProgress();
            await _musicPlayer.Stop();
            if (!string.IsNullOrEmpty(_previewFilename))
            {
                DeleteFile(_previewFilename);
                _previewFilename = null;
            }

        }

        private void UpdateStartMs(uint samples)
        {
            if (_isUpdatingFields)
                return;
            UpdateFields(() =>
            {
                var milliseconds = SamplesToMs(samples);
                TrimStartMs = milliseconds;
                SetStartTimeParts(milliseconds);
            });
        }

        private void UpdateEndMs(uint samples)
        {
            if (_isUpdatingFields)
                return;
            UpdateFields(() =>
            {
                var milliseconds = SamplesToMs(samples);
                TrimEndMs = milliseconds;
                SetEndTimeParts(milliseconds);
            });
        }

        private void UpdateStartSample(uint milliseconds)
        {
            if (_isUpdatingFields)
                return;
            UpdateFields(() =>
            {
                TrimStartSample = MsToSamples(milliseconds);
                SetStartTimeParts(milliseconds);
            });
        }

        private void UpdateEndSample(uint milliseconds)
        {
            if (_isUpdatingFields)
                return;
            UpdateFields(() =>
            {
                TrimEndSample = MsToSamples(milliseconds);
                SetEndTimeParts(milliseconds);
            });
        }

        private void UpdateStartFromTimeParts()
        {
            if (_isUpdatingFields)
                return;

            UpdateFields(() =>
            {
                var milliseconds = ComposeMilliseconds(TrimStartMinutes, TrimStartSeconds, TrimStartMilliseconds);
                TrimStartMs = milliseconds;
                TrimStartSample = MsToSamples(milliseconds);
            });
        }

        private void UpdateEndFromTimeParts()
        {
            if (_isUpdatingFields)
                return;

            UpdateFields(() =>
            {
                var milliseconds = ComposeMilliseconds(TrimEndMinutes, TrimEndSeconds, TrimEndMilliseconds);
                TrimEndMs = milliseconds;
                TrimEndSample = MsToSamples(milliseconds);
            });
        }

        private void SetStartTimeParts(uint milliseconds)
        {
            SplitMilliseconds(milliseconds, out var minutes, out var seconds, out var remainingMilliseconds);
            TrimStartMinutes = minutes;
            TrimStartSeconds = seconds;
            TrimStartMilliseconds = remainingMilliseconds;
        }

        private void SetEndTimeParts(uint milliseconds)
        {
            SplitMilliseconds(milliseconds, out var minutes, out var seconds, out var remainingMilliseconds);
            TrimEndMinutes = minutes;
            TrimEndSeconds = seconds;
            TrimEndMilliseconds = remainingMilliseconds;
        }

        private void UpdateFields(Action update)
        {
            _isUpdatingFields = true;
            update();
            _isUpdatingFields = false;
        }

        private void StartPreviewProgress()
        {
            StopPreviewProgress();
            PreviewProgressMaximum = TotalSamples;
            IsPreviewProgressVisible = true;
            UpdatePreviewProgress();
            _previewProgressSubscription = Observable.Interval(TimeSpan.FromMilliseconds(50))
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(_ => UpdatePreviewProgress());
        }

        private void StopPreviewProgress()
        {
            _previewProgressSubscription?.Dispose();
            _previewProgressSubscription = null;
            IsPreviewProgressVisible = false;
            PreviewProgressValue = 0;
            PreviewProgressText = string.Empty;
        }

        private void UpdatePreviewProgress()
        {
            if (_previewLengthSamples == 0)
                return;

            var previewSample = (uint)Math.Max(0, _musicPlayer.CurrentSample);
            if (previewSample >= _previewLengthSamples)
            {
                PreviewProgressValue = Math.Min(TotalSamples, _previewSourceStartSample + _previewLengthSamples);
                PreviewProgressText = "Preview finished.";
                _ = CompletePreviewPlayback();
                return;
            }

            var sourceSample = Math.Min(TotalSamples, _previewSourceStartSample + previewSample);
            PreviewProgressValue = sourceSample;
            PreviewProgressText = $"Preview: {FormatTime(SamplesToMs(sourceSample))} / {FormatTime(TotalTimeMs)}";
        }

        private async Task CompletePreviewPlayback()
        {
            if (_isCompletingPreview)
                return;

            _isCompletingPreview = true;
            await StopPreview();
        }

        private uint SamplesToMs(uint samples)
        {
            return SampleRate == 0 ? 0 : (uint)Math.Round(samples * 1000.0 / SampleRate);
        }

        private uint MsToSamples(uint milliseconds)
        {
            if (SampleRate == 0)
                return 0;

            var samples = Math.Round(milliseconds * (double)SampleRate / 1000.0);
            return samples >= TotalSamples ? TotalSamples : (uint)Math.Max(0, samples);
        }

        //Loop markers
        private uint ClampMarkerToTrimRange(uint marker)
        {
            var rangeStart = Math.Min(TrimStartSample, TrimEndSample);
            var rangeEnd = Math.Max(TrimStartSample, TrimEndSample);
            return Math.Min(rangeEnd, Math.Max(rangeStart, marker));
        }

        private static uint ComposeMilliseconds(uint minutes, uint seconds, uint milliseconds)
        {
            var total = minutes * 60000.0 + seconds * 1000.0 + milliseconds;
            return total >= uint.MaxValue ? uint.MaxValue : (uint)Math.Round(total);
        }

        private static void SplitMilliseconds(
            uint milliseconds,
            out uint minutes,
            out uint seconds,
            out uint remainingMilliseconds)
        {
            minutes = milliseconds / 60000;
            var remainder = milliseconds % 60000;
            seconds = remainder / 1000;
            remainingMilliseconds = remainder % 1000;
        }

        private static string FormatTime(uint milliseconds)
        {
            var time = TimeSpan.FromMilliseconds(milliseconds);
            return time.TotalHours >= 1 ? time.ToString(@"h\:mm\:ss\.fff") : time.ToString(@"m\:ss\.fff");
        }

        private static string FormatTrimTime(uint milliseconds)
        {
            var minutes = milliseconds / 60000;
            var seconds = (milliseconds % 60000) / 1000;
            var remainingMilliseconds = milliseconds % 1000;
            return $"{minutes:00}:{seconds:00}:{remainingMilliseconds:000}";
        }

        private static void DeleteFile(string filename)
        {
            try
            {
                if (!string.IsNullOrEmpty(filename) && File.Exists(filename))
                    File.Delete(filename);
            }
            catch
            {
            }
        }
    }
}
