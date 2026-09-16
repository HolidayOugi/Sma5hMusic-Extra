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
    public class AudioTrimModalWindow : ReactiveWindow<AudioTrimModalWindowViewModel>
    {
        private PropertyUIntField TrimStartSampleValidation => this.FindControl<PropertyUIntField>("TrimStartSample");
        private PropertyUIntField TrimStartMsValidation => this.FindControl<PropertyUIntField>("TrimStartMs");
        private PropertyUIntField TrimEndSampleValidation => this.FindControl<PropertyUIntField>("TrimEndSample");
        private PropertyUIntField TrimEndMsValidation => this.FindControl<PropertyUIntField>("TrimEndMs");

        public AudioTrimModalWindow()
        {
            InitializeComponent();
            Opened += async (sender, args) =>
            {
                if (ViewModel != null)
                    await ViewModel.PrepareForOpen();
            };
            Closed += async (sender, args) =>
            {
                if (ViewModel != null)
                    await ViewModel.CloseAsync();
                ViewModel?.Dispose();
            };
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
            this.WhenActivated(disposables =>
            {
                this.BindValidation(ViewModel, vm => vm.TrimStartSample, view => view.TrimStartSampleValidation.ValidationError)
                    .DisposeWith(disposables);
                this.BindValidation(ViewModel, vm => vm.TrimStartMs, view => view.TrimStartMsValidation.ValidationError)
                    .DisposeWith(disposables);
                this.BindValidation(ViewModel, vm => vm.TrimEndSample, view => view.TrimEndSampleValidation.ValidationError)
                    .DisposeWith(disposables);
                this.BindValidation(ViewModel, vm => vm.TrimEndMs, view => view.TrimEndMsValidation.ValidationError)
                    .DisposeWith(disposables);
            });
        }
    }
}
