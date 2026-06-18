using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Sma5h.Mods.Music.Helpers;

namespace Sma5hMusic.GUI.Controls
{
    public class ColoredLabelTextBlock : StackPanel
    {
        public static readonly StyledProperty<string> TextProperty =
            AvaloniaProperty.Register<ColoredLabelTextBlock, string>(nameof(Text));

        public string Text
        {
            get => GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public ColoredLabelTextBlock()
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal;
        }

        protected override void OnPropertyChanged<T>(AvaloniaPropertyChangedEventArgs<T> change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == TextProperty)
                UpdateText();
        }

        private void UpdateText()
        {
            Children.Clear();
            var text = Text;
            if (string.IsNullOrEmpty(text))
                return;

            text = text.Replace("{{", string.Empty).Replace("}}", string.Empty);
            foreach (var span in MsbtRichTextColorHelper.Parse(text))
            {
                var color = MsbtRichTextColorHelper.GetColor(span.ColorId) ?? MsbtRichTextColorHelper.DefaultColor;
                var textBlock = new TextBlock
                {
                    Text = span.Text
                };
                if (!color.IsDefault)
                    textBlock.Foreground = Brush.Parse(color.Hex);

                Children.Add(textBlock);
            }
        }
    }
}
