using Microsoft.UI.Xaml.Controls;
using HoYoShadeHub.RPC.HoYoShadeInstall;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace HoYoShadeHub.Features.ViewHost;

public sealed partial class ReShadeCustomSelectionDialog : ContentDialog, INotifyPropertyChanged
{
    // Main window logical size (same as MainWindow.cs CenterInScreen(1200, 676))
    private const double MainWindowLogicalWidth = 1200;
    private const double MainWindowLogicalHeight = 676;
    private const double DialogSizeRatio = 0.85;
    
    // ContentDialog has additional space for title bar (~50px) and button area (~70px)
    // We need to subtract these from the content area to achieve the desired overall dialog size
    private const double TitleBarHeight = 50;
    private const double ButtonAreaHeight = 70;

    public List<EffectPackage> EffectPackages { get; set; }
    public List<Addon> Addons { get; set; }
    public bool IsLoading { get; set; }

    private double _dialogWidth = MainWindowLogicalWidth * DialogSizeRatio;
    private double _dialogHeight = MainWindowLogicalHeight * DialogSizeRatio - TitleBarHeight - ButtonAreaHeight;

    public double DialogWidth
    {
        get => _dialogWidth;
        set
        {
            if (_dialogWidth != value)
            {
                _dialogWidth = value;
                OnPropertyChanged();
            }
        }
    }

    public double DialogHeight
    {
        get => _dialogHeight;
        set
        {
            if (_dialogHeight != value)
            {
                _dialogHeight = value;
                OnPropertyChanged();
            }
        }
    }

    public ContentDialogResult DialogResult { get; private set; } = ContentDialogResult.None;

    public event PropertyChangedEventHandler PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public ReShadeCustomSelectionDialog(List<EffectPackage> effectPackages, List<Addon> addons)
    {
        this.InitializeComponent();
        EffectPackages = effectPackages;
        Addons = addons;
        
        // Set title dynamically using ResourceManager with string literal key
        this.Title = Lang.ResourceManager.GetString("ReShadeDownloadView_CustomizeInstallDialogTitle") 
                     ?? "自定义 ReShade 着色器和插件";
    }

    private void ContentDialog_Loaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        UpdateDialogSize();
    }

    private void UpdateDialogSize()
    {
        // Get the main window's actual size (already includes DPI scaling)
        var mainWindow = MainWindow.Current;
        if (mainWindow?.AppWindow != null)
        {
            // Get UI scale factor
            double uiScale = mainWindow.UIScale;

            // Main window's actual pixel size
            int windowPixelWidth = mainWindow.AppWindow.Size.Width;
            int windowPixelHeight = mainWindow.AppWindow.Size.Height;

            // Convert to logical size
            double windowLogicalWidth = windowPixelWidth / uiScale;
            double windowLogicalHeight = windowPixelHeight / uiScale;

            // Calculate dialog's logical size (0.85x of main window)
            // Subtract title bar and button area height from content area
            // so the overall dialog size matches the 0.85 ratio
            DialogWidth = windowLogicalWidth * DialogSizeRatio;
            DialogHeight = windowLogicalHeight * DialogSizeRatio - TitleBarHeight - ButtonAreaHeight;
        }
        else
        {
            // If main window is not available, use default values
            DialogWidth = MainWindowLogicalWidth * DialogSizeRatio;
            DialogHeight = MainWindowLogicalHeight * DialogSizeRatio - TitleBarHeight - ButtonAreaHeight;
        }
    }

    public List<string> GetSelectedPackages()
    {
        var selected = new List<string>();
        if (EffectPackages != null)
        {
            selected.AddRange(EffectPackages.Where(x => x.Selected == true).Select(x => x.Name));
        }
        if (Addons != null)
        {
            selected.AddRange(Addons.Where(x => x.Selected).Select(x => x.Name));
        }
        return selected;
    }

    private void OnConfirmClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        DialogResult = ContentDialogResult.Primary;
        Hide();
    }

    private void OnCancelClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        DialogResult = ContentDialogResult.Secondary;
        Hide();
    }
}
