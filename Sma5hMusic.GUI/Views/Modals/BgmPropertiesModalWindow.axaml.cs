using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using ReactiveUI;
using ReactiveUI.Validation.Extensions;
using Sma5hMusic.GUI.ViewModels;
using Sma5hMusic.GUI.Views.Fields;
using System.Reactive.Disposables;

namespace Sma5hMusic.GUI.Views
{
    public class BgmPropertiesModalWindow : ReactiveWindow<BgmPropertiesModalWindowViewModel>
    {
        private PropertyField GameIdValidation => this.FindControl<PropertyField>("GameId");

        public BgmPropertiesModalWindow()
        {
            this.InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
            FitToWorkingArea();
            this.WhenActivated(disposables =>
            {
                this.BindValidation(ViewModel, vm => vm.SelectedGameTitleViewModel, view => view.GameIdValidation.ValidationError)
                .DisposeWith(disposables);
            });
        }

        //reduce height for low resolution
        private void FitToWorkingArea()
        {
            var primaryScreen = Screens.Primary;
            //fix a crash on WSL2
            if (primaryScreen == null)
                return;

            var newWidth = primaryScreen.WorkingArea.Width;
            if (Width > newWidth)
                Width = newWidth - 100;
        }
    }
}
