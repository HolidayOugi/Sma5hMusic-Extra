using Avalonia.Controls;
using ReactiveUI;
using Sma5hMusic.GUI.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;

namespace Sma5hMusic.GUI.ViewModels
{
    public class SeriesMultiPickerModalWindowViewModel : ViewModelBase
    {
        private bool _hasSelection;

        public ObservableCollection<SeriesMultiPickerOptionViewModel> Series { get; }
        public ReactiveCommand<Window, Unit> ActionCancel { get; }
        public ReactiveCommand<Window, Unit> ActionOK { get; }
        public ReactiveCommand<Unit, Unit> ActionEnableAll { get; }
        public ReactiveCommand<Unit, Unit> ActionDisableAll { get; }

        public bool HasSelection
        {
            get => _hasSelection;
            private set => this.RaiseAndSetIfChanged(ref _hasSelection, value);
        }

        public SeriesMultiPickerModalWindowViewModel(IEnumerable<SeriesSortOption> series)
        {
            Series = new ObservableCollection<SeriesMultiPickerOptionViewModel>(
                series
                    .OrderBy(p => p.Title, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(p => p.UiSeriesId, StringComparer.OrdinalIgnoreCase)
                    .Select(p => new SeriesMultiPickerOptionViewModel(p)));

            foreach (var item in Series)
                item.WhenAnyValue(p => p.IsSelected).Subscribe(_ => RefreshSelectionState());

            ActionCancel = ReactiveCommand.Create<Window>(Cancel);
            ActionOK = ReactiveCommand.Create<Window>(Save, this.WhenAnyValue(p => p.HasSelection));
            ActionEnableAll = ReactiveCommand.Create(SetAllEnabled);
            ActionDisableAll = ReactiveCommand.Create(SetAllDisabled);
            RefreshSelectionState();
        }

        public IEnumerable<string> GetSelectedSeriesIds()
        {
            return Series.Where(p => p.IsSelected).Select(p => p.UiSeriesId);
        }

        private void SetAllEnabled()
        {
            foreach (var item in Series)
                item.IsSelected = true;
        }

        private void SetAllDisabled()
        {
            foreach (var item in Series)
                item.IsSelected = false;
        }

        private void RefreshSelectionState()
        {
            HasSelection = Series.Any(p => p.IsSelected);
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

    public class SeriesMultiPickerOptionViewModel : ReactiveObject
    {
        private bool _isSelected;

        public string UiSeriesId { get; }
        public string Title { get; }
        public int SongCount { get; }

        public bool IsSelected
        {
            get => _isSelected;
            set => this.RaiseAndSetIfChanged(ref _isSelected, value);
        }

        public SeriesMultiPickerOptionViewModel(SeriesSortOption option)
        {
            UiSeriesId = option.UiSeriesId;
            Title = option.Title;
            SongCount = option.SongCount;
        }
    }
}
