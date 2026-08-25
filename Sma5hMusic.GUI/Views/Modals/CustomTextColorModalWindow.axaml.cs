using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using System;
using System.Linq;
using System.Runtime.InteropServices;

namespace Sma5hMusic.GUI.Views
{
    public class CustomTextColorModalWindow : Window
    {
        private const int PickerSize = 240;
        private const int HueBarWidth = 28;
        private const double SelectorSize = 18;
        private const double HueSelectorHeight = 8;

        private TextBox _hexTextBox;
        private TextBlock _validationText;
        private Border _colorPreview;
        private Image _colorAreaImage;
        private Grid _colorAreaSelector;
        private Image _hueBarImage;
        private Grid _hueBarSelector;
        private Button _okButton;
        private Button _cancelButton;
        private IDisposable _hexTextSubscription;
        private WriteableBitmap _colorAreaBitmap;
        private WriteableBitmap _hueBarBitmap;
        private readonly string _initialHex;
        private readonly byte[] _colorAreaPixels = new byte[PickerSize * PickerSize * 4];
        private double _hue;
        private double _saturation;
        private double _brightness = 1;
        private bool _isSyncing;

        public CustomTextColorModalWindow() : this(null)
        {
        }

        //open picker with either white or previous value
        public CustomTextColorModalWindow(string initialHex)
        {
            var normalizedHex = NormalizeHexText(initialHex);
            _initialHex = IsValidHex(normalizedHex) ? normalizedHex : "FFFFFF";
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
            _hexTextBox = this.FindControl<TextBox>("HexTextBox");
            _validationText = this.FindControl<TextBlock>("ValidationText");
            _colorPreview = this.FindControl<Border>("ColorPreview");
            _colorAreaImage = this.FindControl<Image>("ColorAreaImage");
            _colorAreaSelector = this.FindControl<Grid>("ColorAreaSelector");
            _hueBarImage = this.FindControl<Image>("HueBarImage");
            _hueBarSelector = this.FindControl<Grid>("HueBarSelector");
            _okButton = this.FindControl<Button>("OkButton");
            _cancelButton = this.FindControl<Button>("CancelButton");

            _colorAreaBitmap = CreateBitmap(PickerSize, PickerSize);
            _hueBarBitmap = CreateHueBar();
            _colorAreaImage.Source = _colorAreaBitmap;
            _hueBarImage.Source = _hueBarBitmap;
            _colorAreaImage.PointerPressed += ColorAreaPointerPressed;
            _colorAreaImage.PointerMoved += ColorAreaPointerMoved;
            _hueBarImage.PointerPressed += HueBarPointerPressed;
            _hueBarImage.PointerMoved += HueBarPointerMoved;
            _okButton.Click += (_, _) => Close("#" + (_hexTextBox.Text ?? string.Empty));
            _cancelButton.Click += (_, _) => Close();
            _hexTextBox.Text = _initialHex;
            _hexTextSubscription = _hexTextBox.GetObservable(TextBox.TextProperty).Subscribe(_ => ValidateHexText());
            Closed += (_, _) =>
            {
                _hexTextSubscription?.Dispose();
                _colorAreaBitmap?.Dispose();
                _hueBarBitmap?.Dispose();
            };
            ValidateHexText();
        }

        private void ValidateHexText()
        {
            if (_isSyncing || _hexTextBox == null)
                return;

            var text = _hexTextBox.Text ?? string.Empty;
            var sanitized = NormalizeHexText(text);

            if (!string.Equals(text, sanitized, StringComparison.Ordinal))
            {
                _isSyncing = true;
                _hexTextBox.Text = sanitized;
                _isSyncing = false;
            }
            
            //if new hex value is valid, update the color picker to match it
            var valid = IsValidHex(sanitized);
            if (valid)
            {
                var red = Convert.ToByte(sanitized.Substring(0, 2), 16);
                var green = Convert.ToByte(sanitized.Substring(2, 2), 16);
                var blue = Convert.ToByte(sanitized.Substring(4, 2), 16);
                RgbToHsv(red, green, blue, out _hue, out _saturation, out _brightness);

                UpdateColorArea();
                UpdateColorAreaSelector();
                UpdateHueBarSelector();
            }

            UpdateValidationState(sanitized, valid);
        }

        private void ColorAreaPointerPressed(object sender, PointerPressedEventArgs args)
        {
            UpdateColorFromArea(args.GetPosition(_colorAreaImage));
        }

