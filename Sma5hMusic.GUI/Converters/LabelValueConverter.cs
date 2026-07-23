using Avalonia.Data.Converters;
using Sma5h.Mods.Music.Helpers;
using System;
using System.Globalization;

namespace Sma5hMusic.GUI.Converters
{
    public class LabelValueConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var valueStr = value as string;
            if (string.IsNullOrEmpty(valueStr))
                return valueStr;

            valueStr = valueStr.Replace("{{", string.Empty).Replace("}}", string.Empty);
            return MsbtRichTextColorHelper.ToPlainText(MsbtRichTextColorHelper.Parse(valueStr));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
