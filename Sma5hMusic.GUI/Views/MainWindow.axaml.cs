using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Sma5hMusic.GUI.Interfaces;
using System;

namespace Sma5hMusic.GUI.Views
{
    public class MainWindow : Window, IDialogWindow
    {
        public MainWindow()
        {
            InitializeComponent();

            //custom icon for Linux
            if (OperatingSystem.IsLinux())
            {
                using var iconStream = AvaloniaLocator.Current.GetService<IAssetLoader>().Open(
                    new Uri("avares://Sma5hMusic.GUI/Assets/sma5hmusic-logo.png"));
                Icon = new WindowIcon(iconStream);
            }
        }

        public Window Window => this;

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
