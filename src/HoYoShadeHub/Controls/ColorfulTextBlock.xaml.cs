using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using System;
using System.Diagnostics;
using Windows.UI;


namespace HoYoShadeHub.Controls;

[INotifyPropertyChanged]
public sealed partial class ColorfulTextBlock : UserControl
{


    public ColorfulTextBlock()
    {
        this.InitializeComponent();
    }




    public TextWrapping TextWrapping
    {
        get { return (TextWrapping)GetValue(TextWrappingProperty); }
        set { SetValue(TextWrappingProperty, value); }
    }

    // Using a DependencyProperty as the backing store for TextWrapping.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty TextWrappingProperty =
        DependencyProperty.Register("TextWrapping", typeof(TextWrapping), typeof(ColorfulTextBlock), new PropertyMetadata(default));



    [ObservableProperty]
    public partial string Text { get; set; }
    partial void OnTextChanged(string value)
    {
        try
        {
            var text = ThisTextBlock;
            text.Inlines.Clear();
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }
            var desc = value.AsSpan();
            int lastIndex = 0;
            for (int i = 0; i < desc.Length; i++)
            {
                // 换行
                if (i + 1 < desc.Length && desc[i] == '\\' && desc[i + 1] == 'n')
                {
                    if (i > lastIndex)
                    {
                        text.Inlines.Add(new Run { Text = desc[lastIndex..i].ToString() });
                    }
                    text.Inlines.Add(new LineBreak());
                    i += 1;
                    lastIndex = i + 1;
                    continue;
                }
                // 颜色: <color=#RRGGBB>...</color> 或 <color=#AARRGGBB>...</color>
                if (desc[i..].StartsWith("<color=#", StringComparison.OrdinalIgnoreCase))
                {
                    var afterTag = desc[(i + 8)..];
                    var colorLength = afterTag.IndexOf('>');
                    if (colorLength is 6 or 8)
                    {
                        var colorString = afterTag[..colorLength];
                        if (IsHexColor(colorString))
                        {
                            var afterColor = desc[(i + 9 + colorLength)..];
                            var textLength = afterColor.IndexOf('<');
                            if (textLength >= 0 && afterColor[textLength..].StartsWith("</color>", StringComparison.OrdinalIgnoreCase))
                            {
                                if (i > lastIndex)
                                {
                                    text.Inlines.Add(new Run { Text = desc[lastIndex..i].ToString() });
                                }

                                var color = Convert.FromHexString(colorString);
                                if (colorLength == 8)
                                {
                                    text.Inlines.Add(new Run
                                    {
                                        Text = afterColor[..textLength].ToString(),
                                        Foreground = new SolidColorBrush(Color.FromArgb(color[3], color[0], color[1], color[2])),
                                    });
                                }
                                else
                                {
                                    text.Inlines.Add(new Run
                                    {
                                        Text = afterColor[..textLength].ToString(),
                                        Foreground = new SolidColorBrush(Color.FromArgb(0xFF, color[0], color[1], color[2])),
                                    });
                                }

                                i += 16 + colorLength + textLength;
                                lastIndex = i + 1;
                                continue;
                            }
                        }
                    }
                }
                // 引用 (斜体): <i>...</i>
                if (desc[i..].StartsWith("<i>", StringComparison.OrdinalIgnoreCase))
                {
                    var afterItalic = desc[(i + 3)..];
                    var length = afterItalic.IndexOf("</i>", StringComparison.OrdinalIgnoreCase);
                    if (length >= 0)
                    {
                        if (i > lastIndex)
                        {
                            text.Inlines.Add(new Run { Text = desc[lastIndex..i].ToString() });
                        }
                        text.Inlines.Add(new Run
                        {
                            Text = afterItalic[..length].ToString(),
                            FontStyle = Windows.UI.Text.FontStyle.Italic,
                        });
                        i += length + 6;
                        lastIndex = i + 1;
                        continue;
                    }
                }
            }
            if (lastIndex < desc.Length)
            {
                text.Inlines.Add(new Run { Text = desc[lastIndex..].ToString() });
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            ThisTextBlock.Inlines.Clear();
            ThisTextBlock.Text = value;
        }
    }

    private static bool IsHexColor(ReadOnlySpan<char> span)
    {
        if (span.Length is not (6 or 8))
        {
            return false;
        }

        for (int i = 0; i < span.Length; i++)
        {
            char c = span[i];
            if (!char.IsAsciiHexDigit(c))
            {
                return false;
            }
        }
        return true;
    }

}
