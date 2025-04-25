using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using System;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace PokerGame.Avalonia.Views
{
    public partial class GameView : UserControl
    {
        public GameView()
        {
            InitializeComponent();
            
            // Add value converters to resources
            Resources.Add("BoolToColorConverter", new BoolToColorConverter());
        }
    }
    
    /// <summary>
    /// Converts a boolean value to a color
    /// </summary>
    public class BoolToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            bool boolValue = value is bool b && b;
            
            string[] colors = parameter?.ToString()?.Split(':') ?? new[] { "Green", "Gray" };
            string trueColor = colors.Length > 0 ? colors[0] : "Green";
            string falseColor = colors.Length > 1 ? colors[1] : "Gray";
            
            return boolValue ? 
                new SolidColorBrush(Color.Parse(trueColor)) : 
                new SolidColorBrush(Color.Parse(falseColor));
        }
        
        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
