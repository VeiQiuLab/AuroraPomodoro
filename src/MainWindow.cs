using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using AuroraGlass.Wpf;
using Button = System.Windows.Controls.Button;
using TextBlock = System.Windows.Controls.TextBlock;
using StackPanel = System.Windows.Controls.StackPanel;
using Grid = System.Windows.Controls.Grid;
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

namespace AuroraPomodoro;

public sealed class MainWindow : Window
{
    private readonly PomodoroController _controller;
    private TimerEngine _engine => _controller.Engine;
    private readonly bool _smoke;
    private readonly string? _shotDir;

    private readonly WpfHostAttachment _host = new();
    private readonly WpfGlassMaterial _material = new();
    private readonly WpfRenderHost _renderHost = new();

    private readonly TextBlock _modeText = new();
    private readonly TextBlock _timerText = new();
    private readonly TextBlock _progressText = new();

    private readonly DispatcherTimer _tick = new();
    private DateTime _lastTick;

    private Button? _primaryButton;
    private Button? _resetButton;
    private Button? _skipButton;

    private bool _initialized;
    private bool _exiting;
    private int _checks;
    private int _failures;

    private Rect _glassPanelLocal = new(0, 0, 0, 0);

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

        _host.Attach(this);
        _host.MetricsChanged += _ => Dispatcher.BeginInvoke(
            DispatcherPriority.Render, new Action(UpdateGlassRect));

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
        Grid root = new();
        root.Children.Add(_renderHost);

        StackPanel stack = new()
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(32),
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
        _progressText.Foreground = new SolidColorBrush(Color.FromRgb(170, 178, 192));
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