        private void ColorAreaPointerMoved(object sender, PointerEventArgs args)
        {
            if (args.GetCurrentPoint(_colorAreaImage).Properties.IsLeftButtonPressed)
                UpdateColorFromArea(args.GetPosition(_colorAreaImage));
        }

        private void HueBarPointerPressed(object sender, PointerPressedEventArgs args)
        {
            UpdateHueFromBar(args.GetPosition(_hueBarImage));
        }

        private void HueBarPointerMoved(object sender, PointerEventArgs args)
        {
            if (args.GetCurrentPoint(_hueBarImage).Properties.IsLeftButtonPressed)
                UpdateHueFromBar(args.GetPosition(_hueBarImage));
        }

        //color position -> saturation/brightness
        private void UpdateColorFromArea(Point point)
        {
            var x = Math.Max(0, Math.Min(PickerSize - 1, point.X));
            var y = Math.Max(0, Math.Min(PickerSize - 1, point.Y));
            _saturation = x / (PickerSize - 1);
            _brightness = 1 - y / (PickerSize - 1);
            UpdateColorAreaSelector();
            UpdateHexFromPicker();
        }

        //hue position -> hue
        private void UpdateHueFromBar(Point point)
        {
            var y = Math.Max(0, Math.Min(PickerSize - 1, point.Y));
            _hue = y / (PickerSize - 1) * 359.999;
            UpdateColorArea();
            UpdateHueBarSelector();
            UpdateHexFromPicker();
        }

        //visual pointer position in color box
        private void UpdateColorAreaSelector()
        {
            Canvas.SetLeft(_colorAreaSelector, _saturation * (PickerSize - 1) - SelectorSize / 2);
            Canvas.SetTop(_colorAreaSelector, (1 - _brightness) * (PickerSize - 1) - SelectorSize / 2);
        }
        
        //visual pointer position in hue bar
        private void UpdateHueBarSelector()
        {
            Canvas.SetLeft(_hueBarSelector, 0);
            Canvas.SetTop(_hueBarSelector, _hue / 360 * (PickerSize - 1) - HueSelectorHeight / 2);
        }

        //picker color -> hex text box
        private void UpdateHexFromPicker()
        {
            HsvToRgb(_hue, _saturation, _brightness, out var red, out var green, out var blue);
            var hex = $"{red:X2}{green:X2}{blue:X2}";

            _isSyncing = true;
            _hexTextBox.Text = hex;
            _isSyncing = false;
            UpdateValidationState(hex, true);
        }

        //validation state
        private void UpdateValidationState(string hex, bool valid)
        {
            var hasOnlyHexDigits = hex.All(IsAsciiHexDigit);
            _okButton.IsEnabled = valid;
            _colorPreview.Opacity = valid ? 1 : 0;
            _colorPreview.Background = valid ? Brush.Parse("#" + hex) : null;
            _validationText.Text = string.IsNullOrEmpty(hex) || valid
                ? string.Empty
                : hasOnlyHexDigits ? "Use 6 hex digits." : "Use only hex digits.";
        }

        private static WriteableBitmap CreateBitmap(int width, int height)
        {
            return new WriteableBitmap(
                new PixelSize(width, height),
                new Vector(96, 96),
                PixelFormat.Bgra8888,
                AlphaFormat.Premul);
        }

        //create hue bar
        private static WriteableBitmap CreateHueBar()
        {
            var bitmap = CreateBitmap(HueBarWidth, PickerSize);
            var pixels = new byte[HueBarWidth * PickerSize * 4];

            for (var y = 0; y < PickerSize; y++)
            {
                var hue = y / (PickerSize - 1d) * 359.999;
                HsvToRgb(hue, 1, 1, out var red, out var green, out var blue);

                //each row has the same color
                for (var x = 0; x < HueBarWidth; x++)
                {
                    var index = (y * HueBarWidth + x) * 4;
                    pixels[index] = blue;
                    pixels[index + 1] = green;
                    pixels[index + 2] = red;
                    pixels[index + 3] = 255;
                }
            }

            //copy pixel data to bitmap
            using (var framebuffer = bitmap.Lock())
            {
                for (var y = 0; y < PickerSize; y++)
                {
                    Marshal.Copy(
                        pixels,
                        y * HueBarWidth * 4,
                        IntPtr.Add(framebuffer.Address, y * framebuffer.RowBytes),
                        HueBarWidth * 4);
                }
            }

            return bitmap;
        }

