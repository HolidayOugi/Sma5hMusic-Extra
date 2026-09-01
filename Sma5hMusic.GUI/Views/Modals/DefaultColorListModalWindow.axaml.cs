using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
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
        private const string ColorDragDataFormat = "DEFAULT_COLOR";
        private readonly List<MsbtTextColorSetting> _colors;
        private readonly List<Border> _dropIndicatorLines = new List<Border>();
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

        private async Task StartColorDrag(
            MsbtTextColorSetting color,
            Control dragHandle,
            PointerPressedEventArgs args)
        {
            if (!args.GetCurrentPoint(dragHandle).Properties.IsLeftButtonPressed)
                return;

            var dragData = new DataObject();
            dragData.Set(ColorDragDataFormat, color);
            args.Handled = true;
            await DragDrop.DoDragDrop(args, dragData, DragDropEffects.Move);
            HideDropIndicators();
        }

        private void ColorRowDragOver(int targetIndex, Control targetRow, DragEventArgs args)
        {
            if (!args.Data.Contains(ColorDragDataFormat))
            {
                args.DragEffects = DragDropEffects.None;
                return;
            }

            var insertionIndex = targetIndex;
            if (args.GetPosition(targetRow).Y >= targetRow.Bounds.Height / 2)
                insertionIndex++;

            ShowDropIndicator(insertionIndex);
            args.DragEffects = DragDropEffects.Move;
            args.Handled = true;
        }

        private void DropColor(int insertionIndex, DragEventArgs args)
        {
            var color = args.Data.Get(ColorDragDataFormat) as MsbtTextColorSetting;
            var sourceIndex = color == null ? -1 : _colors.IndexOf(color);
            if (sourceIndex < 0)
                return;

            if (sourceIndex < insertionIndex)
                insertionIndex--;

            insertionIndex = Math.Max(0, Math.Min(insertionIndex, _colors.Count - 1));
            if (sourceIndex == insertionIndex)
            {
                HideDropIndicators();
                args.Handled = true;
                return;
            }

            _colors.RemoveAt(sourceIndex);
            _colors.Insert(insertionIndex, color);
            RefreshRows();
            args.Handled = true;
        }

        private Control CreateDropZone(int insertionIndex)
        {
            var line = new Border
            {
                Height = 2,
                Background = Brush.Parse("#3A96DD"),
                Opacity = 0,
                IsHitTestVisible = false,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };
            var dropZone = new Grid
            {
                Height = 10,
                Background = Brushes.Transparent
            };
            dropZone.Children.Add(line);
            _dropIndicatorLines.Add(line);

            DragDrop.SetAllowDrop(dropZone, true);
            dropZone.AddHandler(
                DragDrop.DragOverEvent,
                (_, args) =>
                {
                    if (!args.Data.Contains(ColorDragDataFormat))
                    {
                        args.DragEffects = DragDropEffects.None;
                        return;
                    }

                    ShowDropIndicator(insertionIndex);
                    args.DragEffects = DragDropEffects.Move;
                    args.Handled = true;
                },
                Avalonia.Interactivity.RoutingStrategies.Tunnel |
                Avalonia.Interactivity.RoutingStrategies.Bubble,
                true);
            dropZone.AddHandler(
                DragDrop.DropEvent,
                (_, args) => DropColor(insertionIndex, args),
                Avalonia.Interactivity.RoutingStrategies.Tunnel |
                Avalonia.Interactivity.RoutingStrategies.Bubble,
                true);
            return dropZone;
        }

        private void ShowDropIndicator(int insertionIndex)
        {
            for (var index = 0; index < _dropIndicatorLines.Count; index++)
                _dropIndicatorLines[index].Opacity = index == insertionIndex ? 1 : 0;
        }

        private void HideDropIndicators()
        {
            foreach (var line in _dropIndicatorLines)
                line.Opacity = 0;
        }

        private void RefreshRows()
        {
            _colorsPanel.Children.Clear();
            _dropIndicatorLines.Clear();
            _emptyText.IsVisible = _colors.Count == 0;

            for (var index = 0; index < _colors.Count; index++)
            {
                var colorIndex = index;
                if (colorIndex == 0)
                    _colorsPanel.Children.Add(CreateDropZone(0));

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
                var reorderButton = new Button
                {
                    Content = "↕",
                    MinWidth = 30,
                    Padding = new Avalonia.Thickness(6, 3),
                    Margin = new Avalonia.Thickness(0, 0, 6, 0),
                    HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                    FontFamily = new FontFamily("Segoe UI Symbol")
                };
                ToolTip.SetTip(reorderButton, "Drag to reorder");
                reorderButton.AddHandler(
                    InputElement.PointerPressedEvent,
                    async (_, args) => await StartColorDrag(_colors[colorIndex], reorderButton, args),
                    Avalonia.Interactivity.RoutingStrategies.Tunnel,
                    true);
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
                    ColumnDefinitions = new ColumnDefinitions("Auto,32,90,*,Auto,Auto"),
                    Background = Brushes.Transparent
                };
                Grid.SetColumn(reorderButton, 0);
                Grid.SetColumn(preview, 1);
                Grid.SetColumn(hexLabel, 2);
                Grid.SetColumn(displayName, 3);
                Grid.SetColumn(editButton, 4);
                Grid.SetColumn(deleteButton, 5);
                row.Children.Add(reorderButton);
                row.Children.Add(preview);
                row.Children.Add(hexLabel);
                row.Children.Add(displayName);
                row.Children.Add(editButton);
                row.Children.Add(deleteButton);
                DragDrop.SetAllowDrop(row, true);
                row.AddHandler(
                    DragDrop.DragOverEvent,
                    (_, args) => ColorRowDragOver(colorIndex, row, args),
                    Avalonia.Interactivity.RoutingStrategies.Tunnel |
                    Avalonia.Interactivity.RoutingStrategies.Bubble,
                    true);
                row.AddHandler(
                    DragDrop.DropEvent,
                    (_, args) =>
                    {
                        var insertionIndex = colorIndex;
                        if (args.GetPosition(row).Y >= row.Bounds.Height / 2)
                            insertionIndex++;
                        DropColor(insertionIndex, args);
                    },
                    Avalonia.Interactivity.RoutingStrategies.Tunnel |
                    Avalonia.Interactivity.RoutingStrategies.Bubble,
                    true);
                _colorsPanel.Children.Add(row);
                _colorsPanel.Children.Add(CreateDropZone(colorIndex + 1));
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
