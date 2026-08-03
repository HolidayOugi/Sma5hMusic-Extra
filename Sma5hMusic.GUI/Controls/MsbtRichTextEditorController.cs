using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Sma5h.Mods.Music.Helpers;
using Sma5hMusic.GUI.ViewModels;
using Sma5hMusic.GUI.Views;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace Sma5hMusic.GUI.Controls
{
    public sealed class MsbtRichTextEditorController : IDisposable
    {
        private readonly TextBox _editor;
        private readonly StackPanel _richTextPreview;
        private readonly ComboBox _textColorComboBox;
        private readonly Button _smallTextMarkerButton;
        private readonly IDisposable _editorTextSubscription;
        private readonly IDisposable _selectionStartSubscription;
        private readonly IDisposable _selectionEndSubscription;
        private readonly IDisposable _dropDownOpenSubscription;
        private const double PreviewFontSize = 13;
        private const double SmallTextFontScale = 0.8;
        private static readonly FontFamily PreviewFontFamily = FontFamily.Default;
        private INotifyPropertyChanged _viewModelNotifier;
        private object _dataContext;
        private List<MsbtRichTextSpan> _spans = new List<MsbtRichTextSpan>();
        private string _plainText = string.Empty;
        private int _selectionStart;
        private int _selectionEnd;
        private int _pendingColorSelectionStart;
        private int _pendingColorSelectionEnd;
        private bool _hasPendingColorSelection;
        private bool _colorAppliedDuringDropDown;
        private bool _isUsingColorPicker;
        private bool _isUsingSmallTextMarkerButton;
        private bool _isChoosingCustomColor;
        private bool _isSyncing;
        private MsbtTextColor _lastTextColor = MsbtRichTextColorHelper.DefaultColor;

        public MsbtRichTextEditorController(TextBox editor, StackPanel richTextPreview, ComboBox textColorComboBox, Button smallTextMarkerButton)
        {
            _editor = editor;
            _richTextPreview = richTextPreview;
            _textColorComboBox = textColorComboBox;
            _smallTextMarkerButton = smallTextMarkerButton;

            if (_editor != null)
            {
                _editorTextSubscription = _editor.GetObservable(TextBox.TextProperty).Subscribe(_ => EditorTextChanged());
                _selectionStartSubscription = _editor.GetObservable(TextBox.SelectionStartProperty).Subscribe(value => UpdateSelection(value, _selectionEnd));
                _selectionEndSubscription = _editor.GetObservable(TextBox.SelectionEndProperty).Subscribe(value => UpdateSelection(_selectionStart, value));
                _editor.AddHandler(InputElement.PointerPressedEvent, EditorPointerPressed, RoutingStrategies.Tunnel, true);
            }

            if (_textColorComboBox != null)
            {
                _textColorComboBox.AddHandler(InputElement.PointerPressedEvent, TextColorComboBoxPointerPressed, RoutingStrategies.Tunnel, true);
                _textColorComboBox.SelectionChanged += TextColorComboBoxSelectionChanged;
                _dropDownOpenSubscription = _textColorComboBox.GetObservable(ComboBox.IsDropDownOpenProperty)
                    .Subscribe(TextColorDropDownOpenChanged);
            }

            if (_smallTextMarkerButton != null)
            {
                _smallTextMarkerButton.AddHandler(InputElement.PointerPressedEvent, SmallTextMarkerButtonPointerPressed, RoutingStrategies.Tunnel, true);
                _smallTextMarkerButton.Click += SmallTextMarkerButtonClick;
            }
        }

        public void SetDataContext(object dataContext)
        {
            if (_viewModelNotifier != null)
                _viewModelNotifier.PropertyChanged -= ViewModelPropertyChanged;

            //update bound view model
            _dataContext = dataContext;
            _viewModelNotifier = _dataContext as INotifyPropertyChanged;
            if (_viewModelNotifier != null)
                _viewModelNotifier.PropertyChanged += ViewModelPropertyChanged;

            SyncFromViewModel();
        }

        public void Dispose()
        {
            if (_viewModelNotifier != null)
                _viewModelNotifier.PropertyChanged -= ViewModelPropertyChanged;
            if (_editor != null)
                _editor.RemoveHandler(InputElement.PointerPressedEvent, EditorPointerPressed);
            if (_textColorComboBox != null)
            {
                _textColorComboBox.RemoveHandler(InputElement.PointerPressedEvent, TextColorComboBoxPointerPressed);
                _textColorComboBox.SelectionChanged -= TextColorComboBoxSelectionChanged;
            }
            if (_smallTextMarkerButton != null)
            {
                _smallTextMarkerButton.RemoveHandler(InputElement.PointerPressedEvent, SmallTextMarkerButtonPointerPressed);
                _smallTextMarkerButton.Click -= SmallTextMarkerButtonClick;
            }

            _editorTextSubscription?.Dispose();
            _selectionStartSubscription?.Dispose();
            _selectionEndSubscription?.Dispose();
            _dropDownOpenSubscription?.Dispose();
        }

        private void EditorPointerPressed(object sender, PointerPressedEventArgs e)
        {
            //clear saved selection when editor gets focus
            _isUsingSmallTextMarkerButton = false;
            ClearPendingColorSelection();
        }

        private void TextColorComboBoxPointerPressed(object sender, PointerPressedEventArgs e)
        {
            //save text selection before color picker gets focus
            if (!_isUsingColorPicker)
                StorePendingColorSelection(true);
            _isUsingColorPicker = true;
        }

        private void SmallTextMarkerButtonPointerPressed(object sender, PointerPressedEventArgs e)
        {
            //save text selection before button gets focus
            StorePendingColorSelection(true);
            _isUsingSmallTextMarkerButton = true;
        }


        //adds small text marker to end of line or selection
        private void SmallTextMarkerButtonClick(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (_editor == null || !(_dataContext is MSBTFieldViewModel viewModel) || !viewModel.EnableColorFormatting)
            {
                _isUsingSmallTextMarkerButton = false;
                return;
            }

            int start;
            int end;
            if (_hasPendingColorSelection)
            {
                start = _pendingColorSelectionStart;
                end = _pendingColorSelectionEnd;
            }
            else
            {
                start = Math.Min(_selectionStart, _selectionEnd);
                end = Math.Max(_selectionStart, _selectionEnd);
            }

            var chars = ToColoredCharacters();
            var markerColorId = MsbtRichTextColorHelper.DefaultColor.Id;
            if (end > start)
            {
                start = Math.Max(0, Math.Min(start, chars.Count));
                end = Math.Max(start, Math.Min(end, chars.Count));
                chars.InsertRange(end, "}}".Select(p => new ColoredCharacter(p, markerColorId)));
                chars.InsertRange(start, "{{".Select(p => new ColoredCharacter(p, markerColorId)));
            }
            else
            {
                start = chars.Count;
                chars.AddRange("{{}}".Select(p => new ColoredCharacter(p, markerColorId)));
            }

            _spans = FromColoredCharacters(chars);
            _plainText = MsbtRichTextColorHelper.ToPlainText(_spans);
            _isSyncing = true;
            _editor.Text = _plainText;
            _isSyncing = false;
            SaveToViewModel(viewModel);
            UpdatePreview();
            CollapseEditorSelection(end > start ? end + 4 : _plainText.Length);
            ClearPendingColorSelection();
            _isUsingSmallTextMarkerButton = false;
        }

        private void TextColorDropDownOpenChanged(bool isOpen)
        {
            if (isOpen)
            {
                //save selection before opening color menu
                if (!_isUsingColorPicker)
                    StorePendingColorSelection(true);
                _isUsingColorPicker = true;
                _colorAppliedDuringDropDown = false;
                return;
            }

            if (_isChoosingCustomColor)
                return;

            if (_colorAppliedDuringDropDown)
                ClearPendingColorSelection();
            else
                //apply color
                ApplySelectedColorToSelection(_textColorComboBox);

            _isUsingColorPicker = false;
        }

        private void ViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MSBTFieldViewModel.CurrentLocalizedValue))
                SyncFromViewModel();
        }

        private void SyncFromViewModel()
        {
            if (_isSyncing || !(_dataContext is MSBTFieldViewModel viewModel) || _editor == null)
                return;

            _isSyncing = true;
            if (viewModel.EnableColorFormatting)
            {
                //hide color tags from editor
                _spans = MsbtRichTextColorHelper.Parse(viewModel.CurrentLocalizedValue);
                _plainText = MsbtRichTextColorHelper.ToPlainText(_spans);
                _editor.Text = _plainText;
                UpdatePreview();
            }
            else
            {
                _spans.Clear();
                _plainText = viewModel.CurrentLocalizedValue ?? string.Empty;
                _editor.Text = _plainText;
            }

            _selectionStart = 0;
            _selectionEnd = 0;
            if (viewModel.SelectedTextColor != null && !viewModel.SelectedTextColor.IsCustomSelector)
                _lastTextColor = viewModel.SelectedTextColor;
            ClearPendingColorSelection();
            _isSyncing = false;
        }

        private void EditorTextChanged()
        {
            if (_isSyncing || !(_dataContext is MSBTFieldViewModel viewModel) || _editor == null)
                return;

            var newPlainText = _editor.Text ?? string.Empty;
            if (!viewModel.EnableColorFormatting)
            {
                SavePlainTextToViewModel(viewModel, newPlainText);
                _plainText = newPlainText;
                return;
            }

            //apply text edit while preserving existing colors
            ApplyTextChange(_plainText, newPlainText, viewModel.SelectedTextColor?.Id ?? MsbtRichTextColorHelper.DefaultColor.Id);
            ClearPendingColorSelection();
            _plainText = newPlainText;
            SaveToViewModel(viewModel);
            UpdatePreview();
        }

        private async void TextColorComboBoxSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var comboBox = sender as ComboBox;
            var selectedColor = comboBox?.SelectedItem as MsbtTextColor;
            if (selectedColor?.IsCustomSelector == true)
            {
                //open custom color dialog
                await SelectCustomColor(comboBox);
                return;
            }

            if (_isChoosingCustomColor)
                return;

            if (selectedColor != null)
                _lastTextColor = selectedColor;

            if (ApplySelectedColorToSelection(sender as ComboBox))
                _colorAppliedDuringDropDown = true;
        }

        private bool ApplySelectedColorToSelection(ComboBox comboBox)
        {
            if (_isSyncing || !(_dataContext is MSBTFieldViewModel viewModel) || !viewModel.EnableColorFormatting || _editor == null)
                return false;

            return ApplyColorToSelection(viewModel, comboBox?.SelectedItem as MsbtTextColor);
        }

        private bool ApplyColorToSelection(MSBTFieldViewModel viewModel, MsbtTextColor selectedColor)
        {
            if (viewModel.IsResettingTextColorSelection)
            {
                ClearPendingColorSelection();
                return false;
            }

            int start;
            int end;
            if (_hasPendingColorSelection)
            {
                start = _pendingColorSelectionStart;
                end = _pendingColorSelectionEnd;
            }
            else
            {
                start = Math.Min(_selectionStart, _selectionEnd);
                end = Math.Max(_selectionStart, _selectionEnd);
            }

            start = Math.Max(0, Math.Min(start, _plainText.Length));
            end = Math.Max(start, Math.Min(end, _plainText.Length));
            if (end <= start)
                return false;

            if (selectedColor?.IsCustomSelector == true)
                return false;

            ApplyColor(start, end - start, selectedColor?.Id ?? MsbtRichTextColorHelper.DefaultColor.Id);
            ClearPendingColorSelection();
            CollapseEditorSelection(start);
            SaveToViewModel(viewModel);
            UpdatePreview();
            return true;
        }

        private async System.Threading.Tasks.Task SelectCustomColor(ComboBox comboBox)
        {
            if (_isChoosingCustomColor || !(_dataContext is MSBTFieldViewModel viewModel) || comboBox == null)
                return;

            _isChoosingCustomColor = true;
            _colorAppliedDuringDropDown = true;
            StorePendingColorSelection();
            comboBox.IsDropDownOpen = false;

            var previousColor = _lastTextColor ?? MsbtRichTextColorHelper.DefaultColor;
            var owner = comboBox.GetVisualRoot() as Window;
            string hexColor = null;
            if (owner != null)
            {
                var dialog = new CustomTextColorModalWindow();
                hexColor = await dialog.ShowDialog<string>(owner);
            }

            var color = MsbtRichTextColorHelper.GetColor(hexColor);
            if (color != null && !color.IsDefault && !color.IsCustomSelector)
            {
                viewModel.AddCustomTextColor(color);
                _lastTextColor = color;
                ApplyColorToSelection(viewModel, color);
            }
            else
            {
                viewModel.SelectedTextColor = previousColor;
            }

            ClearPendingColorSelection();
            _isChoosingCustomColor = false;
            _isUsingColorPicker = false;
        }

        private void UpdateSelection(int start, int end)
        {
            _selectionStart = start;
            _selectionEnd = end;

            var selectedStart = Math.Min(start, end);
            var selectedEnd = Math.Max(start, end);
            if (selectedEnd > selectedStart && !_isUsingColorPicker && !_isUsingSmallTextMarkerButton && !_isChoosingCustomColor)
            {
                _pendingColorSelectionStart = selectedStart;
                _pendingColorSelectionEnd = selectedEnd;
                _hasPendingColorSelection = true;
            }
            else
            {
                //after UI update clean the selection
                Dispatcher.UIThread.Post(() =>
                {
                    if (!_isUsingColorPicker && !_isUsingSmallTextMarkerButton && _selectionStart == _selectionEnd)
                        ClearPendingColorSelection();
                }, DispatcherPriority.Background);
            }
        }

        private void StorePendingColorSelection(bool overwrite = false)
        {
            if (_hasPendingColorSelection && !overwrite)
                return;

            var selectionStart = _editor?.SelectionStart ?? _selectionStart;
            var selectionEnd = _editor?.SelectionEnd ?? _selectionEnd;
            var start = Math.Max(0, Math.Min(Math.Min(selectionStart, selectionEnd), _plainText.Length));
            var end = Math.Max(start, Math.Min(Math.Max(selectionStart, selectionEnd), _plainText.Length));
            if (end > start)
            {
                _pendingColorSelectionStart = start;
                _pendingColorSelectionEnd = end;
                _hasPendingColorSelection = true;
            }
            else if (overwrite)
                ClearPendingColorSelection();
        }

        private void ClearPendingColorSelection()
        {
            _pendingColorSelectionStart = 0;
            _pendingColorSelectionEnd = 0;
            _hasPendingColorSelection = false;
        }

        private void CollapseEditorSelection(int position)
        {
            var textLength = _editor?.Text?.Length ?? 0;
            var collapsedPosition = Math.Max(0, Math.Min(position, textLength));
            _selectionStart = collapsedPosition;
            _selectionEnd = collapsedPosition;
            if (_editor != null)
            {
                _editor.SelectionStart = collapsedPosition;
                _editor.SelectionEnd = collapsedPosition;
            }
        }

        private void SaveToViewModel(MSBTFieldViewModel viewModel)
        {
            _isSyncing = true;
            viewModel.CurrentLocalizedValue = MsbtRichTextColorHelper.Serialize(_spans);
            _isSyncing = false;
        }

        private void SavePlainTextToViewModel(MSBTFieldViewModel viewModel, string text)
        {
            _isSyncing = true;
            viewModel.CurrentLocalizedValue = text;
            _isSyncing = false;
        }

        private void ApplyTextChange(string oldText, string newText, string colorId)
        {
            var prefixLength = 0;
            while (prefixLength < oldText.Length &&
                   prefixLength < newText.Length &&
                   oldText[prefixLength] == newText[prefixLength])
            {
                prefixLength++;
            }

            var oldSuffix = oldText.Length - 1;
            var newSuffix = newText.Length - 1;
            while (oldSuffix >= prefixLength &&
                   newSuffix >= prefixLength &&
                   oldText[oldSuffix] == newText[newSuffix])
            {
                oldSuffix--;
                newSuffix--;
            }

            var removeCount = oldSuffix >= prefixLength ? oldSuffix - prefixLength + 1 : 0;
            var insertText = newSuffix >= prefixLength ? newText.Substring(prefixLength, newSuffix - prefixLength + 1) : string.Empty;

            var chars = ToColoredCharacters();
            if (removeCount > 0)
                chars.RemoveRange(prefixLength, removeCount);
            if (!string.IsNullOrEmpty(insertText))
                chars.InsertRange(prefixLength, insertText.Select(p => new ColoredCharacter(p, colorId)));

            _spans = FromColoredCharacters(chars);
        }

        private void ApplyColor(int start, int length, string colorId)
        {
            var chars = ToColoredCharacters();
            var end = Math.Min(start + length, chars.Count);
            for (var i = Math.Max(0, start); i < end; i++)
                chars[i].ColorId = MsbtRichTextColorHelper.NormalizeColorId(colorId);

            _spans = FromColoredCharacters(chars);
        }

        private List<ColoredCharacter> ToColoredCharacters()
        {
            return _spans
                .SelectMany(span => span.Text.Select(character => new ColoredCharacter(character, span.ColorId)))
                .ToList();
        }

        private static List<MsbtRichTextSpan> FromColoredCharacters(IEnumerable<ColoredCharacter> characters)
        {
            var spans = new List<MsbtRichTextSpan>();
            foreach (var character in characters)
            {
                var colorId = MsbtRichTextColorHelper.NormalizeColorId(character.ColorId);
                var last = spans.LastOrDefault();
                if (last != null && string.Equals(last.ColorId, colorId, StringComparison.OrdinalIgnoreCase))
                    last.Text += character.Value;
                else
                    spans.Add(new MsbtRichTextSpan(character.Value.ToString(), colorId));
            }

            return spans;
        }

        private void UpdatePreview()
        {
            if (_richTextPreview == null)
                return;

            _richTextPreview.Children.Clear();
            var chars = ToColoredCharacters();
            for (var i = 0; i < chars.Count;)
            {
                if (IsSmallTextStart(chars, i))
                {
                    var end = FindSmallTextEnd(chars, i + 2);
                    if (end >= 0)
                    {
                        AppendPreviewText(chars.Skip(i + 2).Take(end - i - 2), true);
                        i = end + 2;
                        continue;
                    }
                }

                var nextMarker = FindNextSmallTextStart(chars, i + 1);
                var takeCount = (nextMarker >= 0 ? nextMarker : chars.Count) - i;
                AppendPreviewText(chars.Skip(i).Take(takeCount), false);
                i += takeCount;
            }
        }

        private void AppendPreviewText(IEnumerable<ColoredCharacter> characters, bool isSmallText)
        {
            var activeColorId = (string)null;
            var text = string.Empty;
            foreach (var character in characters)
            {
                if (!string.Equals(activeColorId, character.ColorId, StringComparison.OrdinalIgnoreCase))
                {
                    AddPreviewTextBlock(text, activeColorId, isSmallText);
                    text = string.Empty;
                    activeColorId = character.ColorId;
                }

                text += character.Value;
            }

            AddPreviewTextBlock(text, activeColorId, isSmallText);
        }

        private void AddPreviewTextBlock(string text, string colorId, bool isSmallText)
        {
            if (string.IsNullOrEmpty(text))
                return;

            var color = MsbtRichTextColorHelper.GetColor(colorId) ?? MsbtRichTextColorHelper.DefaultColor;
            var fontSize = isSmallText ? PreviewFontSize * SmallTextFontScale : PreviewFontSize;
            var textBlock = new TextBlock
            {
                Text = text,
                FontFamily = PreviewFontFamily,
                FontSize = fontSize,
                VerticalAlignment = VerticalAlignment.Bottom
            };
            if (!color.IsDefault)
                textBlock.Foreground = Brush.Parse(color.Hex);

            _richTextPreview.Children.Add(textBlock);
        }

        private static bool IsSmallTextStart(IList<ColoredCharacter> characters, int index)
        {
            return index + 1 < characters.Count &&
                   characters[index].Value == '{' &&
                   characters[index + 1].Value == '{';
        }

        private static bool IsSmallTextEnd(IList<ColoredCharacter> characters, int index)
        {
            return index + 1 < characters.Count &&
                   characters[index].Value == '}' &&
                   characters[index + 1].Value == '}';
        }

        private static int FindSmallTextEnd(IList<ColoredCharacter> characters, int startIndex)
        {
            for (var i = startIndex; i < characters.Count - 1; i++)
            {
                if (IsSmallTextEnd(characters, i))
                    return i;
            }

            return -1;
        }

        private static int FindNextSmallTextStart(IList<ColoredCharacter> characters, int startIndex)
        {
            for (var i = startIndex; i < characters.Count - 1; i++)
            {
                if (IsSmallTextStart(characters, i))
                    return i;
            }

            return -1;
        }

        private class ColoredCharacter
        {
            public ColoredCharacter(char value, string colorId)
            {
                Value = value;
                ColorId = MsbtRichTextColorHelper.NormalizeColorId(colorId);
            }

            public char Value { get; }
            public string ColorId { get; set; }
        }
    }
}
