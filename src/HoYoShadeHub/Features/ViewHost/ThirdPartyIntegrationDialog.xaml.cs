using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using HoYoShadeHub.Core;
using HoYoShadeHub.Core.HoYoPlay;
using HoYoShadeHub.Features.GameLauncher;
using HoYoShadeHub.Frameworks;
using HoYoShadeHub.Helpers;
using System;
using System.IO;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;

namespace HoYoShadeHub.Features.ViewHost;

public sealed partial class ThirdPartyIntegrationDialog : ContentDialog
{
    private readonly ILogger<ThirdPartyIntegrationDialog> _logger = AppConfig.GetLogger<ThirdPartyIntegrationDialog>();

    public GameId CurrentGameId { get; set; }

    public ThirdPartyIntegrationDialog()
    {
        this.InitializeComponent();
        this.Loaded += ThirdPartyIntegrationDialog_Loaded;
    }

    private async void ThirdPartyIntegrationDialog_Loaded(object sender, RoutedEventArgs e)
    {
        await InitializeCommandsAsync();
    }

    private async Task InitializeCommandsAsync()
    {
        try
        {
            bool hoYoShadeInstalled = false;
            bool openHoYoShadeInstalled = false;
            string hoYoShadeCommand = "";
            string openHoYoShadeCommand = "";

            // Get game exe name
            var gameLauncherService = AppConfig.GetService<GameLauncherService>();
            string gameExeName = await gameLauncherService.GetGameExeNameAsync(CurrentGameId);

            // Check HoYoShade installation
            string hoYoShadePath = Path.Combine(AppConfig.UserDataFolder, "HoYoShade");
            if (Directory.Exists(hoYoShadePath))
            {
                string injectExePath = Path.Combine(hoYoShadePath, "inject.exe");
                if (File.Exists(injectExePath))
                {
                    hoYoShadeInstalled = true;
                    hoYoShadeCommand = $"\"{injectExePath}\" {gameExeName}";
                }
            }

            // Check OpenHoYoShade installation
            string openHoYoShadePath = Path.Combine(AppConfig.UserDataFolder, "OpenHoYoShade");
            if (Directory.Exists(openHoYoShadePath))
            {
                string injectExePath = Path.Combine(openHoYoShadePath, "inject.exe");
                if (File.Exists(injectExePath))
                {
                    openHoYoShadeInstalled = true;
                    openHoYoShadeCommand = $"\"{injectExePath}\" {gameExeName}";
                }
            }

            // Update UI based on installation status
            if (!hoYoShadeInstalled && !openHoYoShadeInstalled)
            {
                // No framework installed - show warning
                InfoBar_NoFrameworkInstalled.IsOpen = true;
                StackPanel_HoYoShade.Visibility = Visibility.Collapsed;
                StackPanel_OpenHoYoShade.Visibility = Visibility.Collapsed;
            }
            else
            {
                // At least one framework installed
                InfoBar_NoFrameworkInstalled.IsOpen = false;

                if (hoYoShadeInstalled)
                {
                    StackPanel_HoYoShade.Visibility = Visibility.Visible;
                    TextBox_HoYoShadeCommand.Text = hoYoShadeCommand;
                }
                else
                {
                    StackPanel_HoYoShade.Visibility = Visibility.Collapsed;
                }

                if (openHoYoShadeInstalled)
                {
                    StackPanel_OpenHoYoShade.Visibility = Visibility.Visible;
                    TextBox_OpenHoYoShadeCommand.Text = openHoYoShadeCommand;
                }
                else
                {
                    StackPanel_OpenHoYoShade.Visibility = Visibility.Collapsed;
                }
            }

            _logger.LogInformation("Third-party integration commands initialized. HoYoShade: {HoYoShade}, OpenHoYoShade: {OpenHoYoShade}",
                hoYoShadeInstalled, openHoYoShadeInstalled);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize third-party integration commands");
            InfoBar_NoFrameworkInstalled.IsOpen = true;
            InfoBar_NoFrameworkInstalled.Message = $"≥ı ºªØ ß∞‹£∫{ex.Message}";
            StackPanel_HoYoShade.Visibility = Visibility.Collapsed;
            StackPanel_OpenHoYoShade.Visibility = Visibility.Collapsed;
        }
    }

    private void Button_CopyHoYoShadeCommand_Click(object sender, RoutedEventArgs e)
    {
        CopyToClipboard(TextBox_HoYoShadeCommand.Text, "HoYoShade");
    }

    private void Button_CopyOpenHoYoShadeCommand_Click(object sender, RoutedEventArgs e)
    {
        CopyToClipboard(TextBox_OpenHoYoShadeCommand.Text, "OpenHoYoShade");
    }

    private void CopyToClipboard(string text, string frameworkName)
    {
        try
        {
            var dataPackage = new DataPackage();
            dataPackage.SetText(text);
            Clipboard.SetContent(dataPackage);

            InAppToast.MainWindow?.Success($"{frameworkName}√¸¡Ó“—∏¥÷∆µΩºÙÃ˘∞Â");
            _logger.LogInformation("Copied {FrameworkName} command to clipboard", frameworkName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to copy {FrameworkName} command to clipboard", frameworkName);
            InAppToast.MainWindow?.Error($"∏¥÷∆ ß∞‹£∫{ex.Message}");
        }
    }
}
