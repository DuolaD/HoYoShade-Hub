using Microsoft.UI;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using System;
using Windows.UI;

namespace HoYoShadeHub.Converters;

/// <summary>
/// Convert bool (installed status) to glyph icon
/// </summary>
public partial class BoolToInstallGlyphConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        // ? (checkmark) for installed, ? (cross) for not installed
        return value is true ? "\uE73E" : "\uE711";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Convert bool (installed status) to localized text
/// </summary>
public partial class BoolToInstallStatusTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value is true ? HoYoShadeHub.Language.Lang.WelcomeView_Installed : HoYoShadeHub.Language.Lang.WelcomeView_NotInstalled;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Convert bool (installed status) to color
/// </summary>
public partial class BoolToInstallColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        // Green for installed, Gray for not installed
        return value is true 
            ? new SolidColorBrush(Color.FromArgb(255, 16, 185, 129)) // Green
            : new SolidColorBrush(Color.FromArgb(255, 156, 163, 175)); // Gray
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
