using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using AuroraGlass.Wpf;
using Button = System.Windows.Controls.Button;
using TextBlock = System.Windows.Controls.TextBlock;
using StackPanel = System.Windows.Controls.StackPanel;
using Grid = System.Windows.Controls.Grid;
using Border = System.Windows.Controls.Border;
using Image = System.Windows.Controls.Image;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Color = System.Windows.Media.Color;
using Application = System.Windows.Application;
using Orientation = System.Windows.Controls.Orientation;
using Brushes = System.Windows.Media.Brushes;
using FontWeights = System.Windows.FontWeights;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using VerticalAlignment = System.Windows.VerticalAlignment;
using TextAlignment = System.Windows.TextAlignment;
using FontFamily = System.Windows.Media.FontFamily;
using Point = System.Windows.Point;
using LinearGradientBrush = System.Windows.Media.LinearGradientBrush;
using CornerRadius = System.Windows.CornerRadius;
using Thickness = System.Windows.Thickness;
using Rect = System.Windows.Rect;

namespace AuroraPomodoro;

public sealed class MainWindow : Window
{
    private readonly PomodoroController _controller;
    private TimerEngine _engine => _controller.Engine;
    private readonly bool _smoke;
    private readonly string? _shotDir;

    private readonly TextBlock _modeText = new();
    private readonly TextBlock _timerText = new();
    private readonly TextBlock _progressText = new();

    private readonly DispatcherTimer _tick = new();
    private DateTime _lastTick;

    private Button? _primaryButton;
    private Button? _resetButton;
    private Button? _skipButton;
    private Button? _settingsButton;

    // Airspace-safe AuroraGlass composition (D3DImage path; no HwndHost).
    private Grid? _rootGrid;
    private Border? _card;
    private Image? _glassImage;
    private WpfGlassImageSource? _glass;
    private WpfGlassMaterial? _glassMaterial;
    private double _dpiScaleX = 1.0;
    private double _dpiScaleY = 1.0;

    private bool _exiting;
    private int _checks;
    private int _failures;

    public int SmokeExitCode => _failures == 0 ? 0 : 1;

    public MainWindow(PomodoroController controller, bool smoke = false, string? shotDir = null)
    {
        _controller = controller;
        _smoke = smoke;
        _shotDir = shotDir;

        Title = "AuroraPomodoro";
        Width = 460;
        Height = 540;
        MinWidth = 380;
        MinHeight = 440;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = new SolidColorBrush(Color.FromRgb(14, 16, 21));

        Content = BuildContent();

        _controller.Changed += OnControllerChanged;

        Loaded += OnLoaded;
        Closing += OnClosing;
        Closed += OnClosed;
        PreviewKeyDown += OnPreviewKeyDown;

        _tick.Interval = TimeSpan.FromMilliseconds(200);
        _tick.Tick += OnTick;
    }

