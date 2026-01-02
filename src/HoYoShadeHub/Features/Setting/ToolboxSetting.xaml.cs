using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using HoYoShadeHub.Features.Screenshot;
using HoYoShadeHub.Features.Toolbox;
using HoYoShadeHub.Frameworks;
using System.Collections.Generic;


namespace HoYoShadeHub.Features.Setting;

public sealed partial class ToolboxSetting : PageBase
{


    private readonly ILogger<ToolboxSetting> _logger = AppConfig.GetLogger<ToolboxSetting>();


    public ToolboxSetting()
    {
        this.InitializeComponent();
    }



    protected override void OnLoaded()
    {
        ToolboxItems =
         [
            new ToolboxItem("\xE91B",
                            null,
                            nameof(ImageViewWindow2),
                            nameof(Lang.ToolboxSetting_ImageViewer),
                            nameof(Lang.ToolboxSetting_ViewOrEditImage)),
            new ToolboxItem("\xE90F",
                            null,
                            nameof(BlenderRepairToolWindow),
                            nameof(Lang.ToolboxSetting_BlenderRepairTool),
                            nameof(Lang.ToolboxSetting_BlenderRepairToolDescription)),
        ];
    }



    protected override void OnUnloaded()
    {
        ToolboxItems = null!;
    }



    public List<ToolboxItem> ToolboxItems { get => field; set => SetProperty(ref field, value); }



    private void OnLanguageChanged(object _, LanguageChangedMessage __)
    {
        foreach (var item in ToolboxItems)
        {
            item.UpdateLanguage();
        }
    }



    private void Button_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is FrameworkElement { DataContext: ToolboxItem item })
            {
                if (item.Tag is nameof(ImageViewWindow2))
                {
                    new ImageViewWindow2().ShowWindow(XamlRoot.ContentIslandEnvironment.AppWindowId);
                }
                else if (item.Tag is nameof(BlenderRepairToolWindow))
                {
                    new BlenderRepairToolWindow().ShowWindow(XamlRoot.ContentIslandEnvironment.AppWindowId);
                }
            }
        }
        catch { }
    }



}