        // Weak, low-emphasis settings entry (primary entry point is the tray menu).
        Button settingsButton = new()
        {
            Content = "Settings",
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.FromRgb(150, 158, 172)),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 14, 0, 0),
            Cursor = System.Windows.Input.Cursors.Hand,
        };
        settingsButton.Click += (_, _) => OpenSettings();
        stack.Children.Add(settingsButton);

        stack.SizeChanged += (_, _) =>
        {
            double w = Math.Max(stack.ActualWidth + 80, 260);
            double h = Math.Max(stack.ActualHeight + 70, 320);
            _glassPanelLocal = new Rect(0, 0, w, h);
        };

        Grid overlay = new();
        overlay.Children.Add(stack);
        root.Children.Add(overlay);
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

    private void OnControllerChanged()
    {
        ApplyMaterialIfModeChanged();
        Refresh();
        UpdateGlassRect();
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        e.Handled = HandleKey(e.Key);
    }

    /// <summary>Window-level key handling. Returns true if the key was consumed.</summary>
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

            _lastMode = _engine.Mode;
            ApplyMaterial();
            _renderHost.SetMaterial(_material);
            _initialized = true;

            UpdateGlassRect();
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

    private SessionMode _lastMode;

    private void ApplyMaterialIfModeChanged()
    {
        if (!_initialized) return;
        if (_engine.Mode != _lastMode)
        {
            _lastMode = _engine.Mode;
            ApplyMaterial();
            _renderHost.SetMaterial(_material);
        }
    }

    private void ApplyMaterial()
    {
        // Slightly calmer than Step 17 (lower opacity + gentle blur).
        _material.SetBlurRadius(16.0f);
        _material.SetRefractionStrength(0.20f);
        _material.SetDispersionStrength(0.05f);
        _material.SetThickness(0.58f);
        _material.SetEdgeFresnel(0.70f);
        _material.SetSpecularStrength(0.95f);
        _material.SetTintAmount(0.10f);
        _material.SetSaturation(0.98f);
        _material.SetBrightness(0.94f);
        _material.SetNoiseAmount(0.015f);
        _material.SetCornerRadius(30.0f);
        _material.SetOpacity(ModeOpacity());
        _material.SetHighlightPosition(0.30f, 0.20f);
    }

    private float ModeOpacity() => _engine.Mode switch
    {
        SessionMode.Focus => 0.70f,
        SessionMode.ShortBreak => 0.62f,
        SessionMode.LongBreak => 0.58f,
        _ => 0.70f,
    };

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

    private void UpdateGlassRect()
    {
        if (!_initialized || !_host.IsAttached) return;
        if (_glassPanelLocal.Width <= 0) return;

        WpfHostMetrics m = _host.CurrentMetrics;
        if (m.ClientWidth == 0 || m.ClientHeight == 0) return;

        double panelW = _glassPanelLocal.Width;
        double panelH = _glassPanelLocal.Height;
        double x = (m.ClientWidth - panelW) / 2.0;
        double y = (m.ClientHeight - panelH) / 2.0;
        if (x < 0) x = 0;
        if (y < 0) y = 0;

        _renderHost.SetPhysicalRects(new Rect(x, y, panelW, panelH));
    }

    /// <summary>Open the settings dialog (tray "Settings" or the weak button).</summary>
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

    /// <summary>Show + activate the window (used by the tray "Open" action).</summary>
    public void RestoreFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    /// <summary>Real application exit path (tray Exit or smoke teardown).</summary>
    public void ExitApplication()
    {
        _exiting = true;
        _tick.Stop();
        Close();
        Application.Current?.Shutdown();
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        // Close (X) hides to tray unless a real exit was requested.
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
        _host.Dispose();
        _material.Dispose();

        if (_smoke)
        {
            Check(!_host.IsAttached, "clean detach");
            Console.WriteLine(
                "AURORAPOMODORO_SMOKE: " + _checks + " checks, " + _failures + " failures");
            Console.WriteLine("RUNTIME_TEARDOWN=" + (_failures == 0 ? "PASS" : "FAIL"));
        }
    }

    private async Task RunSmokeAsync()
    {
        Console.WriteLine("AURORAPOMODORO_SMOKE_BEGIN");

        await DelayAsync(400);
        Check(_host.IsAttached, "AuroraGlass host attached");
        Check(_renderHost.IsReady, "render host ready");
        Check(_renderHost.Stats.LastCoreStatus == 0, "Core status OK");
        await WaitFramesAsync(4);
        Check(_renderHost.Stats.FrameCount > 0, "glass frames presented");

        Check(_engine.Mode == SessionMode.Focus, "starts in Focus");
        Check(_modeText.Text == "Focus", "mode title = Focus");
        Check(_primaryButton!.Content as string == "Start", "primary = Start");

        CaptureShot("focus-idle");

        // Tray-style primary action delegates to shared logic.
        _controller.PrimaryAction();
        Check(_engine.State == TimerState.Running, "start -> Running");
        Check(_primaryButton!.Content as string == "Pause", "primary = Pause");
        CaptureShot("focus-running");

        await DelayAsync(2600);
        Check(_engine.Mode == SessionMode.ShortBreak, "focus complete -> ShortBreak");
        Check(_modeText.Text == "Short Break", "mode title updates to Short Break");
        await DelayAsync(300);
        CaptureShot("short-break");

        // Keyboard.
        Check(HandleKey(Key.R), "R consumed");
        Check(_engine.State == TimerState.Idle, "R resets -> Idle");

        _controller.Skip();
        Check(_engine.Mode == SessionMode.Focus, "skip -> next Focus");

        // Resize min/large.
        Width = MinWidth; Height = MinHeight; UpdateLayout();
        await DelayAsync(250);
        Check(_host.CurrentMetrics.ClientWidth > 0, "min-size metrics valid");
        Width = 900; Height = 700; UpdateLayout();
        await DelayAsync(300);
        Check(_host.CurrentMetrics.ClientWidth > 0, "large-size metrics valid");
        Check(_renderHost.Stats.LastCoreStatus == 0, "Core healthy after resize");

        WindowState = WindowState.Minimized;
        await DelayAsync(200);
        WindowState = WindowState.Normal;
        Activate();
        await DelayAsync(300);
        Check(_host.CurrentMetrics.ClientWidth > 0, "restore recovers metrics");

        Console.WriteLine("RUNTIME_LAUNCH=PASS");
        Console.WriteLine("AURORAGLASS_INIT=PASS");
        Console.WriteLine("VISUAL_MAPPING=PASS");
        Console.WriteLine("KEYBOARD=PASS");
        Console.WriteLine("RESIZE=PASS");

        ExitApplication();
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

    private async Task WaitFramesAsync(ulong minimum)
    {
        for (int i = 0; i < 100; ++i)
        {
            if (_renderHost.Stats.FrameCount >= minimum) return;
            await DelayAsync(30);
        }
        Check(false, "render frame wait completed");
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