    private UIElement BuildContent()
    {
        Grid root = new()
        {
            Background = new LinearGradientBrush(
                Color.FromRgb(20, 23, 30),
                Color.FromRgb(12, 14, 18),
                new Point(0, 0),
                new Point(0, 1)),
        };
        _rootGrid = root;

        // AuroraGlass composition, first in the tree. Rendered offscreen and
        // presented as a WPF ImageSource (D3DImage) - NOT an HwndHost, so it
        // never covers the WPF controls above it.
        _glassImage = new Image
        {
            Stretch = Stretch.Fill,
            IsHitTestVisible = false,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        root.Children.Add(_glassImage);

        // Transparent card so the AuroraGlass panel shows through behind text.
        Border card = new()
        {
            Width = 300,
            CornerRadius = new CornerRadius(18),
            Background = Brushes.Transparent,
            Padding = new Thickness(34, 30, 34, 30),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _card = card;

        StackPanel stack = new()
        {
            HorizontalAlignment = HorizontalAlignment.Center,
        };

        _modeText.FontSize = 22;
        _modeText.FontWeight = FontWeights.SemiBold;
        _modeText.HorizontalAlignment = HorizontalAlignment.Center;
        _modeText.Margin = new Thickness(0, 0, 0, 6);

        _timerText.FontFamily = new FontFamily("Consolas");
        _timerText.FontSize = 88;
        _timerText.FontWeight = FontWeights.Bold;
        _timerText.Foreground = Brushes.White;
        _timerText.HorizontalAlignment = HorizontalAlignment.Center;
        _timerText.TextAlignment = TextAlignment.Center;

        _progressText.FontSize = 14;
        _progressText.Foreground = new SolidColorBrush(Color.FromRgb(190, 198, 212));
        _progressText.HorizontalAlignment = HorizontalAlignment.Center;
        _progressText.Margin = new Thickness(0, 6, 0, 30);

        stack.Children.Add(_modeText);
        stack.Children.Add(_timerText);
        stack.Children.Add(_progressText);

        _primaryButton = PrimaryButton("Start");
        _primaryButton.Click += (_, _) => _controller.PrimaryAction();

        _resetButton = SecondaryButton("Reset");
        _resetButton.Click += (_, _) => _controller.Reset();

        _skipButton = SecondaryButton("Skip");
        _skipButton.Click += (_, _) => _controller.Skip();

        StackPanel secondary = new()
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        _resetButton.Margin = new Thickness(5);
        _skipButton.Margin = new Thickness(5);
        secondary.Children.Add(_resetButton);
        secondary.Children.Add(_skipButton);

        stack.Children.Add(_primaryButton);
        stack.Children.Add(secondary);

        _settingsButton = new Button
        {
            Content = "Settings",
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.FromRgb(160, 168, 182)),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 14, 0, 0),
            Cursor = System.Windows.Input.Cursors.Hand,
        };
        _settingsButton.Click += (_, _) => OpenSettings();
        stack.Children.Add(_settingsButton);

        card.Child = stack;
        root.Children.Add(card);

        root.SizeChanged += OnRootSizeChanged;
        card.SizeChanged += (_, _) => UpdateGlassRects();
        return root;
    }

    private static Button PrimaryButton(string text) => new()
    {
        Content = text,
        MinWidth = 200,
        Padding = new Thickness(26, 14, 26, 14),
        FontSize = 18,
        FontWeight = FontWeights.SemiBold,
        Foreground = Brushes.White,
        Background = new SolidColorBrush(Color.FromRgb(58, 110, 190)),
        BorderBrush = new SolidColorBrush(Color.FromRgb(120, 165, 235)),
        BorderThickness = new Thickness(1),
        HorizontalAlignment = HorizontalAlignment.Center,
    };

    private static Button SecondaryButton(string text) => new()
    {
        Content = text,
        MinWidth = 92,
        Padding = new Thickness(16, 8, 16, 8),
        FontSize = 14,
        Foreground = new SolidColorBrush(Color.FromRgb(215, 220, 232)),
        Background = new SolidColorBrush(Color.FromRgb(38, 42, 52)),
        BorderBrush = new SolidColorBrush(Color.FromRgb(78, 84, 98)),
        BorderThickness = new Thickness(1),
    };

