using System;
using Windows.UI.Xaml.Data;

namespace PhotoArchiver.Windows.Converters
{
    public class NullableIntoToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return ((int?)value)?.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            if (value as string == null)
                return null;

            return Int32.Parse(value as string);
        }
    }
}
