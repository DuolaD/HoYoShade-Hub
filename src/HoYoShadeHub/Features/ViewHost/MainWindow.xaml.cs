using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI.Composition;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.Windows.AppLifecycle;
using HoYoShadeHub.Features.Background;
using HoYoShadeHub.Features.Database;
using HoYoShadeHub.Features.GameLauncher;
using HoYoShadeHub.Features.Overlay;
using HoYoShadeHub.Features.Screenshot;
using HoYoShadeHub.Frameworks;
using System;
using System.ComponentModel;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Vanara.PInvoke;
using Windows.Graphics;


namespace HoYoShadeHub.Features.ViewHost;

public enum ViewTransitionType
{
    None,
    SlideFromRight,
    DrillIn,
}

[INotifyPropertyChanged]
public sealed partial class MainWindow : WindowEx
{


    public static new MainWindow Current { get; private set; }


    private bool _mainViewLoaded;
    private ContentControl _currentPresenter = null!;
    private ContentControl _nextPresenter = null!;
    private CompositionScopedBatch? _activeTransitionBatch;


    public MainWindow()
    {
        Current = this;
        MainWindowId = AppWindow.Id;
        this.InitializeComponent();
        _currentPresenter = CurrentContentPresenter;
        _nextPresenter = NextContentPresenter;
        InitializeMainWindow();
        LoadContentView();
        WeakReferenceMessenger.Default.Register<AccentColorChangedMessage>(this, OnAccentColorChanged);
        WeakReferenceMessenger.Default.Register<WelcomePageFinishedMessage>(this, OnWelcomePageFinished);
        WeakReferenceMessenger.Default.Register<NavigateToQuickSetupPageMessage>(this, OnNavigateToQuickSetupPage);
        WeakReferenceMessenger.Default.Register<NavigateToDownloadPageMessage>(this, OnNavigateToDownloadPage);
        WeakReferenceMessenger.Default.Register<NavigateToReShadeDownloadPageMessage>(this, OnNavigateToReShadeDownloadPage);
        WeakReferenceMessenger.Default.Register<GameStartedMessage>(this, OnGameStarted);
    }



