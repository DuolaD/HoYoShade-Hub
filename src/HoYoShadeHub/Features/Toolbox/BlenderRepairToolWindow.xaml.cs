using Microsoft.Extensions.Logging;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using HoYoShadeHub.Frameworks;
using System;
using Vanara.PInvoke;
using Windows.UI;

namespace HoYoShadeHub.Features.Toolbox;

public sealed partial class BlenderRepairToolWindow : WindowEx
{
    private readonly ILogger<BlenderRepairToolWindow> _logger = AppConfig.GetLogger<BlenderRepairToolWindow>();

    public BlenderRepairToolWindow()
    {
        InitializeComponent();
        InitializeWindow();
    }

    private void InitializeWindow()
    {
        Title = "Blender Repair Tool";
        AppWindow.TitleBar.ExtendsContentIntoTitleBar = true;
        AppWindow.TitleBar.IconShowOptions = IconShowOptions.ShowIconAndSystemMenu;
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        AdaptTitleBarButtonColorToActuallTheme();
        SetIcon();
        
        // Set window size
        AppWindow.Resize(new Windows.Graphics.SizeInt32(800, 600));
    }

    private void RootGrid_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            double uiScale = Content.XamlRoot?.RasterizationScale ?? User32.GetDpiForWindow(WindowHandle) / 96.0;
            double x = CustomTitleBar.ActualWidth * uiScale;
            SetDragRectangles(new Windows.Graphics.RectInt32((int)x, 0, 0, (int)(48 * uiScale)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set drag rectangles");
        }
    }

    public void ShowWindow(Microsoft.UI.WindowId windowId)
    {
        try
        {
            CenterInScreen(800, 600);
            Show();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to show window");
        }
    }

    private void Button_Placeholder_Click(object sender, RoutedEventArgs e)
    {
        // Placeholder for future functionality
        TextBlock_Status.Text = "Under development...";
    }
}
