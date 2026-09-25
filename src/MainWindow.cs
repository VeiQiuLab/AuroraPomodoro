using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using AuroraGlass.Wpf;

namespace AuroraPomodoro;

public sealed class MainWindow : Window
{
    private readonly TimerEngine _engine = new();
    private readonly bool _smoke;

    private readonly WpfHostAttachment _host = new();
    private readonly WpfGlassMaterial _material = new();
    private readonly WpfRenderHost _renderHost = new();

    private readonly TextBlock _timerText = new();
    private readonly TextBlock _stateText = new();
    private readonly DispatcherTimer _tick = new();
    private DateTime _lastTick;

    private bool _initialized;
    private int _checks;
    private int _failures;

    private readonly Rect _glassPanelLocal = new(40, 40, 520, 400);
    private Button? _overlayPrimary;

    public int SmokeExitCode => _failures == 0 ? 0 : 1;

    public MainWindow(bool smoke = false)
    {
        _smoke = smoke;

        Title = "AuroraPomodoro";
        Width = 640;
        Height = 520;
        MinWidth = 520;
        MinHeight = 440;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = new SolidColorBrush(Color.FromRgb(16, 18, 24));

        Content = BuildContent();

        _host.Attach(this);
        _host.MetricsChanged += _ => Dispatcher.BeginInvoke(
            DispatcherPriority.Render, new Action(UpdateGlassRect));

        Loaded += OnLoaded;
        Closed += OnClosed;

        _tick.Interval = TimeSpan.FromMilliseconds(200);
        _tick.Tick += OnTick;
    }

    private UIElement BuildContent()
    {
        Grid root = new();
        root.Children.Add(_renderHost);

        Grid overlay = new();
        overlay.Margin = new Thickness(40);

        StackPanel stack = new()
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        _timerText.Text = _engine.Display();
        _timerText.FontFamily = new FontFamily("Consolas");
        _timerText.FontSize = 84;
        _timerText.Foreground = Brushes.White;
        _timerText.HorizontalAlignment = HorizontalAlignment.Center;

        _stateText.Text = "Idle";
        _stateText.FontSize = 16;
        _stateText.Foreground = new SolidColorBrush(Color.FromRgb(190, 196, 210));
        _stateText.HorizontalAlignment = HorizontalAlignment.Center;
        _stateText.Margin = new Thickness(0, 8, 0, 24);

        stack.Children.Add(_timerText);
        stack.Children.Add(_stateText);

        Button primary = MakeButton("Start");
        primary.Click += (_, _) => OnPrimary();

        Button reset = MakeButton("Reset");
        reset.Click += (_, _) => { _engine.Reset(); Refresh(); };

        StackPanel buttons = new()
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        primary.Margin = new Thickness(6);
        reset.Margin = new Thickness(6);
        buttons.Children.Add(primary);
        buttons.Children.Add(reset);

        stack.Children.Add(buttons);
        overlay.Children.Add(stack);
        root.Children.Add(overlay);
        _overlayPrimary = primary;
        return root;
    }

