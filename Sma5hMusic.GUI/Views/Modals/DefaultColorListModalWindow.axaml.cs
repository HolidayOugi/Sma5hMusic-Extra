using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Sma5h.Mods.Music.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Sma5hMusic.GUI.Views
{
    public class DefaultColorListModalWindow : Window
    {
        private readonly List<MsbtTextColorSetting> _colors;
        private StackPanel _colorsPanel;
        private TextBlock _emptyText;
        private TextBlock _validationText;
        private Button _saveButton;

        public DefaultColorListModalWindow() : this(Array.Empty<MsbtTextColorSetting>())
        {
        }

        public DefaultColorListModalWindow(IEnumerable<MsbtTextColorSetting> colors)
        {
            _colors = MsbtRichTextColorHelper.NormalizeDefaultColorSettings(colors);
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
            _colorsPanel = this.FindControl<StackPanel>("ColorsPanel");
            _emptyText = this.FindControl<TextBlock>("EmptyText");
            _validationText = this.FindControl<TextBlock>("ValidationText");
            _saveButton = this.FindControl<Button>("SaveButton");
            this.FindControl<Button>("AddButton").Click += async (_, _) => await AddColor();
            _saveButton.Click += (_, _) => Close(
                MsbtRichTextColorHelper.NormalizeDefaultColorSettings(_colors));
            this.FindControl<Button>("CancelButton").Click += (_, _) => Close();
            RefreshRows();
        }

        private async Task AddColor()
        {
            var hex = await new CustomTextColorModalWindow().ShowDialog<string>(this);
            var normalized = NormalizeHex(hex);
            if (normalized == null || _colors.Any(p =>
                    string.Equals(p.Hex, normalized, StringComparison.OrdinalIgnoreCase)))
                return;

            _colors.Add(new MsbtTextColorSetting(normalized, normalized));
            RefreshRows();
        }

        private async Task EditColor(int index)
        {
            var hex = await new CustomTextColorModalWindow(_colors[index].Hex).ShowDialog<string>(this);
            var normalized = NormalizeHex(hex);
            if (normalized == null ||
                _colors.Where((_, colorIndex) => colorIndex != index)
                    .Any(p => string.Equals(p.Hex, normalized, StringComparison.OrdinalIgnoreCase)))
                return;

            _colors[index].Hex = normalized;
            RefreshRows();
        }

        private void RefreshRows()
        {
            _colorsPanel.Children.Clear();
            _emptyText.IsVisible = _colors.Count == 0;

            for (var index = 0; index < _colors.Count; index++)
            {
                var colorIndex = index;
                var preview = new Border
                {
                    Width = 24,
                    Height = 24,
                    Background = Brush.Parse(_colors[index].Hex),
                    BorderBrush = Brush.Parse("#777777"),
                    BorderThickness = new Avalonia.Thickness(1),
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
                };
                var hexLabel = new TextBlock
                {
                    Text = _colors[index].Hex,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
                };
                var displayName = new TextBox
                {
                    Text = _colors[index].DisplayName,
                    Watermark = "Display name",
                    MaxLength = MsbtRichTextColorHelper.MaxDisplayNameLength,
                    Margin = new Avalonia.Thickness(0, 0, 6, 0)
                };
                displayName.GetObservable(TextBox.TextProperty).Subscribe(value =>
                {
                    _colors[colorIndex].DisplayName = value;
                    UpdateValidation();
                });
                var editButton = new Button
                {
                    Content = "Edit",
                    Margin = new Avalonia.Thickness(0, 0, 6, 0)
                };
                editButton.Click += async (_, _) => await EditColor(colorIndex);
                var deleteButton = new Button { Content = "Delete" };
                deleteButton.Click += (_, _) =>
                {
                    _colors.RemoveAt(colorIndex);
                    RefreshRows();
                };

                var row = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("32,90,*,Auto,Auto")
                };
                Grid.SetColumn(preview, 0);
                Grid.SetColumn(hexLabel, 1);
                Grid.SetColumn(displayName, 2);
                Grid.SetColumn(editButton, 3);
                Grid.SetColumn(deleteButton, 4);
                row.Children.Add(preview);
                row.Children.Add(hexLabel);
                row.Children.Add(displayName);
                row.Children.Add(editButton);
                row.Children.Add(deleteButton);
                _colorsPanel.Children.Add(row);
            }

            UpdateValidation();
        }

        private void UpdateValidation()
        {
            var valid = _colors.All(p => !string.IsNullOrWhiteSpace(p.DisplayName));
            _saveButton.IsEnabled = valid;
            _validationText.Text = valid ? string.Empty : "Every color needs a display name.";
        }

        private static string NormalizeHex(string value)
        {
            var hex = (value ?? string.Empty).Trim().TrimStart('#');
            return hex.Length == 6 && hex.All(Uri.IsHexDigit)
                ? "#" + hex.ToUpperInvariant()
                : null;
        }
    }
}
