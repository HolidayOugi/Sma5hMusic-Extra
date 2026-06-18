using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using System;
using System.Linq;

namespace Sma5hMusic.GUI.Views
{
    public class CustomTextColorModalWindow : Window
    {
        private TextBox _hexTextBox;
        private TextBlock _validationText;
        private Button _okButton;
        private Button _cancelButton;
        private bool _isSyncing;

        public CustomTextColorModalWindow()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
            _hexTextBox = this.FindControl<TextBox>("HexTextBox");
            _validationText = this.FindControl<TextBlock>("ValidationText");
            _okButton = this.FindControl<Button>("OkButton");
            _cancelButton = this.FindControl<Button>("CancelButton");

            _okButton.Click += (_, _) => Close("#" + (_hexTextBox.Text ?? string.Empty));
            _cancelButton.Click += (_, _) => Close();
            _hexTextBox.GetObservable(TextBox.TextProperty).Subscribe(_ => ValidateHexText());
            ValidateHexText();
        }

        private void ValidateHexText()
        {
            if (_isSyncing || _hexTextBox == null)
                return;

            var text = _hexTextBox.Text ?? string.Empty;
            var sanitized = new string(text.Where(Uri.IsHexDigit).ToArray()).ToUpperInvariant();
            if (sanitized.Length > 8)
                sanitized = sanitized.Substring(0, 8);

            if (!string.Equals(text, sanitized, StringComparison.Ordinal))
            {
                _isSyncing = true;
                _hexTextBox.Text = sanitized;
                _isSyncing = false;
            }

            var valid = sanitized.Length == 6 || sanitized.Length == 8;
            _okButton.IsEnabled = valid;
            _validationText.Text = string.IsNullOrEmpty(sanitized) || valid ? string.Empty : "Use 6 or 8 hex digits.";
        }
    }
}