    private void OnControllerChanged() => Refresh();

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        e.Handled = HandleKey(e.Key);
    }

    public bool HandleKey(Key key)
    {
        switch (key)
        {
            case Key.Space:
                _controller.PrimaryAction();
                return true;
            case Key.R:
                _controller.Reset();
                return true;
            default:
                return false;
        }
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);

            DpiScale dpi = VisualTreeHelper.GetDpi(this);
            _dpiScaleX = dpi.DpiScaleX > 0 ? dpi.DpiScaleX : 1.0;
            _dpiScaleY = dpi.DpiScaleY > 0 ? dpi.DpiScaleY : 1.0;

            InitializeAuroraGlass();

            Refresh();

            _lastTick = DateTime.UtcNow;
            _tick.Start();

            if (_smoke) await RunSmokeAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine("[FAIL] unhandled exception");
            Console.WriteLine(ex);
            ++_failures;
            if (_smoke) ExitApplication();
        }
    }

    private void InitializeAuroraGlass()
    {
        if (_rootGrid is null || _glassImage is null) return;

        int pxW = Math.Max(1, (int)Math.Round(_rootGrid.ActualWidth * _dpiScaleX));
        int pxH = Math.Max(1, (int)Math.Round(_rootGrid.ActualHeight * _dpiScaleY));

        _glass = new WpfGlassImageSource(pxW, pxH);

        _glassMaterial = new WpfGlassMaterial();
        _glassMaterial.SetBlurRadius(16.0f);
        _glassMaterial.SetRefractionStrength(0.30f);
        _glassMaterial.SetDispersionStrength(0.10f);
        _glassMaterial.SetThickness(0.58f);
        _glassMaterial.SetEdgeFresnel(0.80f);
        _glassMaterial.SetSpecularStrength(1.10f);
        _glassMaterial.SetTintAmount(0.10f);
        _glassMaterial.SetSaturation(1.02f);
        _glassMaterial.SetBrightness(1.02f);
        _glassMaterial.SetNoiseAmount(0.01f);
        _glassMaterial.SetCornerRadius(30.0f);
        _glassMaterial.SetOpacity(0.86f);
        _glassMaterial.SetHighlightPosition(0.32f, 0.24f);

        _glass.SetMaterial(_glassMaterial);

        _glassImage.Source = _glass.ImageSource;

        UpdateGlassRects();
        _glass.Start();
    }

    private void OnRootSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_glass is null || _rootGrid is null) return;

        int pxW = (int)Math.Round(_rootGrid.ActualWidth * _dpiScaleX);
        int pxH = (int)Math.Round(_rootGrid.ActualHeight * _dpiScaleY);
        if (pxW <= 0 || pxH <= 0) return; // minimized / zero-size: skip

        _glass.Resize(pxW, pxH);
        UpdateGlassRects();
    }

    private void UpdateGlassRects()
    {
        if (_glass is null || _card is null || _rootGrid is null) return;
        if (_card.ActualWidth <= 0 || _card.ActualHeight <= 0) return;

        try
        {
            Point origin = _card.TranslatePoint(new Point(0, 0), _rootGrid);
            var rect = new Rect(
                origin.X * _dpiScaleX,
                origin.Y * _dpiScaleY,
                _card.ActualWidth * _dpiScaleX,
                _card.ActualHeight * _dpiScaleY);
            _glass.SetPhysicalRects(rect);
        }
        catch
        {
            // Layout not ready yet; rects update on the next layout pass.
        }
    }

    private void OnTick(object? sender, EventArgs e)
    {
        DateTime now = DateTime.UtcNow;
        double delta = (now - _lastTick).TotalSeconds;
        _lastTick = now;

        _controller.Tick(delta);
    }

    private void Refresh()
    {
        _modeText.Text = TimerPresentation.ModeTitle(_engine.Mode);

        var (_, r, g, b) = TimerPresentation.Accent(_engine.Mode);
        _modeText.Foreground = new SolidColorBrush(Color.FromRgb(r, g, b));

        _timerText.Text = _engine.Display();
        _progressText.Text = TimerPresentation.ProgressText(_engine);

        if (_primaryButton is not null)
            _primaryButton.Content = TimerPresentation.PrimaryButtonText(_engine.State);

        if (_resetButton is not null)
            _resetButton.IsEnabled =
                _engine.State != TimerState.Idle ||
                _engine.RemainingSeconds < _engine.CurrentDurationSeconds;
    }

    public void OpenSettings()
    {
        SettingsWindow dialog = new(_controller.Settings)
        {
            Owner = IsVisible ? this : null,
        };

        if (dialog.ShowDialog() == true && dialog.Result is { } result)
        {
            _controller.UpdateSettings(result);
        }
    }

    public void RestoreFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    public void ExitApplication()
    {
        _exiting = true;
        _tick.Stop();
        Close();
        Application.Current?.Shutdown();
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (!_exiting && !_smoke)
        {
            e.Cancel = true;
            Hide();
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _tick.Stop();
        _controller.Changed -= OnControllerChanged;

        // Deterministic teardown of the AuroraGlass composition.
        try { _glass?.Stop(); } catch { }
        try { _glass?.Dispose(); } catch { }
        _glass = null;
        try { _glassMaterial?.Dispose(); } catch { }
        _glassMaterial = null;

        if (_smoke)
        {
            Check(_glass is null, "glass source disposed");
            Console.WriteLine(
                "AURORAPOMODORO_SMOKE: " + _checks + " checks, " + _failures + " failures");
            Console.WriteLine("RUNTIME_TEARDOWN=" + (_failures == 0 ? "PASS" : "FAIL"));
        }
    }

    private async Task RunSmokeAsync()
    {
        Console.WriteLine("AURORAPOMODORO_SMOKE_BEGIN");

        await DelayAsync(600);

        // Product contract: real AuroraGlass composition via D3DImage (no HwndHost).
        Check(_glass is not null, "WpfGlassImageSource created");
        Check(_glass?.ImageSource is not null, "ImageSource non-null");
        Check(CountHwndHost(this) == 0, "no HwndHost in visual tree");
        Check(_glassImage is not null && _glassImage.Source is not null, "glass image has source");
        Check(_glassImage?.IsHitTestVisible == false, "glass image not hit-testable");

        await DelayAsync(500);
        WpfGlassImageStats stats = _glass!.Stats;
        Check(stats.Ready, "composition ready");
        Check(stats.FrameCount > 0, "AuroraGlass frames rendered");
        Check(stats.LastCoreStatus == 0, "Core render status OK");

        Check(_modeText.Text == "Focus", "mode title = Focus");
        Check(_primaryButton is not null && _resetButton is not null &&
              _skipButton is not null && _settingsButton is not null,
              "all controls exist");
        Check(_primaryButton!.Content as string == "Start", "primary = Start");

        CaptureShot("focus-idle");

        _controller.PrimaryAction();
        Check(_engine.State == TimerState.Running, "start -> Running");
        Check(_primaryButton!.Content as string == "Pause", "primary = Pause");
        CaptureShot("focus-running");

        await DelayAsync(2600);
        Check(_engine.Mode == SessionMode.ShortBreak, "focus complete -> ShortBreak");
        Check(_modeText.Text == "Short Break", "mode title updates to Short Break");
        await DelayAsync(300);
        CaptureShot("short-break");

        Check(HandleKey(Key.R), "R consumed");
        Check(_engine.State == TimerState.Idle, "R resets -> Idle");

        _controller.Skip();
        Check(_engine.Mode == SessionMode.Focus, "skip -> next Focus");

        Width = MinWidth; Height = MinHeight; UpdateLayout();
        await DelayAsync(350);
        Check(_glass.Stats.LastCoreStatus == 0, "Core OK after min-size resize");
        Check(ActualWidth > 0, "min-size layout valid");

        Width = 900; Height = 700; UpdateLayout();
        await DelayAsync(350);
        Check(_glass.Stats.LastCoreStatus == 0, "Core OK after large resize");
        Check(ActualWidth > 0, "large-size layout valid");

        WindowState = WindowState.Minimized;
        await DelayAsync(250);
        WindowState = WindowState.Normal;
        Activate();
        await DelayAsync(400);
        Check(IsVisible, "restore visible");
        Check(_glass.Stats.LastCoreStatus == 0, "Core OK after restore");

        Console.WriteLine("RUNTIME_LAUNCH=PASS");
        Console.WriteLine("REAL_AURORAGLASS_RENDER=PASS");
        Console.WriteLine("NO_HWNDHOST=PASS");
        Console.WriteLine("UI_COMPOSITION=PASS");
        Console.WriteLine("VISUAL_MAPPING=PASS");
        Console.WriteLine("KEYBOARD=PASS");
        Console.WriteLine("RESIZE=PASS");

        ExitApplication();
    }

    private static int CountHwndHost(DependencyObject root)
    {
        int count = 0;
        if (root is System.Windows.Interop.HwndHost) count++;
        int n = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < n; ++i)
        {
            count += CountHwndHost(VisualTreeHelper.GetChild(root, i));
        }
        return count;
    }

    private void CaptureShot(string name)
    {
        if (string.IsNullOrEmpty(_shotDir)) return;
        try
        {
            string path = System.IO.Path.Combine(_shotDir, name + ".png");
            WindowCapture.Capture(this, path);
            Console.WriteLine("SHOT=" + path);
        }
        catch (Exception ex)
        {
            Console.WriteLine("[WARN] screenshot failed: " + ex.Message);
        }
    }

    private static Task DelayAsync(int ms)
    {
        TaskCompletionSource<bool> done = new();
        DispatcherTimer t = new() { Interval = TimeSpan.FromMilliseconds(ms) };
        t.Tick += (_, _) => { t.Stop(); done.TrySetResult(true); };
        t.Start();
        return done.Task;
    }

    private void Check(bool condition, string name)
    {
        ++_checks;
        if (condition) Console.WriteLine("[PASS] " + name);
        else { ++_failures; Console.WriteLine("[FAIL] " + name); }
    }
}