    private void InitializeMainWindow()
    {
        Title = "HoYoShade Hub";
        AppWindow.TitleBar.ExtendsContentIntoTitleBar = true;
        AppWindow.TitleBar.IconShowOptions = IconShowOptions.ShowIconAndSystemMenu;
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        AppWindow.Closing += AppWindow_Closing;
        AppWindow.Changed += AppWindow_Changed;
        Content.KeyDown += Content_KeyDown;
        CenterInScreen(1200, 676);
        AdaptTitleBarButtonColorToActuallTheme();
        UpdateDragRectangles();
        SetIcon();
        WTSRegisterSessionNotification(WindowHandle, 0);
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsMaximizable = false;
            presenter.IsResizable = false;
        }
    }



    public override void CenterInScreen(int? width = null, int? height = null)
    {
        width = width <= 0 ? null : width;
        height = height <= 0 ? null : height;
        User32.GetCursorPos(out POINT point);
        DisplayArea display = DisplayArea.GetFromPoint(new PointInt32(point.X, point.Y), DisplayAreaFallback.Nearest);
        double scale = UIScale;
        int w = (int)((width * scale) ?? AppWindow.Size.Width);
        int h = (int)((height * scale) ?? AppWindow.Size.Height);
        int x = display.WorkArea.X + (display.WorkArea.Width - w) / 2;
        int y = display.WorkArea.Y + (display.WorkArea.Height - h) / 2;
        AppWindow.MoveAndResize(new RectInt32(x, y, w, h));
    }



    public override void Show()
    {
        double uiScale = UIScale;
        if (Math.Abs(AppWindow.Size.Width - 1200 * uiScale) > 10 || Math.Abs(AppWindow.Size.Height - 676 * uiScale) > 10)
        {
            CenterInScreen(1200, 676);
        }
        base.Show();
    }



    public void ShowByGamepad()
    {
        CenterInScreen(1200, 676);
        User32.SetCursorPos(AppWindow.Position.X + AppWindow.Size.Width / 2, AppWindow.Position.Y + AppWindow.Size.Height / 2);
        base.Show();
    }



    private static bool AreAnimationsEnabled()
    {
        try
        {
            return new Windows.UI.ViewManagement.UISettings().AnimationsEnabled;
        }
        catch
        {
            return true;
        }
    }



    private void NavigateToView(UIElement newContent, ViewTransitionType transitionType)
    {
        bool isWizardView = newContent is WelcomeView or HoYoShadeDownloadView or ReShadeDownloadView or QuickSetupView;

        if (transitionType == ViewTransitionType.None || !AreAnimationsEnabled() || _currentPresenter.Content == null)
        {
            _activeTransitionBatch = null;
            _currentPresenter.Content = newContent;
            _currentPresenter.Opacity = 1;
            _currentPresenter.Visibility = Visibility.Visible;
            _nextPresenter.Visibility = Visibility.Collapsed;
            _nextPresenter.Content = null;

            var currentVisual = ElementCompositionPreview.GetElementVisual(_currentPresenter);
            currentVisual.Offset = Vector3.Zero;
            currentVisual.Opacity = 1.0f;
            currentVisual.Scale = Vector3.One;

            WizardBackgroundImage.Visibility = isWizardView ? Visibility.Visible : Visibility.Collapsed;
            WizardBackgroundMask.Visibility = isWizardView ? Visibility.Visible : Visibility.Collapsed;
            return;
        }

        if (isWizardView)
        {
            WizardBackgroundImage.Visibility = Visibility.Visible;
            WizardBackgroundMask.Visibility = Visibility.Visible;
        }

        var outgoingPresenter = _currentPresenter;
        var incomingPresenter = _nextPresenter;

        incomingPresenter.Content = newContent;
        incomingPresenter.Visibility = Visibility.Visible;

        var compositor = ElementCompositionPreview.GetElementVisual(this.Content).Compositor;
        var outgoingVisual = ElementCompositionPreview.GetElementVisual(outgoingPresenter);
        var incomingVisual = ElementCompositionPreview.GetElementVisual(incomingPresenter);

        // Reset visual properties before starting animation
        incomingVisual.Offset = Vector3.Zero;
        incomingVisual.Opacity = 0f;
        incomingVisual.Scale = Vector3.One;

        var batch = compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
        _activeTransitionBatch = batch;

        var cubicEasing = compositor.CreateCubicBezierEasingFunction(new Vector2(0.1f, 0.9f), new Vector2(0.2f, 1.0f));

        if (transitionType == ViewTransitionType.SlideFromRight)
        {
            // Incoming: start offset X = 60px, opacity = 0 -> offset X = 0, opacity = 1
            incomingVisual.Offset = new Vector3(60f, 0f, 0f);

            var inOffsetAnim = compositor.CreateVector3KeyFrameAnimation();
            inOffsetAnim.Duration = TimeSpan.FromMilliseconds(350);
            inOffsetAnim.InsertKeyFrame(1.0f, Vector3.Zero, cubicEasing);

            var inOpacityAnim = compositor.CreateScalarKeyFrameAnimation();
            inOpacityAnim.Duration = TimeSpan.FromMilliseconds(300);
            inOpacityAnim.InsertKeyFrame(1.0f, 1.0f, cubicEasing);

            // Outgoing: offset X = 0 -> -50px, opacity = 1 -> 0
            var outOffsetAnim = compositor.CreateVector3KeyFrameAnimation();
            outOffsetAnim.Duration = TimeSpan.FromMilliseconds(300);
            outOffsetAnim.InsertKeyFrame(1.0f, new Vector3(-50f, 0f, 0f), cubicEasing);

            var outOpacityAnim = compositor.CreateScalarKeyFrameAnimation();
            outOpacityAnim.Duration = TimeSpan.FromMilliseconds(250);
            outOpacityAnim.InsertKeyFrame(1.0f, 0f, cubicEasing);

            incomingVisual.StartAnimation(nameof(Visual.Offset), inOffsetAnim);
            incomingVisual.StartAnimation(nameof(Visual.Opacity), inOpacityAnim);
            outgoingVisual.StartAnimation(nameof(Visual.Offset), outOffsetAnim);
            outgoingVisual.StartAnimation(nameof(Visual.Opacity), outOpacityAnim);
        }
        else if (transitionType == ViewTransitionType.DrillIn)
        {
            // Incoming: scale 1.03 -> 1.0, opacity 0 -> 1
            float width = (float)(AppWindow.Size.Width / UIScale);
            float height = (float)(AppWindow.Size.Height / UIScale);
            incomingVisual.CenterPoint = new Vector3(width / 2f, height / 2f, 0f);
            incomingVisual.Scale = new Vector3(1.03f, 1.03f, 1.0f);

            var inScaleAnim = compositor.CreateVector3KeyFrameAnimation();
            inScaleAnim.Duration = TimeSpan.FromMilliseconds(450);
            inScaleAnim.InsertKeyFrame(1.0f, Vector3.One, cubicEasing);

            var inOpacityAnim = compositor.CreateScalarKeyFrameAnimation();
            inOpacityAnim.Duration = TimeSpan.FromMilliseconds(400);
            inOpacityAnim.InsertKeyFrame(1.0f, 1.0f, cubicEasing);

            // Outgoing: opacity 1 -> 0
            var outOpacityAnim = compositor.CreateScalarKeyFrameAnimation();
            outOpacityAnim.Duration = TimeSpan.FromMilliseconds(250);
            outOpacityAnim.InsertKeyFrame(1.0f, 0f, cubicEasing);

            incomingVisual.StartAnimation(nameof(Visual.Scale), inScaleAnim);
            incomingVisual.StartAnimation(nameof(Visual.Opacity), inOpacityAnim);
            outgoingVisual.StartAnimation(nameof(Visual.Opacity), outOpacityAnim);
        }

        batch.Completed += (s, e) =>
        {
            if (_activeTransitionBatch == batch)
            {
                _activeTransitionBatch = null;
            }

            outgoingPresenter.Visibility = Visibility.Collapsed;
            outgoingPresenter.Content = null;
            outgoingVisual.Offset = Vector3.Zero;
            outgoingVisual.Opacity = 1.0f;
            outgoingVisual.Scale = Vector3.One;

            incomingVisual.Offset = Vector3.Zero;
            incomingVisual.Opacity = 1.0f;
            incomingVisual.Scale = Vector3.One;

            if (!isWizardView)
            {
                WizardBackgroundImage.Visibility = Visibility.Collapsed;
                WizardBackgroundMask.Visibility = Visibility.Collapsed;
            }

            // Swap current and next presenters
            (_currentPresenter, _nextPresenter) = (_nextPresenter, _currentPresenter);
        };

        batch.End();
    }



    private void LoadContentView()
    {
        if (string.IsNullOrWhiteSpace(AppConfig.UserDataFolder))
        {
            NavigateToView(new WelcomeView(), ViewTransitionType.None);
        }
        else
        {
            AppConfig.WelcomeOOBECompleted = true;
            NavigateToView(new MainView(), ViewTransitionType.None);
            App.Current.EnsureSystemTray();
            _mainViewLoaded = true;
        }
    }



    private void OnWelcomePageFinished(object _, WelcomePageFinishedMessage __)
    {
        if (!_mainViewLoaded && !AppConfig.WelcomeOOBECompleted)
        {
            AppConfig.WelcomeOOBECompleted = true;
            var oobeView = new WelcomeOOBEView();
            oobeView.Completed += () =>
            {
                NavigateToView(new MainView(), ViewTransitionType.DrillIn);
                App.Current.EnsureSystemTray();
                _mainViewLoaded = true;
            };
            NavigateToView(oobeView, ViewTransitionType.DrillIn);
        }
        else
        {
            NavigateToView(new MainView(), ViewTransitionType.DrillIn);
            App.Current.EnsureSystemTray();
            _mainViewLoaded = true;
        }
    }


    private void OnNavigateToQuickSetupPage(object _, NavigateToQuickSetupPageMessage __)
    {
        NavigateToView(new QuickSetupView(), ViewTransitionType.SlideFromRight);
    }


    private void OnNavigateToDownloadPage(object _, NavigateToDownloadPageMessage m)
    {
        NavigateToView(new HoYoShadeDownloadView
        {
            IsUpdateMode = m.IsUpdateMode
        }, ViewTransitionType.SlideFromRight);
    }


    private void OnNavigateToReShadeDownloadPage(object _, NavigateToReShadeDownloadPageMessage m)
    {
        NavigateToView(new ReShadeDownloadView
        {
            IsUpdateMode = m.IsUpdateMode
        }, ViewTransitionType.SlideFromRight);
    }


    private void OnGameStarted(object _, GameStartedMessage __)
    {
        if (_mainViewLoaded)
        {
            StartGameAction action = AppConfig.StartGameAction;
            if (action is StartGameAction.Hide)
            {
                this.Hide();
            }
            else if (action is StartGameAction.Minimize)
            {
                this.Minimize();
            }
        }
    }



    private async void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        try
        {
            if (!_mainViewLoaded)
            {
                App.Current.Exit();
                return;
            }
            args.Cancel = true;
            MainWindowCloseOption option = AppConfig.CloseWindowOption;
            if (option is not MainWindowCloseOption.Hide and not MainWindowCloseOption.Exit)
            {
                var dialog = new MainWindowCloseDialog
                {
                    Title = Lang.ExperienceSettingPage_CloseWindowOption,
                    PrimaryButtonText = Lang.Common_Confirm,
                    CloseButtonText = Lang.Common_Cancel,
                    DefaultButton = ContentDialogButton.Primary,
                    XamlRoot = Content.XamlRoot,
                };
                var result = await dialog.ShowAsync();
                if (result is not ContentDialogResult.Primary)
                {
                    return;
                }
                option = dialog.MainWindowCloseOption.Value;
                AppConfig.CloseWindowOption = option;
            }
            if (option is MainWindowCloseOption.Hide)
            {
                Hide();
            }
            if (option is MainWindowCloseOption.Exit)
            {
                Close();
                AppInstance.GetCurrent().UnregisterKey();
                Task backupTask = Task.Run(DatabaseService.AutoBackupToAppDataLocal);
                Task timeTask = Task.Delay(30000);
                await Task.WhenAny(backupTask, timeTask);
                App.Current.Exit();
            }
        }
        catch { }
    }



    private void OnAccentColorChanged(object _, AccentColorChangedMessage __)
    {
        FrameworkElement ele = (FrameworkElement)Content;
        ele.RequestedTheme = ele.ActualTheme switch
        {
            ElementTheme.Light => ElementTheme.Dark,
            ElementTheme.Dark => ElementTheme.Light,
            _ => ElementTheme.Default,
        };
        ele.RequestedTheme = ElementTheme.Default;
    }



    private void Content_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (_mainViewLoaded)
        {
            if (e.Key is Windows.System.VirtualKey.Escape)
            {
                Hide();
            }
        }
    }



    public override void Hide()
    {
        base.Hide();
        WeakReferenceMessenger.Default.Send(new MainWindowStateChangedMessage { Hide = true, CurrentTime = DateTimeOffset.Now });
        GC.Collect();
    }



    private DateTimeOffset _lastActivatedTime = DateTimeOffset.Now;


    protected override nint WindowSubclassProc(HWND hWnd, uint uMsg, nint wParam, nint lParam, nuint uIdSubclass, nint dwRefData)
    {
        if (uMsg == (uint)User32.WindowMessage.WM_ACTIVATE || uMsg == (uint)User32.WindowMessage.WM_POINTERACTIVATE)
        {
            // 窗口激活
            if (wParam is 0x1 or 0x2)
            {
                // WA_ACTIVE or WA_CLICKACTIVE
                var now = DateTimeOffset.Now;
                WeakReferenceMessenger.Default.Send(new MainWindowStateChangedMessage
                {
                    Activate = true,
                    CurrentTime = now,
                    LastActivatedTime = _lastActivatedTime,
                });
                _lastActivatedTime = now;
            }
        }
        else if (uMsg == (uint)User32.WindowMessage.WM_SYSCOMMAND)
        {
            if (wParam == 0xF030)
            {
                // SC_MAXIMIZE
                // 防止双击标题栏使窗口最大化，WinAppSDK 某个版本的 Bug
                return IntPtr.Zero;
            }
        }
        else if (uMsg == (uint)User32.WindowMessage.WM_WTSSESSION_CHANGE)
        {
            if (wParam == 0x7)
            {
                // WTS_SESSION_LOCK
                // 锁屏，暂停视频背景
                WeakReferenceMessenger.Default.Send(new MainWindowStateChangedMessage { SessionLock = true, CurrentTime = DateTimeOffset.Now });
            }
            else if (wParam == 0x8)
            {
                // WTS_SESSION_UNLOCK 
            }
        }
        else if (uMsg == (uint)User32.WindowMessage.WM_DEVICECHANGE)
        {
            // 存储设备插入/拔出
            if (wParam == 0x8000)
            {
                // DBT_DEVICEARRIVAL
                User32.DEV_BROADCAST_HDR dev = Marshal.PtrToStructure<User32.DEV_BROADCAST_HDR>(lParam);
                if (dev.dbch_devicetype is User32.DBT_DEVTYPE.DBT_DEVTYP_VOLUME)
                {
                    WeakReferenceMessenger.Default.Send(new RemovableStorageDeviceChangedMessage());
                }
            }
            else if (wParam == 0x8004)
            {
                // DBT_DEVICEREMOVECOMPLETE
                User32.DEV_BROADCAST_HDR dev = Marshal.PtrToStructure<User32.DEV_BROADCAST_HDR>(lParam);
                if (dev.dbch_devicetype is User32.DBT_DEVTYPE.DBT_DEVTYP_VOLUME)
                {
                    WeakReferenceMessenger.Default.Send(new RemovableStorageDeviceChangedMessage());
                }
            }
        }
        else if (uMsg == (uint)User32.WindowMessage.WM_HOTKEY)
        {
            if (wParam == 44444)
            {
                if (!RunningGameService.OpenOverlayWindow())
                {
                    this.Show();
                }
            }
        }
        return base.WindowSubclassProc(hWnd, uMsg, wParam, lParam, uIdSubclass, dwRefData);
    }



    [LibraryImport("wtsapi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool WTSRegisterSessionNotification(IntPtr hWnd, int dwFlags);


    private void UpdateDragRectangles()
    {
        if (AppWindowTitleBar.IsCustomizationSupported() && AppWindow.TitleBar.ExtendsContentIntoTitleBar == true)
        {
            double scale = UIScale;
            int titleBarHeight = (int)(48 * scale);
            int leftInset = AppWindow.TitleBar.LeftInset;
            int rightInset = AppWindow.TitleBar.RightInset;
            int dragWidth = AppWindow.Size.Width - leftInset - rightInset;
            if (dragWidth > 0)
            {
                SetDragRectangles(new RectInt32(leftInset, 0, dragWidth, titleBarHeight));
            }
        }
    }

    private void AppWindow_Changed(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (args.DidSizeChange || args.DidPresenterChange)
        {
            UpdateDragRectangles();
        }
    }
}
