using Avalonia.Controls;
using DynamicData;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using ReactiveUI.Validation.Extensions;
using ReactiveUI.Validation.Helpers;
using Sma5h.Mods.Music.Helpers;
using Sma5h.Mods.Music.Models;
using Sma5hMusic.GUI.Interfaces;
using Sma5hMusic.GUI.Models;
using Sma5hMusic.GUI.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text.RegularExpressions;
using VGMMusic;

namespace Sma5hMusic.GUI.ViewModels
{
    public partial class ToneIdCreationModalWindowModel : ReactiveValidationObject, IDisposable
    {
        private readonly ILogger _logger;
        private readonly IViewModelManager _viewModelManager;
        private readonly ReadOnlyObservableCollection<BgmPropertyEntryViewModel> _bgmPropertyEntries;
        private readonly CompositeDisposable _subscriptions = new CompositeDisposable();
        private CoreSongReplacementOption _selectedCoreSongReplacement;
        private bool _disposed;
        private const string REGEX_REPLACE = @"[^a-zA-Z0-9_]";
        private const string REGEX_VALIDATION = @"^[a-z0-9_]+$";

        public ReactiveCommand<Window, Unit> ActionCancel { get; }
        public ReactiveCommand<Window, Unit> ActionCancelAll { get; }
        public ReactiveCommand<Window, Unit> ActionCreate { get; }
        public ReactiveCommand<Window, Unit> ActionReplaceCoreSong { get; }

        [Reactive]
        public string Filename { get; set; }

        [Reactive]
        public string ToneId { get; set; }

        [Reactive]
        public string QueueStatusText { get; set; }

        [Reactive]
        public bool IsQueueStatusVisible { get; set; }

        [Reactive]
        public bool IsCancelAllVisible { get; set; }

        public bool IsCancelAllRequested { get; private set; }

        public MusicModEntries NewMusicModEntries { get; private set; }

        public CoreSongReplacementOption SelectedCoreSongReplacement
        {
            get => _selectedCoreSongReplacement;
            private set => this.RaiseAndSetIfChanged(ref _selectedCoreSongReplacement, value);
        }

