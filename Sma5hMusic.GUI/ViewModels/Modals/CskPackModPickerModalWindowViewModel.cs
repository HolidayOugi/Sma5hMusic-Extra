using Avalonia.Controls;
using ReactiveUI;
using Sma5h.Mods.Music.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;

namespace Sma5hMusic.GUI.ViewModels
{
    public class CskPackModPickerModalWindowViewModel : ViewModelBase
    {
        private bool _hasSelection;

        public ObservableCollection<CskPackModOptionViewModel> Mods { get; }
        public ReactiveCommand<Window, Unit> ActionCancel { get; }
        public ReactiveCommand<Window, Unit> ActionOK { get; }
        public ReactiveCommand<Unit, Unit> ActionEnableAll { get; }
        public ReactiveCommand<Unit, Unit> ActionDisableAll { get; }

        public bool HasSelection
        {
            get => _hasSelection;
            private set => this.RaiseAndSetIfChanged(ref _hasSelection, value);
        }

        public CskPackModPickerModalWindowViewModel(IEnumerable<CskPackModOption> mods)
        {
            Mods = new ObservableCollection<CskPackModOptionViewModel>(
                mods
                    .OrderBy(p => p.DisplayName, StringComparer.OrdinalIgnoreCase)
                    .Select(p => new CskPackModOptionViewModel(p)));

            foreach (var item in Mods)
                item.WhenAnyValue(p => p.IsSelected).Subscribe(_ => RefreshSelectionState());

            ActionCancel = ReactiveCommand.Create<Window>(Cancel);
            ActionOK = ReactiveCommand.Create<Window>(Save, this.WhenAnyValue(p => p.HasSelection));
            ActionEnableAll = ReactiveCommand.Create(SetAllEnabled);
            ActionDisableAll = ReactiveCommand.Create(SetAllDisabled);
            RefreshSelectionState();
        }

        public IEnumerable<string> GetSelectedModKeys()
        {
            return Mods.Where(p => p.IsSelected).Select(p => p.Key);
        }

        private void SetAllEnabled()
        {
            foreach (var item in Mods)
                item.IsSelected = true;
        }

        private void SetAllDisabled()
        {
            foreach (var item in Mods)
                item.IsSelected = false;
        }

        private void RefreshSelectionState()
        {
            HasSelection = Mods.Any(p => p.IsSelected);
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

    public class CskPackModOptionViewModel : ReactiveObject
    {
        private bool _isSelected;

        public string Key { get; }
        public string DisplayName { get; }

        public bool IsSelected
        {
            get => _isSelected;
            set => this.RaiseAndSetIfChanged(ref _isSelected, value);
        }

        public CskPackModOptionViewModel(CskPackModOption option)
        {
            Key = option.Key;
            DisplayName = option.DisplayName;
        }
    }
}
