using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace PokerGame.Avalonia.Converters
{
    public class BoolToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                if (parameter is string colorParam)
                {
                    string[] colors = colorParam.Split(':');
                    if (colors.Length == 2)
                    {
                        string colorName = boolValue ? colors[0] : colors[1];
                        return SolidColorBrush.Parse(colorName);
                    }
                }
                
                // Default colors if no parameter or invalid format
                return boolValue ? SolidColorBrush.Parse("Gold") : SolidColorBrush.Parse("Gray");
            }
            
            return SolidColorBrush.Parse("Gray");
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}