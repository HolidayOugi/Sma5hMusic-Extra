using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using ReactiveUI.Validation.Extensions;
using Sma5h.Mods.Music.Helpers;
using Sma5hMusic.GUI.Helpers;
using Sma5hMusic.GUI.Interfaces;
using Sma5hMusic.GUI.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;

namespace Sma5hMusic.GUI.ViewModels
{
    public class GlobalSettingsModalWindowViewModel : ModalBaseViewModel<GlobalConfigurationViewModel>
    {
        private readonly IGUIStateManager _guiStateManager;
        private readonly IFileDialog _fileDialog;
        private PlaylistGenerationItem _selectedPlaylistGenerationItem;
        private string _originalUIScale;
        private string _originalUITheme;
        private string _originalDefaultGUILocale;
        private bool _originalInGameVolume;
        private string _originalModPath;
        private string _originalModOverridePath;
        private string _originalGameResourcesPath;
        private string _originalResourcesPath;
        private string _originalToolsPath;

        public List<string> UIThemes => new List<string>() { "Dark", "Light" }; //, "WindowsDark", "WindowsLight" };
        public List<string> UIScales => new List<string>() { "Normal", "Small" };
        public List<string> ConversionFormats => new List<string>() { "lopus", "idsp" };
        public List<string> FallBackConversionFormats => new List<string>() { "lopus", "idsp" };
        public List<ComboItem> Locales => Constants.CONVERTER_LOCALE.Select(p => new ComboItem(p.Key, p.Value)).ToList();
        public List<ComboItem> RecordTypes => Constants.CONVERTER_RECORD_TYPE.Select(p => new ComboItem(p.Key, p.Value)).ToList();
        public List<PlaylistGenerationItem> PlaylistGenerationModes => new List<PlaylistGenerationItem>()
        {
            new PlaylistGenerationItem(PlaylistGeneration.Manual, "Manual"),
            new PlaylistGenerationItem(PlaylistGeneration.OnlyMissingSongs, "Add Missing Mod Songs"),
            new PlaylistGenerationItem(PlaylistGeneration.AllSongs, "Add All Songs"),
        };

        public ReactiveCommand<Unit, Unit> ActionWipeAudioCache { get; }
        public ReactiveCommand<string, Unit> ActionOpenFileDialog { get; }

        public ComboItem SelectedGUILocale { get; set; }
        public ComboItem SelectedMSBTLocale { get; set; }
        public ComboItem SelectedDefaultRecordType { get; set; }
        public PlaylistGenerationItem SelectedPlaylistGenerationItem
        {
            get => _selectedPlaylistGenerationItem;
            set
            {
                _selectedPlaylistGenerationItem = value;
                this.RaisePropertyChanged(nameof(SelectedPlaylistGenerationItem));
                //this.RaiseAndSetIfChanged(ref _selectedPlaylistGenerationItem, value);
                if (SelectedItem != null)
                {
                    SelectedItem.PlaylistGenerationMode = (PlaylistGeneration)value.Id;
                    SetPlaylistGenerationItemDescription(SelectedItem);
                }
            }
        }
        [Reactive]
        public bool IsPlaylistGenerationModeManual { get; set; }
        [Reactive]
        public bool IsPlaylistGenerationModeOnlyMissingSongs { get; set; }
        [Reactive]
        public bool IsPlaylistGenerationModeAllSongs { get; set; }

        public GlobalSettingsModalWindowViewModel(IGUIStateManager guiStateManager, IFileDialog fileDialog)
        {
            _guiStateManager = guiStateManager;
            _fileDialog = fileDialog;

            ActionWipeAudioCache = ReactiveCommand.CreateFromTask(OnWipeAudioCache);
            ActionOpenFileDialog = ReactiveCommand.CreateFromTask<string>(OnChoosePath);

            this.ValidationRule(p => p.SelectedItem.OutputPath,
                p => !string.IsNullOrWhiteSpace(p),
                "Output directory is required.");

            this.ValidationRule(p => p.SelectedItem.GameResourcesPath,
                p => !string.IsNullOrEmpty(p) && Directory.Exists(p),
                "This directory does not exist.");

            //this.ValidationRule(p => p.SelectedItem.CachePath,
            //    p => !string.IsNullOrEmpty(p) && Directory.Exists(p),
            //    "This directory does not exist.");

            //this.ValidationRule(p => p.SelectedItem.LogPath,
            //    p => !string.IsNullOrEmpty(p) && Directory.Exists(p),
            //    "This directory does not exist.");

            this.ValidationRule(p => p.SelectedItem.ModOverridePath,
                p => !string.IsNullOrEmpty(p) && Directory.Exists(p),
                "This directory does not exist.");

            this.ValidationRule(p => p.SelectedItem.ModPath,
                p => !string.IsNullOrEmpty(p) && Directory.Exists(p),
                "This directory does not exist.");

            this.ValidationRule(p => p.SelectedItem.ResourcesPath,
                p => !string.IsNullOrEmpty(p) && Directory.Exists(p),
                "This directory does not exist.");

            this.ValidationRule(p => p.SelectedItem.ToolsPath,
                p => !string.IsNullOrEmpty(p) && Directory.Exists(p),
                "This directory does not exist.");

            this.ValidationRule(p => p.SelectedItem.YtDlpPath,
                p => string.IsNullOrWhiteSpace(p) || File.Exists(p),
                "This file does not exist.");

            this.ValidationRule(p => p.SelectedItem.FfmpegPath,
                p => string.IsNullOrWhiteSpace(p) || File.Exists(p),
                "This file does not exist.");
        }

        public async Task OnWipeAudioCache()
        {
            await _guiStateManager.WipeAudioCache();
        }

