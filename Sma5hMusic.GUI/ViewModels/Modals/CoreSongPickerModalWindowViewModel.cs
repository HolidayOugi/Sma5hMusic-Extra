using Avalonia.Controls;
using ReactiveUI;
using Sma5hMusic.GUI.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Text.RegularExpressions;

namespace Sma5hMusic.GUI.ViewModels
{
    public class CoreSongPickerModalWindowViewModel : ViewModelBase
    {
        private CoreSongPickerSeriesOptionViewModel _selectedSeries;
        private CoreSongPickerOptionViewModel _selectedSong;

        public ObservableCollection<CoreSongPickerOptionViewModel> Songs { get; }
        public ObservableCollection<CoreSongPickerSeriesOptionViewModel> Series { get; }
        public double PickerMinWidth { get; }
        public ReactiveCommand<Window, Unit> ActionCancel { get; }
        public ReactiveCommand<Window, Unit> ActionOK { get; }

        public CoreSongPickerSeriesOptionViewModel SelectedSeries
        {
            get => _selectedSeries;
            set
            {
                this.RaiseAndSetIfChanged(ref _selectedSeries, value);
                SelectedSong = null;
                this.RaisePropertyChanged(nameof(FilteredSongs));
            }
        }

        public CoreSongPickerOptionViewModel SelectedSong
        {
            get => _selectedSong;
            set => this.RaiseAndSetIfChanged(ref _selectedSong, value);
        }

        public IEnumerable<CoreSongPickerOptionViewModel> FilteredSongs
        {
            get
            {
                if (SelectedSeries == null)
                    return Enumerable.Empty<CoreSongPickerOptionViewModel>();

                return Songs.Where(p => string.Equals(p.UiSeriesId, SelectedSeries.UiSeriesId, StringComparison.OrdinalIgnoreCase));
            }
        }

        public CoreSongPickerModalWindowViewModel(IEnumerable<CoreSongReplacementOption> songs)
        {
            Songs = new ObservableCollection<CoreSongPickerOptionViewModel>(
                songs
                    .OrderBy(p => p.TestDispOrder < 0)
                    .ThenBy(p => p.TestDispOrder)
                    .ThenBy(p => p.Title, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(p => p.UiBgmId, StringComparer.OrdinalIgnoreCase)
                    .Select(p => new CoreSongPickerOptionViewModel(p)));

            Series = new ObservableCollection<CoreSongPickerSeriesOptionViewModel>(
                Songs
                    .GroupBy(p => p.UiSeriesId)
                    .Select(p => new CoreSongPickerSeriesOptionViewModel(
                        p.Key,
                        p.FirstOrDefault()?.SeriesTitle ?? p.Key,
                        p.Count()))
                    .OrderBy(p => p.Title, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(p => p.UiSeriesId, StringComparer.OrdinalIgnoreCase));

            PickerMinWidth = CalculatePickerMinWidth(Songs);

            ActionCancel = ReactiveCommand.Create<Window>(Cancel);
            ActionOK = ReactiveCommand.Create<Window>(Save, this.WhenAnyValue(p => p.SelectedSong).Select(p => p != null));
            SelectedSeries = Series.FirstOrDefault();
        }

        private static double CalculatePickerMinWidth(IEnumerable<CoreSongPickerOptionViewModel> songs)
        {
            const double baseChromeWidth = 300;
            const double titleCharacterWidth = 6.8;
            const double toneIdCharacterWidth = 5.4;
            const double minimumWidth = 650;

            var maxTextWidth = songs
                .Select(p => (StripLabelTags(p.Title).Length * titleCharacterWidth) + (p.ToneId.Length * toneIdCharacterWidth))
                .DefaultIfEmpty(0)
                .Max();

            return Math.Max(minimumWidth, baseChromeWidth + maxTextWidth);
        }

        private static string StripLabelTags(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            return Regex.Replace(text, @"</?color(?:=[^>]+)?>", string.Empty, RegexOptions.IgnoreCase);
        }

        private void Cancel(Window window)
        {
            window.Close();
        }

        private void Save(Window window)
        {
            window.Close(window);
        }
    }

    public class CoreSongPickerOptionViewModel
    {
        public CoreSongReplacementOption Option { get; }
        public string UiBgmId { get; }
        public string UiSeriesId { get; }
        public string SeriesTitle { get; }
        public string Title { get; }
        public string ToneId { get; }
        public short TestDispOrder { get; }

        public CoreSongPickerOptionViewModel(CoreSongReplacementOption option)
        {
            Option = option;
            UiBgmId = option.UiBgmId;
            UiSeriesId = option.UiSeriesId;
            SeriesTitle = option.SeriesTitle;
            Title = !string.IsNullOrWhiteSpace(option.Title) ? option.Title : option.UiBgmId;
            ToneId = option.ToneId;
            TestDispOrder = option.TestDispOrder;
        }
    }

    public class CoreSongPickerSeriesOptionViewModel
    {
        public string UiSeriesId { get; }
        public string Title { get; }
        public int SongCount { get; }

        public CoreSongPickerSeriesOptionViewModel(string uiSeriesId, string title, int songCount)
        {
            UiSeriesId = uiSeriesId;
            Title = !string.IsNullOrWhiteSpace(title) ? title : uiSeriesId;
            SongCount = songCount;
        }
    }
}
