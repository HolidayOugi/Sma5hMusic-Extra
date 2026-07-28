using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using ReactiveUI;
using Sma5h.Mods.Music.Helpers;
using Sma5h.Mods.Music.Interfaces;
using Sma5hMusic.GUI.Views;
using System;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;

namespace Sma5hMusic.GUI.ViewModels
{
    public partial class MainWindowViewModel
    {
        private readonly ICskPackBuildService _cskPackBuildService;

        public ReactiveCommand<Unit, Unit> ActionBuildCskPacks { get; }
        public ReactiveCommand<Unit, Unit> ActionBuildSingleCskPack { get; }
        public ReactiveCommand<Unit, Unit> ActionBuildCskMetadataOnly { get; }

        public async Task OnBuildCskPacks()
        {
            await BuildCskPacks(false);
        }

        public async Task OnBuildSingleCskPack()
        {
            var buildStarted = false;
            try
            {
                var currentLocale = _viewModelManager.CurrentLocale;
                var availableSeries = await _cskPackBuildService.GetAvailableSeries(currentLocale);
                if (availableSeries.Count == 0 && !HasCskOverride())
                {
                    await _messageDialog.ShowError("CSK pack build failed", "No changes were found.");
                    return;
                }

                if (!await _buildDialog.EnsureArcOutputIsClean())
                    return;

                IsLoading = true;
                IsShowingDebug = true;
                buildStarted = true;
                await _musicPlayer.Stop();
                _logger.LogInformation("Building single CSK pack for all {SeriesCount} available series.", availableSeries.Count);

                await _cskPackBuildService.BuildSingle(availableSeries.Select(p => p.Key), currentLocale);
                await _messageDialog.ShowInformation("Complete", "Single CSK pack build complete.");
            }
            catch (Exception e)
            {
                await _messageDialog.ShowError("CSK pack build failed", e.Message, e);
            }
            finally
            {
                if (buildStarted)
                {
                    IsLoading = false;
                    IsShowingDebug = false;
                }
            }
        }

        public async Task OnBuildCskMetadataOnly()
        {
            var buildStarted = false;
            try
            {
                var currentLocale = _viewModelManager.CurrentLocale;
                var availableSeries = await _cskPackBuildService.GetAvailableSeries(currentLocale);
                if (availableSeries.Count == 0)
                {
                    await _messageDialog.ShowError("CSK metadata build failed", "No series were found in the currently loaded music mods.");
                    return;
                }

                if (!await _buildDialog.EnsureArcOutputIsClean())
                    return;

                IsLoading = true;
                IsShowingDebug = true;
                buildStarted = true;
                await _musicPlayer.Stop();
                _logger.LogInformation("Building CSK metadata-only packs for all {SeriesCount} available series.", availableSeries.Count);

                await _cskPackBuildService.BuildMetadataOnly(currentLocale);
                await _messageDialog.ShowInformation("Complete", "CSK metadata-only build complete.");
            }
            catch (Exception e)
            {
                await _messageDialog.ShowError("CSK metadata build failed", e.Message, e);
            }
            finally
            {
                if (buildStarted)
                {
                    IsLoading = false;
                    IsShowingDebug = false;
                }
            }
        }

        private async Task BuildCskPacks(bool singlePack)
        {
            var buildStarted = false;
            try
            {
                var currentLocale = _viewModelManager.CurrentLocale;
                var availableSeries = await _cskPackBuildService.GetAvailableSeries(currentLocale);
                if (availableSeries.Count == 0)
                {
                    if (!HasCskOverride())
                    {
                        await _messageDialog.ShowError("CSK pack build failed", "No changes were found.");
                        return;
                    }

                    if (!await _buildDialog.EnsureArcOutputIsClean())
                        return;

                    IsLoading = true;
                    IsShowingDebug = true;
                    buildStarted = true;
                    await _musicPlayer.Stop();
                    _logger.LogInformation("Building CSK vanilla song changes pack.");

                    if (singlePack)
                        await _cskPackBuildService.BuildSingle(Enumerable.Empty<string>(), currentLocale);
                    else
                        await _cskPackBuildService.Build(Enumerable.Empty<string>(), currentLocale);

                    await _messageDialog.ShowInformation("Complete", singlePack ? "Single CSK pack build complete." : "Modular CSK packs build complete.");
                    return;
                }

                var pickerViewModel = new CskPackSeriesPickerModalWindowViewModel(availableSeries);
                var pickerWindow = new CskPackSeriesPickerModalWindow { DataContext = pickerViewModel };
                var pickerResult = await pickerWindow.ShowDialog<CskPackSeriesPickerModalWindow>(_rootDialog.Window);
                if (pickerResult == null)
                    return;

                var selectedSeriesKeys = pickerViewModel.GetSelectedSeriesKeys().ToList();
                if (selectedSeriesKeys.Count == 0)
                    return;

                if (!await _buildDialog.EnsureArcOutputIsClean())
                    return;

                IsLoading = true;
                IsShowingDebug = true;
                buildStarted = true;
                await _musicPlayer.Stop();
                _logger.LogInformation("Building {CskBuildMode} CSK pack(s) for {SelectedSeriesCount} selected series.", singlePack ? "single" : "modular", selectedSeriesKeys.Count);

                if (singlePack)
                    await _cskPackBuildService.BuildSingle(selectedSeriesKeys, currentLocale);
                else
                    await _cskPackBuildService.Build(selectedSeriesKeys, currentLocale);

                await _messageDialog.ShowInformation("Complete", singlePack ? "Single CSK pack build complete." : "Modular CSK packs build complete.");
            }
            catch (Exception e)
            {
                await _messageDialog.ShowError("CSK pack build failed", e.Message, e);
            }
            finally
            {
                if (buildStarted)
                {
                    IsLoading = false;
                    IsShowingDebug = false;
                }
            }
        }

        private bool HasCskOverride()
        {
            return new[]
            {
                MusicConstants.MusicModFiles.MUSIC_OVERRIDE_CORE_BGM_JSON_FILE,
                MusicConstants.MusicModFiles.MUSIC_OVERRIDE_PLAYLIST_JSON_FILE
            }.Any(file =>
            {
                var path = Path.Combine(_appSettings.CurrentValue.Sma5hMusicOverride.ModPath, file);
                return File.Exists(path) && JObject.Parse(File.ReadAllText(path)).HasValues;
            });
        }
    }
}
