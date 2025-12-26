using Microsoft.UI.Xaml.Data;
using System;

namespace HoYoShadeHub.Converters;

internal partial class BoolReversedConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return !(bool)value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
