using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace PokerGame.Avalonia.Converters
{
    public class IntGreaterThanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int intValue && parameter is string strParam && int.TryParse(strParam, out int compareValue))
            {
                return intValue > compareValue;
            }
            
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}