        public async Task OnChoosePath(string param)
        {
            var result = param switch
            {
                "YtDlpPath" => await _fileDialog.OpenFileDialogYtDlp(),
                "FfmpegPath" => await _fileDialog.OpenFileDialogFfmpeg(),
                _ => await _fileDialog.OpenFolderDialog()
            };
            if (!string.IsNullOrEmpty(result))
            {
                if (param == "YtDlpPath" || param == "FfmpegPath")
                    EnsureExecutableOnLinux(result);

                switch (param)
                {
                    case "OutputPath":
                        SelectedItem.OutputPath = result;
                        break;
                    case "ModPath":
                        SelectedItem.ModPath = result;
                        break;
                    case "ModOverridePath":
                        SelectedItem.ModOverridePath = result;
                        break;
                    case "GameResourcesPath":
                        SelectedItem.GameResourcesPath = result;
                        break;
                    case "ResourcesPath":
                        SelectedItem.ResourcesPath = result;
                        break;
                    case "ToolsPath":
                        SelectedItem.ToolsPath = result;
                        break;
                    case "YtDlpPath":
                        SelectedItem.YtDlpPath = result;
                        break;
                    case "FfmpegPath":
                        SelectedItem.FfmpegPath = result;
                        break;
                    case "CachePath":
                        SelectedItem.CachePath = result;
                        break;
                    case "TempPath":
                        SelectedItem.TempPath = result;
                        break;
                    case "LogPath":
                        SelectedItem.LogPath = result;
                        break;
                }
            }
        }

        private static void EnsureExecutableOnLinux(string path)
        {
            if (!OperatingSystem.IsLinux())
                return;

            try
            {
                //set bit for executable on
                var mode = File.GetUnixFileMode(path);
                File.SetUnixFileMode(path, mode | UnixFileMode.UserExecute);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        protected override void LoadItem(GlobalConfigurationViewModel item)
        {
            _originalUIScale = item?.UIScale;
            _originalUITheme = item?.UITheme;
            _originalDefaultGUILocale = item?.DefaultGUILocale;
            _originalInGameVolume = item?.InGameVolume ?? false;
            _originalModPath = item?.ModPath;
            _originalModOverridePath = item?.ModOverridePath;
            _originalGameResourcesPath = item?.GameResourcesPath;
            _originalResourcesPath = item?.ResourcesPath;
            _originalToolsPath = item?.ToolsPath;

            SelectedGUILocale = Locales.FirstOrDefault(p => p.Id == item?.DefaultGUILocale);
            SelectedMSBTLocale = Locales.FirstOrDefault(p => p.Id == item?.DefaultMSBTLocale);
            SelectedDefaultRecordType = RecordTypes.FirstOrDefault(p => p.Id == item?.DefaultRecordType)
                ?? RecordTypes.First(p => p.Id == MusicConstants.InternalIds.RECORD_TYPE_DEFAULT);
            SelectedPlaylistGenerationItem = PlaylistGenerationModes.FirstOrDefault(p => p.Id == (int?)item?.PlaylistGenerationMode);

            if (item != null && item.AudioNormalizationTargetLufs <= 0)
                item.AudioNormalizationTargetLufs = 14;

            if (item != null && (item.LoopPreviewSeconds < 2 || item.LoopPreviewSeconds > 10))
                item.LoopPreviewSeconds = 6;

            if (item != null && (item.StartingOrderForSeries < 0 || item.StartingOrderForSeries > 39))
                item.StartingOrderForSeries = 1;
        }

        protected override async Task<bool> SaveChanges()
        {
            SelectedItem.DefaultGUILocale = SelectedGUILocale?.Id;
            SelectedItem.DefaultMSBTLocale = SelectedMSBTLocale?.Id;
            SelectedItem.DefaultRecordType = SelectedDefaultRecordType?.Id ?? MusicConstants.InternalIds.RECORD_TYPE_DEFAULT;
            var requiresRestart = HasRestartRequiredChanges();
            SelectedItem.SaveChanges();

            return await _guiStateManager.UpdateGlobalSettings(SelectedItem.GetReference(), requiresRestart);
        }


        private bool HasRestartRequiredChanges()
        {
            return !string.Equals(_originalUIScale, SelectedItem.UIScale, StringComparison.Ordinal)
                || !string.Equals(_originalUITheme, SelectedItem.UITheme, StringComparison.Ordinal)
                || !string.Equals(_originalDefaultGUILocale, SelectedItem.DefaultGUILocale, StringComparison.Ordinal)
                || _originalInGameVolume != SelectedItem.InGameVolume
                || !PathsAreEqual(_originalModPath, SelectedItem.ModPath)
                || !PathsAreEqual(_originalModOverridePath, SelectedItem.ModOverridePath)
                || !PathsAreEqual(_originalGameResourcesPath, SelectedItem.GameResourcesPath)
                || !PathsAreEqual(_originalResourcesPath, SelectedItem.ResourcesPath)
                || !PathsAreEqual(_originalToolsPath, SelectedItem.ToolsPath);
        }

        private static bool PathsAreEqual(string left, string right)
        {
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }
        private void SetPlaylistGenerationItemDescription(GlobalConfigurationViewModel item)
        {
            if (item != null)
            {
                IsPlaylistGenerationModeManual = item.PlaylistGenerationMode == PlaylistGeneration.Manual;
                IsPlaylistGenerationModeOnlyMissingSongs = item.PlaylistGenerationMode == PlaylistGeneration.OnlyMissingSongs;
                IsPlaylistGenerationModeAllSongs = item.PlaylistGenerationMode == PlaylistGeneration.AllSongs;
            }
            else
            {
                IsPlaylistGenerationModeManual = IsPlaylistGenerationModeOnlyMissingSongs = IsPlaylistGenerationModeAllSongs = false;
            }
        }
    }
}
