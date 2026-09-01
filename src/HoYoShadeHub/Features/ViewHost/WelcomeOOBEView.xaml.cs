using System;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using HoYoShadeHub.Language;

namespace HoYoShadeHub.Features.ViewHost;

public sealed partial class WelcomeOOBEView : UserControl
{
    private readonly CancellationTokenSource _cts = new();
    private bool _isCompleted;

    public event Action? Completed;

    public WelcomeOOBEView()
    {
        this.InitializeComponent();
    }

    private async void Grid_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            var phrases = new[]
            {
                Lang.WelcomeOOBE_Greeting1,
                Lang.WelcomeOOBE_Greeting2,
                Lang.WelcomeOOBE_Greeting3,
            };

            var visual = ElementCompositionPreview.GetElementVisual(GreetingTextBlock);
            var compositor = visual.Compositor;

            // Initial state: hide visual before entering animation starts
            visual.Opacity = 0f;

            // Wait for view transition into OOBE screen to fully settle
            await Task.Delay(400, _cts.Token);

            var cubicEasing = compositor.CreateCubicBezierEasingFunction(new Vector2(0.1f, 0.9f), new Vector2(0.2f, 1.0f));

            foreach (var phrase in phrases)
            {
                if (_cts.IsCancellationRequested || _isCompleted) break;

                GreetingTextBlock.Text = phrase;

                // Step A: Float up + Fade In (Y: 20 -> 0, Opacity: 0 -> 1)
                visual.Offset = new Vector3(0f, 20f, 0f);
                visual.Opacity = 0f;

                var inOffsetAnim = compositor.CreateVector3KeyFrameAnimation();
                inOffsetAnim.Duration = TimeSpan.FromMilliseconds(600);
                inOffsetAnim.InsertKeyFrame(1.0f, Vector3.Zero, cubicEasing);

                var inOpacityAnim = compositor.CreateScalarKeyFrameAnimation();
                inOpacityAnim.Duration = TimeSpan.FromMilliseconds(550);
                inOpacityAnim.InsertKeyFrame(1.0f, 1.0f, cubicEasing);

                visual.StartAnimation(nameof(Visual.Offset), inOffsetAnim);
                visual.StartAnimation(nameof(Visual.Opacity), inOpacityAnim);

                // Hold for 2.0 seconds as requested
                await Task.Delay(2000, _cts.Token);
                if (_cts.IsCancellationRequested || _isCompleted) break;

                // Step B: Float up + Fade Out (Y: 0 -> -20, Opacity: 1 -> 0)
                var outOffsetAnim = compositor.CreateVector3KeyFrameAnimation();
                outOffsetAnim.Duration = TimeSpan.FromMilliseconds(500);
                outOffsetAnim.InsertKeyFrame(1.0f, new Vector3(0f, -20f, 0f), cubicEasing);

                var outOpacityAnim = compositor.CreateScalarKeyFrameAnimation();
                outOpacityAnim.Duration = TimeSpan.FromMilliseconds(450);
                outOpacityAnim.InsertKeyFrame(1.0f, 0f, cubicEasing);

                visual.StartAnimation(nameof(Visual.Offset), outOffsetAnim);
                visual.StartAnimation(nameof(Visual.Opacity), outOpacityAnim);

                await Task.Delay(500, _cts.Token);
            }

            if (!_isCompleted && !_cts.IsCancellationRequested)
            {
                TriggerComplete();
            }
        }
        catch (TaskCanceledException)
        {
            // Skipped by user
        }
        catch (Exception)
        {
            TriggerComplete();
        }
    }

    private void TriggerComplete()
    {
        if (_isCompleted) return;
        _isCompleted = true;
        _cts.Cancel();
        this.DispatcherQueue.TryEnqueue(() => Completed?.Invoke());
    }

    private void Grid_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        TriggerComplete();
    }

    private void Grid_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        TriggerComplete();
    }
}
