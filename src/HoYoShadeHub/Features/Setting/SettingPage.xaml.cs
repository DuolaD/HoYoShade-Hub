using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml.Controls;
using HoYoShadeHub.Frameworks;
using HoYoShadeHub.Language;
using System;
using System.Globalization;


namespace HoYoShadeHub.Features.Setting;

public sealed partial class SettingPage : PageBase
{


    private readonly ILogger<SettingPage> _logger = AppConfig.GetLogger<SettingPage>();


    public SettingPage()
    {
        this.InitializeComponent();
        Frame_Setting.Navigate(typeof(AboutSetting));
        WeakReferenceMessenger.Default.Register<LanguageChangedMessage>(this, (_, _) => OnLanguageChanged());
    }


    private void OnLanguageChanged()
    {
        // Ensure Lang uses the current culture
        Lang.Culture = CultureInfo.CurrentUICulture;
        
        // Update bindings
        this.Bindings.Update();
        
        // Manually update NavigationView menu items
        UpdateNavigationViewMenuItems();
    }


    private void UpdateNavigationViewMenuItems()
    {
        try
        {
            if (SettingPage_NavigationView?.MenuItems == null)
            {
                return;
            }

            foreach (var item in SettingPage_NavigationView.MenuItems)
            {
                if (item is NavigationViewItem navItem)
                {
                    // Update Content based on Tag
                    navItem.Content = navItem.Tag switch
                    {
                        nameof(AboutSetting) => Lang.SettingPage_About,
                        nameof(GeneralSetting) => Lang.SettingPage_General,
                        nameof(FileManageSetting) => Lang.SettingPage_FileManagement,
                        nameof(HotkeySetting) => Lang.SettingPage_KeyboardShortcuts,
                        nameof(AdvancedSetting) => Lang.SettingPage_Advanced,
                        nameof(ToolboxSetting) => Lang.SettingPage_Toolbox,
                        _ => navItem.Content,
                    };
                }
            }

            // Update PaneHeader text
            if (SettingPage_NavigationView.PaneHeader is Microsoft.UI.Xaml.Controls.TextBlock headerTextBlock)
            {
                headerTextBlock.Text = Lang.SettingPage_AppSettings;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Update navigation view menu items");
        }
    }



    private void NavigationView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        try
        {
            Type? type = args.InvokedItemContainer?.Tag switch
            {
                nameof(AboutSetting) => typeof(AboutSetting),
                nameof(GeneralSetting) => typeof(GeneralSetting),
                nameof(FileManageSetting) => typeof(FileManageSetting),
                nameof(AdvancedSetting) => typeof(AdvancedSetting),
                nameof(ToolboxSetting) => typeof(ToolboxSetting),
                nameof(HotkeySetting) => typeof(HotkeySetting),
                _ => null,
            };
            if (type is not null)
            {
                Frame_Setting.Navigate(type);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Setting page navigate.");
        }
    }



    protected override void OnUnloaded()
    {
        WeakReferenceMessenger.Default.UnregisterAll(this);
    }



}
