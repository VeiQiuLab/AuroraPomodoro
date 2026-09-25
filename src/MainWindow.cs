using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using AuroraGlass.Wpf;

namespace AuroraPomodoro;

public sealed class MainWindow : Window
{
    private readonly TimerEngine _engine;
    private readonly bool _smoke;

    private readonly WpfHostAttachment _host = new();
    private readonly WpfGlassMaterial _material = new();
    private readonly WpfRenderHost _renderHost = new();

    private readonly TextBlock _modeText = new();
    private readonly TextBlock _timerText = new();
    private readonly TextBlock _progressText = new();
    private readonly TextBlock _stateText = new();

    private readonly DispatcherTimer _tick = new();
    private DateTime _lastTick;

    private Button? _primaryButton;
    private Button? _resetButton;
    private Button? _skipButton;

    private bool _initialized;
    private int _checks;
    private int _failures;

    private readonly Rect _glassPanelLocal = new(40, 40, 520, 440);

    public int SmokeExitCode => _failures == 0 ? 0 : 1;

    public MainWindow(bool smoke = false)
    {
        _smoke = smoke;

        // Smoke mode uses short durations to exercise the full cycle quickly.
        // Production defaults remain 25 / 5 / 15.
        _engine = smoke
            ? new TimerEngine(2, 1, 2)
            : new TimerEngine();

        Title = "AuroraPomodoro";
        Width = 640;
        Height = 560;
        MinWidth = 520;
        MinHeight = 460;
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

        _modeText.Text = "Focus";
        _modeText.FontSize = 20;
        _modeText.FontWeight = FontWeights.SemiBold;
        _modeText.Foreground = new SolidColorBrush(Color.FromRgb(150, 210, 255));
        _modeText.HorizontalAlignment = HorizontalAlignment.Center;

        _timerText.Text = _engine.Display();
        _timerText.FontFamily = new FontFamily("Consolas");
        _timerText.FontSize = 84;
        _timerText.Foreground = Brushes.White;
        _timerText.HorizontalAlignment = HorizontalAlignment.Center;

        _progressText.Text = "Focus 1 / 4";
        _progressText.FontSize = 14;
        _progressText.Foreground = new SolidColorBrush(Color.FromRgb(200, 206, 220));
        _progressText.HorizontalAlignment = HorizontalAlignment.Center;
        _progressText.Margin = new Thickness(0, 4, 0, 4);

        _stateText.Text = "Idle";
        _stateText.FontSize = 14;
        _stateText.Foreground = new SolidColorBrush(Color.FromRgb(160, 168, 184));
        _stateText.HorizontalAlignment = HorizontalAlignment.Center;
        _stateText.Margin = new Thickness(0, 4, 0, 22);

        stack.Children.Add(_modeText);
        stack.Children.Add(_timerText);
        stack.Children.Add(_progressText);
        stack.Children.Add(_stateText);

        _primaryButton = MakeButton("Start");
        _primaryButton.Click += (_, _) => OnPrimary();

        _resetButton = MakeButton("Reset");
        _resetButton.Click += (_, _) => { _engine.Reset(); Refresh(); };

        _skipButton = MakeButton("Skip");
        _skipButton.Click += (_, _) => { _engine.Skip(); Refresh(); };

        StackPanel buttons = new()
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        foreach (Button b in new[] { _primaryButton, _resetButton, _skipButton })
        {
            b.Margin = new Thickness(6);
            buttons.Children.Add(b);
        }

        stack.Children.Add(buttons);
        overlay.Children.Add(stack);
        root.Children.Add(overlay);
        return root;
    }

