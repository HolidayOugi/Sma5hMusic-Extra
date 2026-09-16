using AutoMapper;
using Avalonia.Controls;
using Avalonia.Threading;
using DynamicData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using ReactiveUI.Validation.Extensions;
using Sma5h.Mods.Music;
using Sma5h.Mods.Music.Helpers;
using Sma5hMusic.GUI.Helpers;
using Sma5hMusic.GUI.Interfaces;
using Sma5hMusic.GUI.Models;
using Sma5hMusic.GUI.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using VGMMusic;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Sma5hMusic.GUI.ViewModels
{
    public class BgmPropertiesModalWindowViewModel : ModalBaseViewModel<BgmEntryViewModel>
    {
        private readonly IOptionsMonitor<ApplicationSettings> _config;
        private readonly IMapper _mapper;
        private readonly ILogger _logger;
        private readonly IFileDialog _fileDialog;
        private readonly IGUIStateManager _guiStateManager;
        private readonly IViewModelManager _viewModelManager;
        private readonly IAudioImportService _audioImportService;
        private readonly IMessageDialog _messageDialog;
        private readonly IServiceProvider _serviceProvider;
        private readonly List<GameTitleEntryViewModel> _recentGameTitles;
        private readonly List<ComboItem> _recordTypes;
        private readonly List<ComboItem> _specialCategories;
        private readonly ReadOnlyObservableCollection<SeriesEntryViewModel> _series;
        private readonly ReadOnlyObservableCollection<GameTitleEntryViewModel> _games;
        private readonly ReadOnlyObservableCollection<string> _assignedInfoIds;
        private readonly Subject<Window> _whenNewRequestToAddGameEntry;
        private bool _isUpdatingSpecialRule = false;
        private bool _isSaving;
        private string _originalGameTitleId;
        private string _originalFilename;
        private string _pendingTargetAudioFile;
        private string _pendingStagedAudioFile;

        public IEnumerable<GameTitleEntryViewModel> RecentGameTitles { get { return _recentGameTitles; } }
        [Reactive]
        public bool DisplayRecents { get; set; }
        [Reactive]
        public GameTitleEntryViewModel SelectedRecentAction { get; set; }

        public IObservable<Window> WhenNewRequestToAddGameEntry { get { return _whenNewRequestToAddGameEntry; } }
        public GamePropertiesModalWindowViewModel VMGamePropertiesModal { get; set; }

        public BgmDbRootEntryViewModel DbRootViewModel { get; private set; }
        public BgmStreamSetEntryViewModel StreamSetViewModel { get; private set; }
        public BgmAssignedInfoEntryViewModel AssignedInfoViewModel { get; private set; }
        [Reactive]
        public BgmStreamPropertyEntryViewModel StreamPropertyViewModel { get; private set; }
        public BgmPropertyEntryViewModel BgmPropertyViewModel { get; private set; }

        public MSBTFieldViewModel MSBTTitleEditor { get; set; }
        public MSBTFieldViewModel MSBTAuthorEditor { get; set; }
        public MSBTFieldViewModel MSBTCopyrightEditor { get; set; }
        public IEnumerable<ComboItem> RecordTypes { get { return _recordTypes; } }
        public IEnumerable<ComboItem> SpecialCategories { get { return _specialCategories; } }
        [Reactive]
        public ComboItem SelectedRecordType { get; set; }
        [Reactive]
        public GameTitleEntryViewModel SelectedGameTitleViewModel { get; set; }
        [Reactive]
        public ComboItem SelectedSpecialCategory { get; set; }
        [Reactive]
        public bool IsSpecialCategoryPinch { get; set; }
        [Reactive]
        public bool IsInSoundTest { get; set; }

        [Reactive]
        public bool IsModSong { get; set; }

        public ReadOnlyObservableCollection<SeriesEntryViewModel> Series { get { return _series; } }
        public ReadOnlyObservableCollection<GameTitleEntryViewModel> Games { get { return _games; } }
        public ReadOnlyObservableCollection<string> AssignedInfoIds { get { return _assignedInfoIds; } }

        public ReactiveCommand<Window, Unit> ActionNewGame { get; }
        public ReactiveCommand<BgmPropertyEntryViewModel, Unit> ActionChangeFile { get; }
        public ReactiveCommand<BgmPropertyEntryViewModel, Unit> ActionCalculateLoopCues { get; }
        public ReactiveCommand<Window, Unit> ActionPreviewLoops { get; }
        public ReactiveCommand<Window, Unit> ActionNormalizeSong { get; }
        public ReactiveCommand<Window, Unit> ActionTrimAudio { get; }
        public ReactiveCommand<Window, Unit> ActionClosing { get; }
        public ReactiveCommand<Unit, Unit> ActionSetVolumeToAverage { get; }
        public ReactiveCommand<Unit, Unit> ActionSetVolumeToMedian { get; }

        public BgmPropertiesModalWindowViewModel(IOptionsMonitor<ApplicationSettings> config, ILogger<BgmPropertiesModalWindowViewModel> logger, IFileDialog fileDialog,
            IMapper mapper, IGUIStateManager guiStateManager, IViewModelManager viewModelManager, IAudioImportService audioImportService,
            IMessageDialog messageDialog, IServiceProvider serviceProvider)
        {
            _config = config;
            _logger = logger;
            _mapper = mapper;
            _guiStateManager = guiStateManager;
            _viewModelManager = viewModelManager;
            _audioImportService = audioImportService;
            _messageDialog = messageDialog;
            _serviceProvider = serviceProvider;
            _fileDialog = fileDialog;
            _recordTypes = GetRecordTypes();
            _specialCategories = GetSpecialCategories();
            _whenNewRequestToAddGameEntry = new Subject<Window>();
            _recentGameTitles = new List<GameTitleEntryViewModel>();

            //Bind observables
            viewModelManager.ObservableSeries.Connect()
               .ObserveOn(RxApp.MainThreadScheduler)
               .Bind(out _series)
               .DisposeMany()
               .Subscribe();
            viewModelManager.ObservableGameTitles.Connect()
               .ObserveOn(RxApp.MainThreadScheduler)
               .Bind(out _games)
               .DisposeMany()
               .Subscribe();
            viewModelManager.ObservableAssignedInfoEntries.Connect()
               .Transform(p => p.InfoId)
               .ObserveOn(RxApp.MainThreadScheduler)
               .Bind(out _assignedInfoIds)
               .DisposeMany()
               .Subscribe();

            //Set up MSBT Fields
            var defaultLocale = _config.CurrentValue.Sma5hMusicGUI.DefaultGUILocale;
            var defaultLocaleItem = new ComboItem(defaultLocale, Constants.GetLocaleDisplayName(defaultLocale));
            MSBTTitleEditor = new MSBTFieldViewModel()
            {
                SelectedLocale = defaultLocaleItem,
                EnableColorFormatting = true
            };
            MSBTAuthorEditor = new MSBTFieldViewModel()
            {
                SelectedLocale = defaultLocaleItem
            };
            MSBTCopyrightEditor = new MSBTFieldViewModel()
            {
                SelectedLocale = defaultLocaleItem,
                AcceptsReturn = true
            };

            //Set up subscriber on special category
            this.WhenAnyValue(p => p.SelectedSpecialCategory).Subscribe(o => SetSpecialCategoryRules(o?.Id));
            this.WhenAnyValue(p => p.SelectedItem.StreamSetViewModel.SpecialCategory).Subscribe(o => SetSpecialCategoryRules(o));
            this.WhenAnyValue(p => p.SelectedGameTitleViewModel).Subscribe((o) => SetGameTitleId(o));
            this.WhenAnyValue(p => p.SelectedRecentAction).Subscribe(o => HandleRecentAction(o));

            //Validation
            this.ValidationRule(p => p.SelectedGameTitleViewModel,
                p => p != null && !string.IsNullOrWhiteSpace(p.UiGameTitleId),
                "Please select a game.");
            this.ValidationRule(p => p.StreamPropertyViewModel.StartPoint0,
                p => ValidateStreamPropertyTime(p), "This value must be of the format '00:00:00.000'");
            this.ValidationRule(p => p.StreamPropertyViewModel.StartPoint1,
                p => ValidateStreamPropertyTime(p), "This value must be of the format '00:00:00.000'");
            this.ValidationRule(p => p.StreamPropertyViewModel.StartPoint2,
                p => ValidateStreamPropertyTime(p), "This value must be of the format '00:00:00.000'");
            this.ValidationRule(p => p.StreamPropertyViewModel.StartPoint3,
                p => ValidateStreamPropertyTime(p), "This value must be of the format '00:00:00.000'");
            this.ValidationRule(p => p.StreamPropertyViewModel.StartPoint4,
                p => ValidateStreamPropertyTime(p), "This value must be of the format '00:00:00.000'");
            this.ValidationRule(p => p.StreamPropertyViewModel.EndPoint,
                p => ValidateStreamPropertyTime(p), "This value must be of the format '00:00:00.000'");
            this.ValidationRule(p => p.StreamPropertyViewModel.StartPointSuddenDeath,
                p => ValidateStreamPropertyTime(p), "This value must be of the format '00:00:00.000'");
            this.ValidationRule(p => p.StreamPropertyViewModel.StartPointTransition,
                p => ValidateStreamPropertyTime(p), "This value must be of the format '00:00:00.000'");

            ActionNewGame = ReactiveCommand.Create<Window>(AddNewGame);
            ActionChangeFile = ReactiveCommand.CreateFromTask<BgmPropertyEntryViewModel>(ChangeFile);
            ActionCalculateLoopCues = ReactiveCommand.CreateFromTask<BgmPropertyEntryViewModel>(p => CalculateAudioCues(p, _pendingStagedAudioFile));
            ActionPreviewLoops = ReactiveCommand.CreateFromTask<Window>(PreviewLoops);
            ActionNormalizeSong = ReactiveCommand.CreateFromTask<Window>(NormalizeSong);
            ActionTrimAudio = ReactiveCommand.CreateFromTask<Window>(TrimSong);
            ActionClosing = ReactiveCommand.CreateFromTask<Window>(ClosingWindow);
            ActionSetVolumeToAverage = ReactiveCommand.Create(SetVolumeToAverage);
            ActionSetVolumeToMedian = ReactiveCommand.Create(SetVolumeToMedian);
        }

        private void SetVolumeToAverage()
        {
            var volumes = GetCurrentMetadataSongVolumes();
            if (volumes.Count == 0)
                return;

            BgmPropertyViewModel.AudioVolume = (float)Math.Round(volumes.Average(), 2);
        }

        private void SetVolumeToMedian()
        {
            var volumes = GetCurrentMetadataSongVolumes().OrderBy(p => p).ToList();
            if (volumes.Count == 0)
                return;

            var middle = volumes.Count / 2;
            var median = volumes.Count % 2 == 1
                ? volumes[middle]
                : (volumes[middle - 1] + volumes[middle]) / 2.0f;

            BgmPropertyViewModel.AudioVolume = (float)Math.Round(median, 2);
        }

        private List<float> GetCurrentMetadataSongVolumes()
        {
            return _viewModelManager.GetBgmPropertyEntriesViewModels()
                .Where(p => p.MusicMod?.Id == BgmPropertyViewModel.MusicMod?.Id)
                .Select(p => p.AudioVolume)
                .ToList();
        }

        private bool ValidateStreamPropertyTime(string value)
        {
            return string.IsNullOrEmpty(value) || Regex.IsMatch(value, @"^\d{2}:\d{2}:\d{2}.\d{3}$", RegexOptions.Compiled);
        }

        private void HandleRecentAction(GameTitleEntryViewModel o)
        {
            if (o == null)
                return;

            SelectedGameTitleViewModel = _games.FirstOrDefault(p => p.UiGameTitleId == o.UiGameTitleId);

            Dispatcher.UIThread.InvokeAsync(() =>
            {
                SelectedRecentAction = null;
            }, DispatcherPriority.Background);
        }

        private void AddRecentGameTitle(GameTitleEntryViewModel gameTitle)
        {
            if (gameTitle == null || string.IsNullOrEmpty(gameTitle.UiGameTitleId))
                return;

            _recentGameTitles.RemoveAll(p => p.UiGameTitleId == gameTitle.UiGameTitleId);

            if (_recentGameTitles.Count > 9)
                _recentGameTitles.RemoveAt(_recentGameTitles.Count - 1);

            _recentGameTitles.Insert(0, gameTitle);
            DisplayRecents = _recentGameTitles.Count > 0;
            this.RaisePropertyChanged(nameof(RecentGameTitles));
        }

        private void AddNewGame(Window window)
        {
            _logger.LogDebug("Clicked Add New Game");
            _whenNewRequestToAddGameEntry.OnNext(window);
        }

        //new ChangeFile stages the new audio file, should not cause vgmstream errors
        private async Task ChangeFile(BgmPropertyEntryViewModel bgmPropertyEntryViewModel)
        {
            _logger.LogDebug("Clicked Change File");
            var filename = await _fileDialog.OpenFileDialogAudioSingle();
            if (string.IsNullOrEmpty(filename))
                return;

            if (BgmPropertyViewModel.MusicPlayer != null)
                await BgmPropertyViewModel.MusicPlayer.StopSong();

            var tempPath = Path.Combine(_config.CurrentValue.TempPath, "AudioImport");
            Directory.CreateDirectory(tempPath);

            var stagedFilename = Path.Combine(tempPath, $"{Guid.NewGuid():N}{Path.GetExtension(filename)}");
            File.Copy(filename, stagedFilename);

            if (!await CalculateAudioCues(bgmPropertyEntryViewModel, stagedFilename))
            {
                DeleteAudioFileIfExists(stagedFilename);
                return;
            }

            var targetFilename = Path.ChangeExtension(
                _pendingTargetAudioFile ?? BgmPropertyViewModel.Filename,
                Path.GetExtension(filename));

            DiscardPendingAudioChanges();
            AdoptStagedAudio(targetFilename, stagedFilename);
            await BgmPropertyViewModel.MusicPlayer?.ChangeFilename(stagedFilename);
        }

        private async Task PreviewLoops(Window parentWindow)
        {
            if (BgmPropertyViewModel == null)
                return;

            string sourceFilename = null;
            string previewFilename = null;
            ToneIdCreationModalWindowModel vmToneIdCreation = null;

            try
            {
                _logger.LogDebug("Clicked Preview Loops");

                sourceFilename = GetCurrentAudioFilename();
                if (string.IsNullOrWhiteSpace(sourceFilename) || !File.Exists(sourceFilename))
                {
                    await _messageDialog.ShowError("Preview Loops", "The song file could not be found.");
                    return;
                }

                previewFilename = await _audioImportService.ExtractAudioToTempWav(sourceFilename);

                var audioInfo = await _audioImportService.GetAudioInfo(previewFilename);
                vmToneIdCreation = ActivatorUtilities.CreateInstance<ToneIdCreationModalWindowModel>(_serviceProvider);
                vmToneIdCreation.LoadQueueStatus(0);
                vmToneIdCreation.LoadLoopPreviewOnlyInfo(
                    previewFilename,
                    audioInfo.SampleRate,
                    audioInfo.TotalSamples,
                    BgmPropertyViewModel.LoopStartSample,
                    BgmPropertyViewModel.LoopEndSample
                );

                await vmToneIdCreation.PrepareForOpen();

                var modalToneIdCreation = new ToneIdCreationModalWindow() { DataContext = vmToneIdCreation };
                var result = await modalToneIdCreation.ShowDialog<ToneIdCreationModalWindow>(parentWindow);

                if (result == null)
                    return;

                var newLoopStartSample = vmToneIdCreation.LoopStartSample;
                var newLoopEndSample = vmToneIdCreation.LoopEndSample;

                if (BgmPropertyViewModel.MusicPlayer != null)
                    await BgmPropertyViewModel.MusicPlayer.StopSong();

                var previousFilename = GetCurrentAudioFilename();
                var updatedFile = await UpdateNus3AudioLoopPointsWithProgress(
                    parentWindow,
                    BgmPropertyViewModel.NameId,
                    StageCurrentAudioFile(previousFilename),
                    newLoopStartSample,
                    newLoopEndSample
                );

                if (!string.Equals(updatedFile, _pendingStagedAudioFile, StringComparison.OrdinalIgnoreCase))
                    AdoptStagedAudio(GetNus3AudioTargetFilename(), updatedFile);

                await CalculateAudioCues(BgmPropertyViewModel, updatedFile);

                if (BgmPropertyViewModel.MusicPlayer != null)
                    await BgmPropertyViewModel.MusicPlayer.ChangeFilename(updatedFile);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error while previewing and choosing loop points.");
                await _messageDialog.ShowError("Preview Loops", e.Message, e);
            }
            finally
            {
                vmToneIdCreation?.Dispose();
                DeleteTemporaryPreviewFile(sourceFilename, previewFilename);
            }
        }

        private async Task<string> UpdateNus3AudioLoopPointsWithProgress(
            Window parentWindow,
            string toneId,
            string filename,
            uint loopStartSample,
            uint loopEndSample)
        {
            var progressVm = new AudioConversionProgressModalWindowViewModel();
            progressVm.SetUpdatingLoops(Path.GetFileName(BgmPropertyViewModel.Filename));

            var progressWindow = new AudioConversionProgressModalWindow
            {
                DataContext = progressVm
            };

            var closingProgrammatically = false;
            progressWindow.Closing += (sender, args) =>
            {
                if (!closingProgrammatically)
                    args.Cancel = true;
            };

            var progressDialogTask = progressWindow.ShowDialog(parentWindow);

            try
            {
                var updatedFile = await _audioImportService.UpdateExistingNus3AudioLoopPoints(toneId, filename, loopStartSample, loopEndSample);
                progressVm.SetComplete();
                return updatedFile;
            }
            finally
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    closingProgrammatically = true;

                    if (progressWindow.IsVisible)
                        progressWindow.Close();
                });

                await progressDialogTask;
            }
        }

        private void DeleteTemporaryPreviewFile(string originalFilename, string previewFilename)
        {
            if (string.IsNullOrWhiteSpace(previewFilename) || string.Equals(originalFilename, previewFilename, StringComparison.OrdinalIgnoreCase))
                return;

            try
            {
                if (File.Exists(previewFilename))
                    File.Delete(previewFilename);
            }
            catch (Exception e)
            {
                _logger.LogWarning(e, "Could not delete temporary loop preview source file {Filename}.", previewFilename);
            }
        }

        private async Task TrimSong(Window parentWindow)
        {
            if (BgmPropertyViewModel == null)
                return;

            string preparedWav = null;
            string trimmedWav = null;
            string stagedNus3Audio = null;

            try
            {
                _logger.LogDebug("Clicked Trim Audio");

                var sourceFilename = GetCurrentAudioFilename();
                if (string.IsNullOrWhiteSpace(sourceFilename) || !File.Exists(sourceFilename))
                {
                    await _messageDialog.ShowError("Trim Audio", "The song file could not be found.");
                    return;
                }

                if (!_audioImportService.IsFfmpegConfigured())
                {
                    await _messageDialog.ShowInformation(
                        "Trim Audio unavailable",
                        "ffmpeg is not configured. Set its path in Global Settings before trimming audio.");
                    return;
                }

                if (BgmPropertyViewModel.MusicPlayer != null)
                    await BgmPropertyViewModel.MusicPlayer.StopSong();

                preparedWav = await _audioImportService.ExtractAudioToTempWav(sourceFilename);

                var audioInfo = await _audioImportService.GetAudioInfo(preparedWav);
                var waveformPeaks = await _audioImportService.GetAudioWaveformPeaks(preparedWav, 1600);
                var hasValidLoopPoints =
                    BgmPropertyViewModel.LoopEndSample > 0 &&
                    BgmPropertyViewModel.LoopEndSample <= audioInfo.TotalSamples &&
                    BgmPropertyViewModel.LoopStartSample <= BgmPropertyViewModel.LoopEndSample;

                var trimViewModel = new AudioTrimModalWindowViewModel(
                    _audioImportService,
                    _messageDialog,
                    _serviceProvider.GetRequiredService<IVGMMusicPlayer>(),
                    BgmPropertyViewModel.Filename,
                    preparedWav,
                    audioInfo.SampleRate,
                    audioInfo.TotalSamples,
                    waveformPeaks,
                    hasValidLoopPoints ? BgmPropertyViewModel.LoopStartSample : null,
                    hasValidLoopPoints ? BgmPropertyViewModel.LoopEndSample : null);
                var trimWindow = new AudioTrimModalWindow { DataContext = trimViewModel };

                // The trim modal owns and cleans up the prepared WAV after this point.
                preparedWav = null;
                trimmedWav = await trimWindow.ShowDialog<string>(parentWindow);
                if (string.IsNullOrWhiteSpace(trimmedWav))
                    return;

                uint? newLoopStartSample = null;
                uint? newLoopEndSample = null;
                if (hasValidLoopPoints)
                {
                    var retainedStartSample = trimViewModel.TrimStartSample;
                    var retainedEndSample = trimViewModel.TrimEndSample;
                    var adjustedSourceLoopStart = Math.Max(BgmPropertyViewModel.LoopStartSample, retainedStartSample);
                    var adjustedSourceLoopEnd = Math.Min(BgmPropertyViewModel.LoopEndSample, retainedEndSample);
                    var startWasClipped = retainedStartSample > BgmPropertyViewModel.LoopStartSample;
                    var endWasClipped = retainedEndSample < BgmPropertyViewModel.LoopEndSample;

                    //update loop points only if selection contains at least some of the original loop interval
                    if (adjustedSourceLoopStart < adjustedSourceLoopEnd)
                    {
                        newLoopStartSample = adjustedSourceLoopStart - retainedStartSample;
                        newLoopEndSample = adjustedSourceLoopEnd - retainedStartSample;
                    }

                    if (startWasClipped || endWasClipped)
                    {
                        string warning;
                        if (!newLoopStartSample.HasValue)
                        {
                            warning = "The trim selection removes the entire existing loop interval. The trimmed audio will be rebuilt without loop points.";
                        }
                        else if (startWasClipped && endWasClipped)
                        {
                            warning = "The trim selection cuts into both the start and end of the existing loop. Both loop points will be moved to the retained audio boundaries.";
                        }
                        else if (startWasClipped)
                        {
                            warning = "The trim selection cuts into the start of the existing loop. The loop start will be moved to the beginning of the retained audio.";
                        }
                        else
                        {
                            warning = "The trim selection cuts into the end of the existing loop. The loop end will be moved to the end of the retained audio.";
                        }

                        await _messageDialog.ShowWarning("Trim Audio - Loop points adjusted", warning);
                    }
                }

                var targetFilename = GetNus3AudioTargetFilename();
                stagedNus3Audio = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.nus3audio");
                await RebuildTrimmedSongWithProgress(
                    parentWindow,
                    trimmedWav,
                    stagedNus3Audio,
                    newLoopStartSample,
                    newLoopEndSample);

                //update the BGM property view model to point to the new nus3audio file
                AdoptStagedAudio(targetFilename, stagedNus3Audio);
                stagedNus3Audio = null;

                await CalculateAudioCues(BgmPropertyViewModel, _pendingStagedAudioFile);
                if (BgmPropertyViewModel.MusicPlayer != null)
                    await BgmPropertyViewModel.MusicPlayer.ChangeFilename(_pendingStagedAudioFile);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error while trimming song.");
                await _messageDialog.ShowError("Trim Audio", e.Message, e);
            }
            finally
            {
                DeleteAudioFileIfExists(preparedWav);
                DeleteAudioFileIfExists(trimmedWav);
                DeleteAudioFileIfExists(stagedNus3Audio);
            }
        }

        private async Task RebuildTrimmedSongWithProgress(
            Window parentWindow,
            string trimmedWav,
            string outputFilename,
            uint? loopStartSample,
            uint? loopEndSample)
        {
            var progressVm = new AudioConversionProgressModalWindowViewModel();
            progressVm.SetTrimming(Path.GetFileName(BgmPropertyViewModel.Filename));
            var progressWindow = new AudioConversionProgressModalWindow { DataContext = progressVm };
            var closingProgrammatically = false;
            progressWindow.Closing += (sender, args) =>
            {
                if (!closingProgrammatically)
                    args.Cancel = true;
            };

            var progressDialogTask = progressWindow.ShowDialog(parentWindow);
            try
            {
                await _audioImportService.CreateNus3AudioFromTrimmedWav(
                    BgmPropertyViewModel.NameId,
                    trimmedWav,
                    outputFilename,
                    loopStartSample,
                    loopEndSample);
                progressVm.SetComplete();
            }
            finally
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    closingProgrammatically = true;
                    if (progressWindow.IsVisible)
                        progressWindow.Close();
                });
                await progressDialogTask;
            }
        }

        private string GetCurrentAudioFilename()
        {
            return !string.IsNullOrWhiteSpace(_pendingStagedAudioFile) &&
                   File.Exists(_pendingStagedAudioFile)
                ? _pendingStagedAudioFile
                : BgmPropertyViewModel.Filename;
        }

        private string GetNus3AudioTargetFilename()
        {
            var currentTargetFilename = _pendingTargetAudioFile ?? BgmPropertyViewModel.Filename;
            if (_audioImportService.IsNus3Audio(currentTargetFilename))
                return currentTargetFilename;

            return Path.Combine(
                Path.GetDirectoryName(currentTargetFilename) ?? string.Empty,
                $"{BgmPropertyViewModel.NameId}.nus3audio");
        }

        private void AdoptStagedAudio(string targetFilename, string stagedFilename)
        {
            if (!string.IsNullOrWhiteSpace(_pendingStagedAudioFile) &&
                !string.Equals(_pendingStagedAudioFile, stagedFilename, StringComparison.OrdinalIgnoreCase))
            {
                DeleteAudioFileIfExists(_pendingStagedAudioFile);
            }

            _pendingTargetAudioFile = targetFilename;
            _pendingStagedAudioFile = stagedFilename;
            BgmPropertyViewModel.Filename = targetFilename;
        }

        private void DeleteAudioFileIfExists(string filename)
        {
            if (string.IsNullOrWhiteSpace(filename))
                return;

            try
            {
                if (File.Exists(filename))
                    File.Delete(filename);
            }
            catch (Exception e)
            {
                _logger.LogWarning(e, "Could not delete audio file {Filename}.", filename);
            }
        }

        private void DiscardPendingAudioChanges()
        {
            DeleteAudioFileIfExists(_pendingStagedAudioFile);

            _pendingTargetAudioFile = null;
            _pendingStagedAudioFile = null;
        }

        private async Task NormalizeSong(Window parentWindow)
        {
            if (BgmPropertyViewModel == null)
                return;

            try
            {
                _logger.LogDebug("Clicked Normalize Song");

                var sourceFilename = GetCurrentAudioFilename();
                if (string.IsNullOrWhiteSpace(sourceFilename) || !File.Exists(sourceFilename))
                {
                    await _messageDialog.ShowError("Normalize Song", "The song file could not be found.");
                    return;
                }

                var confirm = await _messageDialog.ShowWarningConfirm(
                    "Normalize Song",
                    $"This will normalize and overwrite '{Path.GetFileName(BgmPropertyViewModel.Filename)}'. Continue?"
                );

                if (!confirm)
                    return;

                if (BgmPropertyViewModel.MusicPlayer != null)
                    await BgmPropertyViewModel.MusicPlayer.StopSong();

                await NormalizeSongWithProgress(parentWindow);

                if (BgmPropertyViewModel.MusicPlayer != null)
                    await BgmPropertyViewModel.MusicPlayer.ChangeFilename(_pendingStagedAudioFile ?? BgmPropertyViewModel.Filename);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error while normalizing song.");
                await _messageDialog.ShowError("Normalize Song", e.Message, e);
            }
        }

        private async Task NormalizeSongWithProgress(Window parentWindow)
        {
            var progressVm = new AudioConversionProgressModalWindowViewModel();
            progressVm.SetNormalizing(Path.GetFileName(BgmPropertyViewModel.Filename));

            var progressWindow = new AudioConversionProgressModalWindow
            {
                DataContext = progressVm
            };

            var closingProgrammatically = false;
            progressWindow.Closing += (sender, args) =>
            {
                if (!closingProgrammatically)
                    args.Cancel = true;
            };

            var progressDialogTask = progressWindow.ShowDialog(parentWindow);

            try
            {
                var previousFilename = GetCurrentAudioFilename();
                var normalizedFile = await _audioImportService.NormalizeExistingNus3Audio(
                    BgmPropertyViewModel.NameId,
                    StageCurrentAudioFile(previousFilename));

                if (!string.Equals(normalizedFile, _pendingStagedAudioFile, StringComparison.OrdinalIgnoreCase))
                    AdoptStagedAudio(GetNus3AudioTargetFilename(), normalizedFile);

                await CalculateAudioCues(BgmPropertyViewModel, normalizedFile);
                progressVm.SetComplete();
            }
            finally
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    closingProgrammatically = true;

                    if (progressWindow.IsVisible)
                        progressWindow.Close();
                });

                await progressDialogTask;
            }
        }

        private string StageCurrentAudioFile(string filename)
        {
            //if already staged, return the staged file
            if (!string.IsNullOrWhiteSpace(_pendingStagedAudioFile) &&
                (string.Equals(filename, _pendingTargetAudioFile, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(filename, _pendingStagedAudioFile, StringComparison.OrdinalIgnoreCase)))
                return _pendingStagedAudioFile;

            //if filename changed, delete old staged file
            if (!string.IsNullOrWhiteSpace(_pendingStagedAudioFile))
            {
                File.Delete(_pendingStagedAudioFile);
            }

            //create new staged file
            var tempPath = Path.Combine(_config.CurrentValue.TempPath, "AudioImport");
            Directory.CreateDirectory(tempPath);

            _pendingTargetAudioFile = filename;
            _pendingStagedAudioFile = Path.Combine(tempPath, $"{Guid.NewGuid():N}{Path.GetExtension(filename)}");
            File.Copy(filename, _pendingStagedAudioFile);
            return _pendingStagedAudioFile;
        }

        private List<ComboItem> GetRecordTypes()
        {
            var recordTypes = new List<ComboItem>();
            recordTypes.AddRange(Constants.CONVERTER_RECORD_TYPE.Select(p => new ComboItem(p.Key, p.Value)));
            return recordTypes;
        }

        private List<ComboItem> GetSpecialCategories()
        {
            var recordTypes = new List<ComboItem>() { new ComboItem(string.Empty, "None/Other") };
            recordTypes.AddRange(Constants.SpecialCategories.UI_SPECIAL_CATEGORY.Select(p => new ComboItem(p.Key, p.Value)));
            return recordTypes;
        }

        private void SetSpecialCategoryRules(string specialRule)
        {
            if (!_isUpdatingSpecialRule)
            {
                _isUpdatingSpecialRule = true;
                IsSpecialCategoryPinch = false;

                if (_refSelectedItem != null)
                {
                    SelectedSpecialCategory = _specialCategories.FirstOrDefault(p => p.Id == specialRule);
                    if (SelectedSpecialCategory == null)
                        SelectedSpecialCategory = _specialCategories[0];
                    _refSelectedItem.StreamSetViewModel.SpecialCategory = specialRule;

                    switch (specialRule)
                    {
                        case Constants.SpecialCategories.SPECIAL_CATEGORY_PINCH_VALUE:
                            IsSpecialCategoryPinch = true;
                            break;
                    }
                }
                _isUpdatingSpecialRule = false;
            }
        }

        private void SetGameTitleId(GameTitleEntryViewModel gameTitle)
        {
            if (DbRootViewModel != null)
            {
                if (gameTitle != null)
                {
                    DbRootViewModel.UiGameTitleId = gameTitle.UiGameTitleId;
                }
                else
                    DbRootViewModel.UiGameTitleId = MusicConstants.InternalIds.GAME_TITLE_ID_DEFAULT;
            }
        }

        private async Task ClosingWindow(Window w)
        {
            if (BgmPropertyViewModel?.MusicPlayer != null)
                await BgmPropertyViewModel.MusicPlayer.ChangeFilename(_originalFilename);

            if (_isSaving)
                return;

            DiscardPendingAudioChanges();
        }

        protected override async Task<bool> SaveChanges()
        {
            _logger.LogDebug("Save Changes");

            if (BgmPropertyViewModel?.MusicPlayer != null)
                await BgmPropertyViewModel.MusicPlayer.StopSong();

            //replace the original audio with the staged audio if it was changed
            if (!string.IsNullOrWhiteSpace(_pendingStagedAudioFile))
            {
                if (string.Equals(BgmPropertyViewModel.Filename, _pendingTargetAudioFile, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(BgmPropertyViewModel.Filename, _pendingStagedAudioFile, StringComparison.OrdinalIgnoreCase))
                {
                    File.Copy(_pendingStagedAudioFile, _pendingTargetAudioFile, true);
                    BgmPropertyViewModel.Filename = _pendingTargetAudioFile;
                }
                File.Delete(_pendingStagedAudioFile);
                _pendingTargetAudioFile = null;
                _pendingStagedAudioFile = null;
            }
            _isSaving = true;
            if (BgmPropertyViewModel.AudioVolume < Constants.MinimumGameVolume)
                BgmPropertyViewModel.AudioVolume = Constants.MinimumGameVolume;
            if (BgmPropertyViewModel.AudioVolume > Constants.MaximumGameVolume)
                BgmPropertyViewModel.AudioVolume = Constants.MaximumGameVolume;
            BgmPropertyViewModel.AudioVolume = (float)Math.Round(BgmPropertyViewModel.AudioVolume, 2, MidpointRounding.AwayFromZero);

            _originalFilename = BgmPropertyViewModel.Filename;

            DbRootViewModel.TestDispOrder = (short)(IsInSoundTest ? DbRootViewModel.TestDispOrder > -1 ? DbRootViewModel.TestDispOrder : _guiStateManager.GetNewHighestSoundTestOrderValue() : -1);
            if (SelectedRecordType != null)
                DbRootViewModel.RecordType = SelectedRecordType.Id;
            MSBTTitleEditor.SaveValueToRecent();
            MSBTAuthorEditor.SaveValueToRecent();
            MSBTCopyrightEditor.SaveValueToRecent();
            DbRootViewModel.MSBTTitle = SaveMSBTValues(MSBTTitleEditor.MSBTValues);
            DbRootViewModel.MSBTAuthor = SaveMSBTValues(MSBTAuthorEditor.MSBTValues);
            DbRootViewModel.MSBTCopyright = SaveMSBTValues(MSBTCopyrightEditor.MSBTValues);

            if (!string.IsNullOrEmpty(SelectedGameTitleViewModel?.UiGameTitleId) && _originalGameTitleId != SelectedGameTitleViewModel.UiGameTitleId)
            {
                AddRecentGameTitle(SelectedGameTitleViewModel);
            }

            return true;
        }

        private Dictionary<string, string> SaveMSBTValues(Dictionary<string, string> msbtValues)
        {
            var output = new Dictionary<string, string>();
            var copyToEmptyLocales = _config.CurrentValue.Sma5hMusicGUI.CopyToEmptyLocales;
            var defaultMSBTLocale = _config.CurrentValue.Sma5hMusicGUI.DefaultMSBTLocale;
            if (msbtValues != null)
            {
                foreach (var msbtValue in msbtValues)
                {
                    if (!string.IsNullOrEmpty(msbtValue.Value))
                        output.Add(msbtValue.Key, msbtValue.Value);
                    else if (copyToEmptyLocales && msbtValues.ContainsKey(defaultMSBTLocale))
                        output.Add(msbtValue.Key, msbtValues[defaultMSBTLocale]);
                }
            }
            return output;
        }

        protected override void LoadItem(BgmEntryViewModel item)
        {
            _logger.LogDebug("Load Item");
            DbRootViewModel = item?.DbRootViewModel;
            StreamSetViewModel = item?.StreamSetViewModel;
            AssignedInfoViewModel = item?.AssignedInfoViewModel;
            StreamPropertyViewModel = item?.StreamPropertyViewModel;
            BgmPropertyViewModel = item?.BgmPropertyViewModel;
            _originalFilename = BgmPropertyViewModel?.Filename;
            _isSaving = false;
            _pendingTargetAudioFile = null;
            _pendingStagedAudioFile = null;

            IsModSong = item.MusicMod != null;

            MSBTTitleEditor.ResetTextColorSelection();
            MSBTAuthorEditor.ResetTextColorSelection();
            MSBTCopyrightEditor.ResetTextColorSelection();
            MSBTTitleEditor.MSBTValues = DbRootViewModel.MSBTTitle;
            MSBTAuthorEditor.MSBTValues = DbRootViewModel.MSBTAuthor;
            MSBTCopyrightEditor.MSBTValues = DbRootViewModel.MSBTCopyright;
            IsInSoundTest = DbRootViewModel.TestDispOrder > -1;
            SelectedRecordType = _recordTypes.FirstOrDefault(p => p.Id == DbRootViewModel.RecordType);
            SelectedGameTitleViewModel = _games.FirstOrDefault(p => p.UiGameTitleId == DbRootViewModel.UiGameTitleId);
            _originalGameTitleId = SelectedGameTitleViewModel?.UiGameTitleId;
            SetSpecialCategoryRules(StreamSetViewModel.SpecialCategory);
        }

        private async Task<bool> CalculateAudioCues(BgmPropertyEntryViewModel bgmPropertyEntryViewModel, string filename = null)
        {
            var audioCuePoints = await _guiStateManager.UpdateAudioCuePoints(filename ?? bgmPropertyEntryViewModel.Filename);
            if (audioCuePoints != null)
            {
                _mapper.Map(audioCuePoints, bgmPropertyEntryViewModel);
                return true;
            }
            return false;
        }

    }
}