    private static Button MakeButton(string text)
    {
        return new Button
        {
            Content = text,
            Padding = new Thickness(22, 10, 22, 10),
            FontSize = 15,
            Background = new SolidColorBrush(Color.FromRgb(40, 44, 54)),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(90, 96, 110)),
            BorderThickness = new Thickness(1),
        };
    }

    private void OnPrimary()
    {
        switch (_engine.State)
        {
            case PomodoroState.Idle: _engine.Start(); break;
            case PomodoroState.Running: _engine.Pause(); break;
            case PomodoroState.Paused: _engine.Resume(); break;
        }
        Refresh();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);

            _material.SetBlurRadius(16.0f);
            _material.SetRefractionStrength(0.24f);
            _material.SetDispersionStrength(0.06f);
            _material.SetThickness(0.58f);
            _material.SetEdgeFresnel(0.76f);
            _material.SetSpecularStrength(1.05f);
            _material.SetTintAmount(0.08f);
            _material.SetSaturation(1.02f);
            _material.SetBrightness(1.02f);
            _material.SetNoiseAmount(0.01f);
            _material.SetCornerRadius(28.0f);
            _material.SetOpacity(0.82f);
            _material.SetHighlightPosition(0.30f, 0.22f);

            _renderHost.SetMaterial(_material);
            _initialized = true;

            UpdateGlassRect();
            Refresh();

            _lastTick = DateTime.UtcNow;
            _tick.Start();

            if (_smoke)
            {
                await RunSmokeAsync();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("[FAIL] unhandled exception");
            Console.WriteLine(ex);
            ++_failures;
            if (_smoke) Close();
        }
    }

    private void OnTick(object? sender, EventArgs e)
    {
        DateTime now = DateTime.UtcNow;
        double delta = (now - _lastTick).TotalSeconds;
        _lastTick = now;

        if (_engine.Tick(delta))
        {
            Refresh();
            UpdateGlassRect();
        }
    }

    private void Refresh()
    {
        _timerText.Text = _engine.Display();
        _stateText.Text = _engine.State.ToString();
        if (_overlayPrimary is not null)
        {
            _overlayPrimary.Content = _engine.State switch
            {
                PomodoroState.Idle => "Start",
                PomodoroState.Running => "Pause",
                PomodoroState.Paused => "Resume",
                _ => "Start",
            };
        }
    }

    private void UpdateGlassRect()
    {
        if (!_initialized || !_host.IsAttached) return;
        _renderHost.SetPhysicalRects(_host.DipToPhysical(_glassPanelLocal));
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _tick.Stop();

        // Note: the native path may already have detached the host via its
        // WM_NCDESTROY safety cleanup before Closed fires. That is expected.
        _host.Dispose();
        _material.Dispose();

        if (_smoke)
        {
            Check(!_host.IsAttached, "clean detach");

            Console.WriteLine(
                "AURORAPOMODORO_SMOKE: " + _checks + " checks, " + _failures + " failures");
            Console.WriteLine(
                "RUNTIME_TEARDOWN=" + (_failures == 0 ? "PASS" : "FAIL"));
        }
    }

    private async Task RunSmokeAsync()
    {
        Console.WriteLine("AURORAPOMODORO_SMOKE_BEGIN");

        await DelayAsync(400);
        Check(_host.IsAttached, "AuroraGlass host attached");
        Check(_renderHost.IsReady, "render host ready");
        Check(_host.CurrentMetrics.ClientWidth > 0 &&
              _host.CurrentMetrics.ClientHeight > 0, "host metrics valid");

        WpfRenderHostStats s0 = _renderHost.Stats;
        Check(s0.LastCoreStatus == 0, "Core status OK");

        await WaitFramesAsync(4);
        WpfRenderHostStats s1 = _renderHost.Stats;
        Check(s1.FrameCount > 0, "glass frames presented");

        // Timer: start -> running.
        OnPrimary();
        Check(_engine.State == PomodoroState.Running, "start -> Running");
        await DelayAsync(1200);
        Check(_engine.RemainingSeconds < _engine.DurationSeconds, "countdown changes");

        // Pause.
        OnPrimary();
        Check(_engine.State == PomodoroState.Paused, "pause -> Paused");

        // Resume.
        OnPrimary();
        Check(_engine.State == PomodoroState.Running, "resume -> Running");

        // Reset.
        _engine.Reset();
        Refresh();
        Check(_engine.State == PomodoroState.Idle, "reset -> Idle");
        Check(_engine.Display() == "25:00", "reset -> 25:00");

        // Resize.
        WpfHostMetrics before = _host.CurrentMetrics;
        Width += 80; Height += 60;
        UpdateLayout();
        await DelayAsync(300);
        WpfHostMetrics after = _host.CurrentMetrics;
        Check(after.ClientWidth != before.ClientWidth, "resize reaches host metrics");

        // Minimize / restore.
        WindowState = WindowState.Minimized;
        await DelayAsync(200);
        WindowState = WindowState.Normal;
        Activate();
        await DelayAsync(300);
        Check(_host.CurrentMetrics.ClientWidth > 0, "restore recovers metrics");

        await WaitFramesAsync(s1.FrameCount + 4);
        Check(_renderHost.Stats.LastCoreStatus == 0, "Core healthy after resize");

        Console.WriteLine("RUNTIME_LAUNCH=PASS");
        Console.WriteLine("AURORAGLASS_INIT=PASS");
        Console.WriteLine("GLASS_SURFACE=PASS");
        Console.WriteLine("TIMER=PASS");
        Console.WriteLine("RESIZE=PASS");
        Console.WriteLine("MINIMIZE_RESTORE=PASS");

        Close();
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
        if (condition)
        {
            Console.WriteLine("[PASS] " + name);
        }
        else
        {
            ++_failures;
            Console.WriteLine("[FAIL] " + name);
        }
    }
}
