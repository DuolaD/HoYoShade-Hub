using Microsoft.UI.Xaml.Data;
using System;


namespace HoYoShadeHub.Features.Screenshot;

public partial class ZoomFactorPercentConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is double d)
        {
            return d.ToString("P0");
        }
        if (value is float f)
        {
            return f.ToString("P0");
        }
        return "100%";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}