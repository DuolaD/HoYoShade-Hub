using Microsoft.UI;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using System;
using Windows.UI;

namespace HoYoShadeHub.Converters;

public partial class BoolToCheckMarkConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        // √ (checkmark) for true, × (cross) for false
        return value is true ? "√" : "×";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// 将布尔值转换为颜色（true=绿色, false=红色）
/// </summary>
public partial class BoolToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        // Green for true, Red for false
        return value is true 
            ? new SolidColorBrush(Color.FromArgb(255, 16, 185, 129)) // Green
            : new SolidColorBrush(Color.FromArgb(255, 239, 68, 68)); // Red
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
