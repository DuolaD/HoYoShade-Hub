using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using System.Threading.Tasks;
using System;
using HoYoShadeHub.Language;

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
            Content = Lang.PresetsDialog_KeepExisting,
            Tag = PresetsHandlingOption.KeepExisting,
            Margin = new Thickness(0, 8, 0, 0)
        };
        ToolTipService.SetToolTip(radioKeepExisting, Lang.PresetsDialog_KeepExisting_Tooltip);

        var radioOverwrite = new RadioButton
        {
            Content = Lang.PresetsDialog_Overwrite,
            Tag = PresetsHandlingOption.Overwrite,
            Margin = new Thickness(0, 8, 0, 0)
        };
        ToolTipService.SetToolTip(radioOverwrite, Lang.PresetsDialog_Overwrite_Tooltip);

        var radioSeparateFolder = new RadioButton
        {
            Content = Lang.PresetsDialog_SeparateFolder,
            Tag = PresetsHandlingOption.SeparateFolder,
            IsChecked = true,
            Margin = new Thickness(0, 8, 0, 0)
        };
        ToolTipService.SetToolTip(radioSeparateFolder, Lang.PresetsDialog_SeparateFolder_Tooltip);

        var stackPanel = new StackPanel
        {
            Spacing = 8
        };

        var description = new TextBlock
        {
            Text = Lang.PresetsDialog_Description,
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
            Title = Lang.PresetsDialog_Title,
            // Content set later
        };

        var btnCancel = new Button
        {
            Content = Lang.Common_Cancel,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        
        var btnContinue = new Button
        {
            Content = Lang.Common_Confirm,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Style = (Style)Application.Current.Resources["AccentButtonStyle"]
        };

        bool isConfirmed = false;
        btnCancel.Click += (s, e) => dialog.Hide();
        btnContinue.Click += (s, e) => 
        {
            isConfirmed = true;
            dialog.Hide();
        };

        var buttonGrid = new Grid
        {
            Margin = new Thickness(0, 24, 0, 0),
            ColumnSpacing = 12
        };
        buttonGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        buttonGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        Grid.SetColumn(btnCancel, 0);
        buttonGrid.Children.Add(btnCancel);
        Grid.SetColumn(btnContinue, 1);
        buttonGrid.Children.Add(btnContinue);

        stackPanel.Children.Add(buttonGrid);
        dialog.Content = stackPanel;

        await dialog.ShowAsync();

        if (!isConfirmed)
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
