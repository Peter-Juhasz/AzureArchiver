using System;
using Windows.UI.Xaml.Data;

namespace PhotoArchiver.Windows.Converters
{
    public class LongToDoubleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return (double)(long)value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return (long)(double)value;
        }
    }
}
