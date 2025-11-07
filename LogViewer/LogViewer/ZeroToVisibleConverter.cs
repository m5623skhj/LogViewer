using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace LogViewerApp
{
    public class ZeroToVisibleConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int i)
            {
                return i == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            if (value is double d)
            {
                return Math.Abs(d) < 1e-9 ? Visibility.Visible : Visibility.Collapsed;
            }

            return Visibility.Visible;
        }

        public object ConvertBack(object? value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
