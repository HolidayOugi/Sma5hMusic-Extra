using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
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
        private readonly IDisposable _editorTextSubscription;
        private readonly IDisposable _selectionStartSubscription;
        private readonly IDisposable _selectionEndSubscription;
        private readonly IDisposable _dropDownOpenSubscription;
        private INotifyPropertyChanged _viewModelNotifier;
        private object _dataContext;
        private List<MsbtRichTextSpan> _spans = new List<MsbtRichTextSpan>();
        private string _plainText = string.Empty;
        private int _selectionStart;
        private int _selectionEnd;
        private int _pendingColorSelectionStart;
        private int _pendingColorSelectionEnd;
        private bool _colorAppliedDuringDropDown;
        private bool _isChoosingCustomColor;
        private bool _isSyncing;
        private MsbtTextColor _lastTextColor = MsbtRichTextColorHelper.DefaultColor;

        public MsbtRichTextEditorController(TextBox editor, StackPanel richTextPreview, ComboBox textColorComboBox)
        {
            _editor = editor;
            _richTextPreview = richTextPreview;
            _textColorComboBox = textColorComboBox;

            if (_editor != null)
            {
                _editorTextSubscription = _editor.GetObservable(TextBox.TextProperty).Subscribe(_ => EditorTextChanged());
                _selectionStartSubscription = _editor.GetObservable(TextBox.SelectionStartProperty).Subscribe(value => UpdateSelection(value, _selectionEnd));
                _selectionEndSubscription = _editor.GetObservable(TextBox.SelectionEndProperty).Subscribe(value => UpdateSelection(_selectionStart, value));
                _editor.PointerPressed += EditorPointerPressed;
            }

            if (_textColorComboBox != null)
            {
                _textColorComboBox.PointerPressed += TextColorComboBoxPointerPressed;
                _textColorComboBox.SelectionChanged += TextColorComboBoxSelectionChanged;
                _dropDownOpenSubscription = _textColorComboBox.GetObservable(ComboBox.IsDropDownOpenProperty)
                    .Subscribe(TextColorDropDownOpenChanged);
            }
        }

        public void SetDataContext(object dataContext)
        {
            if (_viewModelNotifier != null)
                _viewModelNotifier.PropertyChanged -= ViewModelPropertyChanged;

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
                _editor.PointerPressed -= EditorPointerPressed;
            if (_textColorComboBox != null)
            {
                _textColorComboBox.PointerPressed -= TextColorComboBoxPointerPressed;
                _textColorComboBox.SelectionChanged -= TextColorComboBoxSelectionChanged;
            }

            _editorTextSubscription?.Dispose();
            _selectionStartSubscription?.Dispose();
            _selectionEndSubscription?.Dispose();
            _dropDownOpenSubscription?.Dispose();
        }

        private void EditorPointerPressed(object sender, PointerPressedEventArgs e)
        {
            ClearPendingColorSelection();
        }

        private void TextColorComboBoxPointerPressed(object sender, PointerPressedEventArgs e)
        {
            StorePendingColorSelection();
        }

        private void TextColorDropDownOpenChanged(bool isOpen)
        {
            if (isOpen)
            {
                _colorAppliedDuringDropDown = false;
                StorePendingColorSelection();
                return;
            }

            if (_isChoosingCustomColor)
                return;

            if (_colorAppliedDuringDropDown)
                ClearPendingColorSelection();
            else
                ApplySelectedColorToSelection(_textColorComboBox);
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
            var start = Math.Min(_selectionStart, _selectionEnd);
            var end = Math.Max(_selectionStart, _selectionEnd);
            if (viewModel.IsResettingTextColorSelection)
            {
                ClearPendingColorSelection();
                return false;
            }

            if (end <= start)
            {
                start = _pendingColorSelectionStart;
                end = _pendingColorSelectionEnd;
            }
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
        }

        private void UpdateSelection(int start, int end)
        {
            _selectionStart = start;
            _selectionEnd = end;

            var selectedStart = Math.Min(start, end);
            var selectedEnd = Math.Max(start, end);
            if (selectedEnd > selectedStart)
            {
                _pendingColorSelectionStart = selectedStart;
                _pendingColorSelectionEnd = selectedEnd;
            }
        }

        private void StorePendingColorSelection()
        {
            var start = Math.Min(_selectionStart, _selectionEnd);
            var end = Math.Max(_selectionStart, _selectionEnd);
            if (end > start)
            {
                _pendingColorSelectionStart = start;
                _pendingColorSelectionEnd = end;
            }
        }

        private void ClearPendingColorSelection()
        {
            _pendingColorSelectionStart = 0;
            _pendingColorSelectionEnd = 0;
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
            foreach (var span in _spans)
            {
                var color = MsbtRichTextColorHelper.GetColor(span.ColorId) ?? MsbtRichTextColorHelper.DefaultColor;
                var textBlock = new TextBlock
                {
                    Text = span.Text
                };
                if (!color.IsDefault)
                    textBlock.Foreground = Brush.Parse(color.Hex);

                _richTextPreview.Children.Add(textBlock);
            }
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
