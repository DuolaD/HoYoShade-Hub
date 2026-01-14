using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using System.Threading.Tasks;
using System;

namespace HoYoShadeHub.Features.ViewHost;

public enum PresetsHandlingOption
{
    KeepExisting = 1,
    Overwrite = 0,
    SeparateFolder = 2
}

public static class PresetsHandlingDialog
{
    public static async Task<(bool cancelled, PresetsHandlingOption option)> ShowAsync(XamlRoot xamlRoot)
    {
        var radioKeepExisting = new RadioButton
        {
            Content = "不更新预设文件（不推荐）",
            Tag = PresetsHandlingOption.KeepExisting,
            Margin = new Thickness(0, 8, 0, 0)
        };
        ToolTipService.SetToolTip(radioKeepExisting, "保留现有的预设文件，不安装新版本的预设文件。可能导致新功能无法使用。");

        var radioOverwrite = new RadioButton
        {
            Content = "覆盖（不推荐）",
            Tag = PresetsHandlingOption.Overwrite,
            Margin = new Thickness(0, 8, 0, 0)
        };
        ToolTipService.SetToolTip(radioOverwrite, "用新版本的预设文件完全覆盖现有预设。你的自定义预设和修改将会丢失。");

        var radioSeparateFolder = new RadioButton
        {
            Content = "单独放置新版本预设文件（推荐）",
            Tag = PresetsHandlingOption.SeparateFolder,
            IsChecked = true,
            Margin = new Thickness(0, 8, 0, 0)
        };
        ToolTipService.SetToolTip(radioSeparateFolder, "将新版本的预设文件放入以版本号命名的独立文件夹中，保留原有预设。这是最安全的选项。");

        var stackPanel = new StackPanel
        {
            Spacing = 8
        };

        var description = new TextBlock
        {
            Text = "检测到你正在更新HoYoShade框架，新版本包含更新的预设文件。请选择如何处理现有的预设：",
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.8,
            Margin = new Thickness(0, 0, 0, 16)
        };

        stackPanel.Children.Add(description);
        stackPanel.Children.Add(radioKeepExisting);
        stackPanel.Children.Add(radioOverwrite);
        stackPanel.Children.Add(radioSeparateFolder);

        var dialog = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = "你希望如何处理现有的预设文件？",
            Content = stackPanel,
            PrimaryButtonText = "继续",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Primary
        };

        var result = await dialog.ShowAsync();

        if (result != ContentDialogResult.Primary)
        {
            return (true, PresetsHandlingOption.SeparateFolder);
        }

        // Find which radio button is checked
        PresetsHandlingOption selectedOption = PresetsHandlingOption.SeparateFolder;
        if (radioKeepExisting.IsChecked == true)
            selectedOption = PresetsHandlingOption.KeepExisting;
        else if (radioOverwrite.IsChecked == true)
            selectedOption = PresetsHandlingOption.Overwrite;
        else if (radioSeparateFolder.IsChecked == true)
            selectedOption = PresetsHandlingOption.SeparateFolder;

        return (false, selectedOption);
    }
}
