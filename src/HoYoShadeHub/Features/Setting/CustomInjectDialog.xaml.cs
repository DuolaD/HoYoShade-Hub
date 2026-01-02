using Microsoft.UI.Xaml.Controls;

namespace HoYoShadeHub.Features.Setting;

public sealed partial class CustomInjectDialog : ContentDialog
{
    public string ProcessName { get; set; } = "";

    public CustomInjectDialog()
    {
        this.InitializeComponent();
    }
}
