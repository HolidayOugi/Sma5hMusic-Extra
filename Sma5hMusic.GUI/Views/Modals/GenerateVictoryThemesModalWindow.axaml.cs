using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using ReactiveUI;
using Sma5hMusic.GUI.ViewModels;

namespace Sma5hMusic.GUI.Views
{
    public class GenerateVictoryThemesModalWindow : ReactiveWindow<GenerateVictoryThemesModalWindowViewModel>
    {
        public GenerateVictoryThemesModalWindow()
        {
            this.InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
