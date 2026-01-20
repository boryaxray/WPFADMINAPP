using System;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;

namespace WPFAPP.Converters
{
    public class IndexConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (value is int alternationIndex)
                {
                    return (alternationIndex + 1).ToString();
                }

                // Дополнительная проверка
                if (value is ListViewItem item)
                {
                    var listView = ItemsControl.ItemsControlFromItemContainer(item) as ListView;
                    if (listView != null)
                    {
                        int index = listView.ItemContainerGenerator.IndexFromContainer(item);
                        if (index >= 0)
                        {
                            return (index + 1).ToString();
                        }
                    }
                }
            }
            catch { }

            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}