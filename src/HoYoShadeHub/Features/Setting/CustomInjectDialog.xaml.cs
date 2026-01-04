using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace HoYoShadeHub.Features.Setting;

public sealed partial class CustomInjectDialog : ContentDialog
{
    public string ProcessName { get; set; } = "";

    public ContentDialogResult Result { get; private set; } = ContentDialogResult.None;

    public CustomInjectDialog()
    {
        this.InitializeComponent();
    }

    private void OnInjectClick(object sender, RoutedEventArgs e)
    {
        Result = ContentDialogResult.Primary;
        Hide();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        Result = ContentDialogResult.Secondary;
        Hide();
    }
}
