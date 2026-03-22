using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media;

namespace HoYoShadeHub.Models;

public partial class DownloadServerItem : ObservableObject
{
    [ObservableProperty]
    private string name;

    [ObservableProperty]
    private string latencyText = "";

    [ObservableProperty]
    private Brush latencyColor;

    public int ServerIndex { get; set; }
}
