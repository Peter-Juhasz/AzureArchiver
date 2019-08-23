using PhotoArchiver.Windows.ViewModels;
using System;
using Windows.UI.Xaml.Data;

namespace PhotoArchiver.Windows.Converters
{
    public class FileSizeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return ItemViewModel.BytesToString((long)value);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return null;
        }
    }
}
