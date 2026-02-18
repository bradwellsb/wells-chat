using System.Globalization;

namespace CohoistChat.Maui
{
    //This converter is no longer used, because DateTime is only stored as string in transit, then converted to datetime on each end
    public class StringToDateTimeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string stringValue)
            {
                _ = DateTime.TryParse(stringValue, out DateTime dateTimeValue);
                return dateTimeValue;
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            //if (value is DateTime dateTimeValue)
            //{
            //    return dateTimeValue.ToString();
            //}
            //return value;
            return null; //Because this will only be used in one-way bindings
        }
    }
}