        public ToneIdCreationModalWindowModel(ILogger<ToneIdCreationModalWindowModel> logger, IViewModelManager viewModelManager, IAudioImportService audioImportService, IMessageDialog messageDialog, IVGMMusicPlayer musicPlayer)
        {
            _logger = logger;
            _viewModelManager = viewModelManager;
            _audioImportService = audioImportService;
            _messageDialog = messageDialog;
            _musicPlayer = musicPlayer;
            WindowHeight = 400;
            WindowWidth = 520;
            WindowMinWidth = 500;
            PreviewProgressText = string.Empty;
            AutoLoopStatus = string.Empty;
            AutoLoopPoints = new ObservableCollection<AutoLoopPoint>();

            //Bind observables
            _subscriptions.Add(this.WhenAnyValue(x => x.IsAudioImport, x => x.IsLoopPreviewOnly)
                .Subscribe(_ =>
                {
                    this.RaisePropertyChanged(nameof(WindowTitle));
                    this.RaisePropertyChanged(nameof(ResetLoopButtonText));
                }));

            _subscriptions.Add(this.WhenAnyValue(x => x.IsLoopPreviewOnly, x => x.IsImportingSong)
                .Subscribe(_ => this.RaisePropertyChanged(nameof(CanReplaceCoreSong))));

            _subscriptions.Add(viewModelManager.ObservableBgmPropertyEntries.Connect()
               .ObserveOn(RxApp.MainThreadScheduler)
               .Bind(out _bgmPropertyEntries)
               .DisposeMany()
               .Subscribe(_ => RefreshToneIdValidation()));

            this.ValidationRule(p => p.ToneId,
                p => !string.IsNullOrEmpty(p) && Regex.IsMatch(p, REGEX_VALIDATION),
                $"The ToneId can only contain lowercase letters, digits and underscore.");

            this.ValidationRule(p => p.ToneId,
              p => p != null && p.Length <= MusicConstants.GameResources.ToneIdMaximumSize,
              $"The ToneId is too long. Maximum is {MusicConstants.GameResources.ToneIdMaximumSize}");

            this.ValidationRule(p => p.ToneId,
             p => p != null && p.Length >= MusicConstants.GameResources.ToneIdMinimumSize,
             $"The ToneId is too short. Minimum is {MusicConstants.GameResources.ToneIdMinimumSize}");

            this.ValidationRule(p => p.ToneId,
               p => !string.IsNullOrEmpty(p) && IsToneIdAvailable(p),
               $"The ToneId already exists in the database");

            this.ValidationRule(p => p.LoopEndSample,
                p => !IsAudioImport || p > 0,
                "Loop end sample must be greater than 0.");

            this.ValidationRule(p => p.LoopStartSample,
                this.WhenAnyValue(p => p.IsAudioImport, p => p.LoopStartSample, p => p.LoopEndSample,
                    (isAudioImport, loopStartSample, loopEndSample) => !isAudioImport || loopStartSample <= loopEndSample),
                "Loop start sample must be lower than or equal to loop end sample.");

            this.ValidationRule(p => p.LoopEndSample,
                this.WhenAnyValue(p => p.IsAudioImport, p => p.LoopStartSample, p => p.LoopEndSample,
                    (isAudioImport, loopStartSample, loopEndSample) => !isAudioImport || loopStartSample <= loopEndSample),
                "Loop end sample must be greater than or equal to loop start sample.");

            this.ValidationRule(p => p.LoopEndSample,
                p => !IsAudioImport || p <= TotalSamples,
                "Loop end sample cannot be greater than the total sample count.");

            this.ValidationRule(p => p.LoopEndMs,
                p => !IsAudioImport || p > 0,
                "Loop end ms must be greater than 0.");

            this.ValidationRule(p => p.LoopStartMs,
                this.WhenAnyValue(p => p.IsAudioImport, p => p.LoopStartMs, p => p.LoopEndMs,
                    (isAudioImport, loopStartMs, loopEndMs) => !isAudioImport || loopStartMs <= loopEndMs),
                "Loop start ms must be lower than or equal to loop end ms.");

            this.ValidationRule(p => p.LoopEndMs,
                this.WhenAnyValue(p => p.IsAudioImport, p => p.LoopStartMs, p => p.LoopEndMs,
                    (isAudioImport, loopStartMs, loopEndMs) => !isAudioImport || loopStartMs <= loopEndMs),
                "Loop end ms must be greater than or equal to loop start ms.");

            this.ValidationRule(p => p.LoopEndMs,
                p => !IsAudioImport || p <= TotalTimeMs,
                "Loop end ms cannot be greater than the total length.");

            var canExecute = this.WhenAnyValue(x => x.ValidationContext.IsValid);
            var canPreview = this.WhenAnyValue(
                x => x.IsAudioImport,
                x => x.LoopStartSample,
                x => x.LoopEndSample,
                x => x.TotalSamples,
                (isAudioImport, loopStartSample, loopEndSample, totalSamples) =>
                    isAudioImport &&
                    loopEndSample > 0 &&
                    loopStartSample <= loopEndSample &&
                    loopEndSample <= totalSamples);
            var canCalculateAutoLoops = this.WhenAnyValue(x => x.IsAudioImport, x => x.IsCalculatingAutoLoops, (isAudioImport, isCalculating) => isAudioImport && !isCalculating);
            ActionCancel = ReactiveCommand.Create<Window>(Cancel);
            ActionCancelAll = ReactiveCommand.Create<Window>(CancelAll);
            ActionCreate = ReactiveCommand.Create<Window>(Select, canExecute);
            ActionReplaceCoreSong = ReactiveCommand.CreateFromTask<Window>(ReplaceCoreSong);
            ActionPreviewLoop = ReactiveCommand.CreateFromTask(PreviewLoop, canPreview);
            ActionStopPreview = ReactiveCommand.CreateFromTask(StopPreview);
            ActionResetLoopDefaults = ReactiveCommand.Create(ResetLoopDefaults);
            ActionCalculateAutoLoops = ReactiveCommand.CreateFromTask(CalculateAutoLoops, canCalculateAutoLoops);
            ActionLoadMoreAutoLoops = ReactiveCommand.Create(LoadMoreAutoLoops);
            ActionPreviewAutoLoop = ReactiveCommand.CreateFromTask<AutoLoopPoint>(PreviewAutoLoop);

            _subscriptions.Add(this.WhenAnyValue(p => p.LoopStartSample)
                .Subscribe(p => UpdateLoopStartMsFromSample(p)));

            _subscriptions.Add(this.WhenAnyValue(p => p.LoopEndSample)
                .Subscribe(p => UpdateLoopEndMsFromSample(p)));

            _subscriptions.Add(this.WhenAnyValue(p => p.LoopStartMs)
                .Subscribe(p => UpdateLoopStartSampleFromMs(p)));

            _subscriptions.Add(this.WhenAnyValue(p => p.LoopEndMs)
                .Subscribe(p => UpdateLoopEndSampleFromMs(p)));

            _subscriptions.Add(this.WhenAnyValue(p => p.LoopStartMinutes, p => p.LoopStartSeconds, p => p.LoopStartMilliseconds)
                .Subscribe(_ => UpdateLoopStartFromTimeParts()));

            _subscriptions.Add(this.WhenAnyValue(p => p.LoopEndMinutes, p => p.LoopEndSeconds, p => p.LoopEndMilliseconds)
                .Subscribe(_ => UpdateLoopEndFromTimeParts()));

            _subscriptions.Add(this.WhenAnyValue(p => p.SelectedAutoLoop)
                .Where(p => p != null)
                .Subscribe(ApplyAutoLoop));

            _subscriptions.Add(this.WhenAnyValue(p => p.ToneId)
                .Subscribe(p =>
                {
                    if (SelectedCoreSongReplacement != null &&
                        !string.Equals(SelectedCoreSongReplacement.ToneId, p, StringComparison.OrdinalIgnoreCase))
                    {
                        SelectedCoreSongReplacement = null;
                    }
                }));
        }

