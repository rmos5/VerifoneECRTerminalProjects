using System;
using System.Globalization;
using System.Windows.Data;
using Verifone.ECRTerminal;

namespace VerifonePaymentTerminal
{
    public class TransactionTypeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return TransactionTypes.GetTypeString(value as string);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
