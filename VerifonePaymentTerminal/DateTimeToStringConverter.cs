using System;
using System.Globalization;
using System.Windows.Data;

namespace VerifonePaymentTerminal
{
    public class DateTimeToStringConverter : IValueConverter
    {
        public string Format { get; set; } = "dd.MM.yyyy HH:mm";

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is DateTime dt)
                return dt.ToString(Format, culture);
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (DateTime.TryParse(value as string, culture, DateTimeStyles.None, out var dt))
                return dt;
            return Binding.DoNothing;
        }
    }
}