        public CoreSongReplacementOption GetCoreSongReplacement()
        {
            if (_viewModelManager == null)
                return null;

            if (SelectedCoreSongReplacement != null &&
                string.Equals(SelectedCoreSongReplacement.ToneId, ToneId, StringComparison.OrdinalIgnoreCase))
            {
                return SelectedCoreSongReplacement;
            }

            return GetCoreSongReplacementOptions()
                .FirstOrDefault(p => string.Equals(p.ToneId, ToneId, StringComparison.OrdinalIgnoreCase));
        }

        public void LoadToneId(string toneId)
        {
            SelectedCoreSongReplacement = null;
            var sanitizedToneId = Regex.Replace(toneId.Replace(" ", "_"), REGEX_REPLACE, string.Empty).ToLower();
            ToneId = string.IsNullOrEmpty(sanitizedToneId) ? Guid.NewGuid().ToString("N") : sanitizedToneId;
            RefreshToneIdValidation();
        }

        public void LoadQueueStatus(int songsRemaining)
        {
            IsQueueStatusVisible = songsRemaining > 0;
            IsCancelAllVisible = songsRemaining > 0;
            IsCancelAllRequested = false;
            QueueStatusText = songsRemaining == 1
                ? "1 Song left in queue..."
                : $"{songsRemaining} Songs left in queue...";
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            StopAutoLoopStatusAnimation();
            DisposePreviewProgressTimer();
            _subscriptions.Dispose();
        }

        private async void CancelAll(Window w)
        {
            IsCancelAllRequested = true;

            try
            {
                _logger.LogInformation("Tone ID modal cancel all requested. Stopping preview before close.");
                await ClosePreview();
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error while stopping loop preview on cancel all.");
            }
            w.Close();
        }

        private async void Cancel(Window w)
        {
            try
            {
                _logger.LogInformation("Tone ID modal cancel requested. Stopping preview before close.");
                await ClosePreview();
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error while stopping loop preview on cancel.");
            }
            w.Close();
        }

        private async void Select(Window window)
        {
            _logger.LogDebug("Clicked OK");
            try
            {
                _logger.LogInformation("Tone ID modal choose requested. Stopping preview before close.");
                await ClosePreview();
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error while stopping loop preview on choose.");
            }
            window.Close(window);
        }

        private async System.Threading.Tasks.Task ReplaceCoreSong(Window window)
        {
            var pickerViewModel = new CoreSongPickerModalWindowViewModel(GetCoreSongReplacementOptions());
            var pickerWindow = new CoreSongPickerModalWindow { DataContext = pickerViewModel };
            var result = await pickerWindow.ShowDialog<CoreSongPickerModalWindow>(window);

            if (result != null && pickerViewModel.SelectedSong != null)
            {
                SelectedCoreSongReplacement = pickerViewModel.SelectedSong.Option;
                ToneId = SelectedCoreSongReplacement.ToneId;
            }
        }

        private IEnumerable<CoreSongReplacementOption> GetCoreSongReplacementOptions()
        {
            return _viewModelManager.GetBgmDbRootEntriesViewModels()
                .Where(p => p.Source == EntrySource.Core &&
                            p.BgmPropertyViewModel != null &&
                            p.BgmPropertyViewModel.MusicMod == null &&
                            !string.IsNullOrWhiteSpace(p.ToneId))
                .Select(p => new CoreSongReplacementOption
                {
                    UiBgmId = p.UiBgmId,
                    UiSeriesId = p.SeriesId,
                    SeriesTitle = p.SeriesViewModel?.Title ?? p.SeriesId,
                    Title = p.Title,
                    ToneId = p.ToneId,
                    TestDispOrder = p.TestDispOrder
                });
        }

        private bool IsToneIdAvailable(string toneId)
        {
            if (_bgmPropertyEntries == null)
                return true;

            var matches = _bgmPropertyEntries
                .Where(p => string.Equals(p.NameId, toneId, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (matches.Count == 0)
                return true;

            if (matches.Any(p => p.Source == EntrySource.Mod))
                return false;

            var coreMatch = matches.FirstOrDefault(p => p.Source == EntrySource.Core);
            return coreMatch != null && coreMatch.MusicMod == null;
        }

        private void RefreshToneIdValidation()
        {
            this.RaisePropertyChanged(nameof(ToneId));
        }
    }
}