        //update color box
        private void UpdateColorArea()
        {
            HsvToRgb(_hue, 1, 1, out var hueRed, out var hueGreen, out var hueBlue);
            
            //fill pixel array with saturation/brightness values
            for (var y = 0; y < PickerSize; y++)
            {
                var brightness = 1 - y / (PickerSize - 1d);
                for (var x = 0; x < PickerSize; x++)
                {
                    var saturation = x / (PickerSize - 1d);
                    var index = (y * PickerSize + x) * 4;
                    _colorAreaPixels[index] = (byte)Math.Round(
                        (255 + (hueBlue - 255) * saturation) * brightness);
                    _colorAreaPixels[index + 1] = (byte)Math.Round(
                        (255 + (hueGreen - 255) * saturation) * brightness);
                    _colorAreaPixels[index + 2] = (byte)Math.Round(
                        (255 + (hueRed - 255) * saturation) * brightness);
                    _colorAreaPixels[index + 3] = 255;
                }
            }

            //copy pixel data to bitmap
            using (var framebuffer = _colorAreaBitmap.Lock())
            {
                for (var y = 0; y < PickerSize; y++)
                {
                    Marshal.Copy(
                        _colorAreaPixels,
                        y * PickerSize * 4,
                        IntPtr.Add(framebuffer.Address, y * framebuffer.RowBytes),
                        PickerSize * 4);
                }
            }

            //invalidation necessary to properly update the color box
            _colorAreaImage.InvalidateVisual();
        }

        //HSV to RGB conversion
        //needed to show the final color result after the selection is made
        private static void HsvToRgb(double hue, double saturation, double value,
            out byte red, out byte green, out byte blue)
        {
            var chroma = value * saturation;
            var hueSection = hue / 60;
            var intermediate = chroma * (1 - Math.Abs(hueSection % 2 - 1));
            var offset = value - chroma;
            double redValue;
            double greenValue;
            double blueValue;

            if (hueSection < 1)
                (redValue, greenValue, blueValue) = (chroma, intermediate, 0);
            else if (hueSection < 2)
                (redValue, greenValue, blueValue) = (intermediate, chroma, 0);
            else if (hueSection < 3)
                (redValue, greenValue, blueValue) = (0, chroma, intermediate);
            else if (hueSection < 4)
                (redValue, greenValue, blueValue) = (0, intermediate, chroma);
            else if (hueSection < 5)
                (redValue, greenValue, blueValue) = (intermediate, 0, chroma);
            else
                (redValue, greenValue, blueValue) = (chroma, 0, intermediate);

            red = (byte)Math.Round((redValue + offset) * 255);
            green = (byte)Math.Round((greenValue + offset) * 255);
            blue = (byte)Math.Round((blueValue + offset) * 255);
        }

        //RGB to HSV conversion
        //needed for the selector to properly update
        private static void RgbToHsv(byte red, byte green, byte blue,
            out double hue, out double saturation, out double value)
        {
            var redValue = red / 255d;
            var greenValue = green / 255d;
            var blueValue = blue / 255d;
            var maximum = Math.Max(redValue, Math.Max(greenValue, blueValue));
            var minimum = Math.Min(redValue, Math.Min(greenValue, blueValue));
            var difference = maximum - minimum;

            if (difference == 0)
                hue = 0;
            else if (maximum == redValue)
                hue = 60 * ((greenValue - blueValue) / difference % 6);
            else if (maximum == greenValue)
                hue = 60 * ((blueValue - redValue) / difference + 2);
            else
                hue = 60 * ((redValue - greenValue) / difference + 4);

            if (hue < 0)
                hue += 360;

            saturation = maximum == 0 ? 0 : difference / maximum;
            value = maximum;
        }

        private static bool IsValidHex(string value)
        {
            return value.Length == 6 && value.All(IsAsciiHexDigit);
        }

        private static bool IsAsciiHexDigit(char value)
        {
            return (value >= '0' && value <= '9') ||
                   (value >= 'A' && value <= 'F');
        }

        private static string NormalizeHexText(string text)
        {
            var sanitized = (text ?? string.Empty).Trim();

            //accepts both #FFFFFF and FFFFFF
            //makes copying from other sources less of an hassle :D
            if (sanitized.StartsWith("#"))
                sanitized = sanitized.Substring(1);

            return sanitized.ToUpperInvariant();
        }
    }
}
