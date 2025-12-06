using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace LogViewerApp
{
    public class ZeroToVisibleConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
        {
            return value switch
            {
                int i => i == 0 ? Visibility.Visible : Visibility.Collapsed,
                double d => Math.Abs(d) < 1e-9 ? Visibility.Visible : Visibility.Collapsed,
                _ => Visibility.Visible
            };
        }

        public object ConvertBack(object? value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