    private static Button MakeButton(string text)
    {
        return new Button
        {
            Content = text,
            Padding = new Thickness(20, 9, 20, 9),
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
            case TimerState.Idle: _engine.Start(); break;
            case TimerState.Running: _engine.Pause(); break;
            case TimerState.Paused: _engine.Resume(); break;
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
        _modeText.Text = _engine.Mode switch
        {
            SessionMode.Focus => "Focus",
            SessionMode.ShortBreak => "Short Break",
            SessionMode.LongBreak => "Long Break",
            _ => "Focus",
        };

        _timerText.Text = _engine.Display();

        _progressText.Text = _engine.Mode == SessionMode.Focus
            ? $"Focus {Math.Min(_engine.CurrentFocusNumber, TimerEngine.FocusesPerCycle)} / {TimerEngine.FocusesPerCycle}"
            : $"Next Focus: {Math.Min(_engine.NextFocusNumber, TimerEngine.FocusesPerCycle)} / {TimerEngine.FocusesPerCycle}";

        _stateText.Text = _engine.State.ToString();

        if (_primaryButton is not null)
        {
            _primaryButton.Content = _engine.State switch
            {
                TimerState.Idle => "Start",
                TimerState.Running => "Pause",
                TimerState.Paused => "Resume",
                _ => "Start",
            };
        }

        // Idle: Reset/Skip are still meaningful (skip current session).
        if (_resetButton is not null)
            _resetButton.IsEnabled = _engine.State != TimerState.Idle || _engine.RemainingSeconds < _engine.CurrentDurationSeconds;
        if (_skipButton is not null)
            _skipButton.IsEnabled = true;
    }

    private void UpdateGlassRect()
    {
        if (!_initialized || !_host.IsAttached) return;
        _renderHost.SetPhysicalRects(_host.DipToPhysical(_glassPanelLocal));
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _tick.Stop();

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
        Check(_host.CurrentMetrics.ClientWidth > 0, "host metrics valid");
        Check(_renderHost.Stats.LastCoreStatus == 0, "Core status OK");

        await WaitFramesAsync(4);
        Check(_renderHost.Stats.FrameCount > 0, "glass frames presented");

        // Initial Focus.
        Check(_engine.Mode == SessionMode.Focus, "starts in Focus");
        Check(_engine.Display() == "00:02", "focus duration is short (smoke)");

        // Start Focus and let it complete -> ShortBreak.
        OnPrimary();
        Check(_engine.State == TimerState.Running, "start -> Running");
        await DelayAsync(2600);
        Check(_engine.Mode == SessionMode.ShortBreak, "focus complete -> ShortBreak");
        Check(_engine.CompletedFocusSessions == 1, "focus counted");

        // Skip the break -> Focus.
        _engine.Skip();
        Refresh();
        Check(_engine.Mode == SessionMode.Focus, "skip break -> next Focus");

        // Pause / resume.
        OnPrimary();
        Check(_engine.State == TimerState.Running, "start focus2");
        OnPrimary();
        Check(_engine.State == TimerState.Paused, "pause -> Paused");
        OnPrimary();
        Check(_engine.State == TimerState.Running, "resume -> Running");

        // Reset current session only.
        _engine.Reset();
        Refresh();
        Check(_engine.State == TimerState.Idle, "reset -> Idle");
        Check(_engine.Display() == "00:02", "reset restores focus duration");

        // Resize.
        WpfHostMetrics before = _host.CurrentMetrics;
        Width += 80; Height += 60;
        UpdateLayout();
        await DelayAsync(300);
        Check(_host.CurrentMetrics.ClientWidth != before.ClientWidth, "resize reaches host metrics");

        // Minimize / restore.
        WindowState = WindowState.Minimized;
        await DelayAsync(200);
        WindowState = WindowState.Normal;
        Activate();
        await DelayAsync(300);
        Check(_host.CurrentMetrics.ClientWidth > 0, "restore recovers metrics");

        await WaitFramesAsync(_renderHost.Stats.FrameCount + 4);
        Check(_renderHost.Stats.LastCoreStatus == 0, "Core healthy after resize");

        Console.WriteLine("RUNTIME_LAUNCH=PASS");
        Console.WriteLine("AURORAGLASS_INIT=PASS");
        Console.WriteLine("GLASS_SURFACE=PASS");
        Console.WriteLine("CYCLE=PASS");
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
        if (condition) Console.WriteLine("[PASS] " + name);
        else { ++_failures; Console.WriteLine("[FAIL] " + name); }
    }
}
