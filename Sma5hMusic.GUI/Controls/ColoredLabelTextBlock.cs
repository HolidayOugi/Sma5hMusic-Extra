using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Sma5h.Mods.Music.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Sma5hMusic.GUI.Controls
{
    public class ColoredLabelTextBlock : StackPanel
    {
        private const double SmallTextScale = 0.8;

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

            var chars = ToColoredCharacters(MsbtRichTextColorHelper.Parse(text));
            for (var i = 0; i < chars.Count;)
            {
                if (IsSmallTextStart(chars, i))
                {
                    var end = FindSmallTextEnd(chars, i + 2);
                    if (end >= 0)
                    {
                        AddText(chars.Skip(i + 2).Take(end - i - 2), true);
                        i = end + 2;
                        continue;
                    }
                }

                var nextMarker = FindNextSmallTextStart(chars, i + 1);
                var takeCount = (nextMarker >= 0 ? nextMarker : chars.Count) - i;
                AddText(chars.Skip(i).Take(takeCount), false);
                i += takeCount;
            }
        }

        private void AddText(IEnumerable<ColoredCharacter> characters, bool isSmallText)
        {
            var activeColorId = (string)null;
            var text = string.Empty;
            foreach (var character in characters)
            {
                if (!string.Equals(activeColorId, character.ColorId, StringComparison.OrdinalIgnoreCase))
                {
                    AddTextBlock(text, activeColorId, isSmallText);
                    text = string.Empty;
                    activeColorId = character.ColorId;
                }

                text += character.Value;
            }

            AddTextBlock(text, activeColorId, isSmallText);
        }

        private void AddTextBlock(string text, string colorId, bool isSmallText)
        {
            if (string.IsNullOrEmpty(text))
                return;

            var color = MsbtRichTextColorHelper.GetColor(colorId) ?? MsbtRichTextColorHelper.DefaultColor;
            var textBlock = new TextBlock
            {
                Text = text,
                VerticalAlignment = VerticalAlignment.Bottom
            };
            if (isSmallText)
            {
                textBlock.RenderTransformOrigin = new RelativePoint(0, 1, RelativeUnit.Relative);
                textBlock.RenderTransform = new ScaleTransform(SmallTextScale, SmallTextScale);
            }
            if (!color.IsDefault)
                textBlock.Foreground = Brush.Parse(color.Hex);

            Children.Add(textBlock);
        }

        private static List<ColoredCharacter> ToColoredCharacters(IEnumerable<MsbtRichTextSpan> spans)
        {
            return spans
                .SelectMany(span => span.Text.Select(character => new ColoredCharacter(character, span.ColorId)))
                .ToList();
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
                ColorId = colorId;
            }

            public char Value { get; }
            public string ColorId { get; }
        }
    }
}
