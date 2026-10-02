using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace HoYoShadeHub.Features.Setting;

[INotifyPropertyChanged]
public sealed partial class CustomInjectDialog : ContentDialog
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasProcessName))]
    private string _processName = "";

    public bool HasProcessName => !string.IsNullOrWhiteSpace(ProcessName);

    public ContentDialogResult Result { get; private set; } = ContentDialogResult.None;

    public CustomInjectDialog()
    {
        this.InitializeComponent();
    }

    private void OnInjectClick(object sender, RoutedEventArgs e)
    {
        if (!HasProcessName)
        {
            return;
        }
        Result = ContentDialogResult.Primary;
        Hide();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        Result = ContentDialogResult.Secondary;
        Hide();
    }
}
