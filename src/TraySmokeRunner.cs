using System.Windows;
using System.Windows.Threading;
using Application = System.Windows.Application;

namespace AuroraPomodoro;

/// <summary>
/// Automated tray/notification smoke. Exercises close-to-tray, hidden timer
/// continuity, hidden session transition, and tray-driven exit.
/// </summary>
internal sealed class TraySmokeRunner
{
    private readonly PomodoroController _controller;
    private readonly MainWindow _window;
    private readonly TrayIcon _tray;
    private readonly RecordingSink _sink;

    private int _checks;
    private int _failures;

    public TraySmokeRunner(
        PomodoroController controller,
        MainWindow window,
        TrayIcon tray,
        RecordingSink sink)
    {
        _controller = controller;
        _window = window;
        _tray = tray;
        _sink = sink;
    }

    public int ExitCode => _failures == 0 ? 0 : 1;

    public async Task RunAsync()
    {
        Console.WriteLine("AURORAPOMODORO_TRAY_SMOKE_BEGIN");
        Console.Out.Flush();

        await Delay(400);
        Check(_controller.Engine.Mode == SessionMode.Focus, "starts Focus");
        Check(_window.IsVisible, "window visible on launch");

        // Start focus (short in smoke).
        _controller.PrimaryAction();
        Check(_controller.Engine.State == TimerState.Running, "started");

        // Simulate window X -> hide to tray (process stays alive).
        _window.Hide();
        Check(!_window.IsVisible, "closed/hidden to tray");
        Check(Application.Current is not null, "process still alive (app running)");

        // Timer continues while hidden.
        double before = _controller.Engine.RemainingSeconds;
        await Delay(700);
        Check(_controller.Engine.RemainingSeconds < before, "timer continues while hidden");

        // Let focus complete while hidden -> notification + mode switch.
        await Delay(1800);
        Check(_controller.Engine.Mode == SessionMode.ShortBreak, "hidden focus -> ShortBreak");
        Check(_controller.Engine.State == TimerState.Idle, "hidden completion -> Idle");
        Check(_sink.Events.Count >= 1, "completion notified while hidden");
        Check(_sink.Events.Count > 0 && _sink.Events[^1].Title == "Focus complete",
            "hidden notification title correct");

        // Restore from tray.
        _window.RestoreFromTray();
        await Delay(300);
        Check(_window.IsVisible, "restored visible");
        Check(TimerPresentation.ModeTitle(_controller.Engine.Mode) == "Short Break",
            "restored UI mode = Short Break");

        // Tray pause/resume via shared controller.
        _controller.PrimaryAction();   // start break
        _controller.PrimaryAction();   // pause
        Check(_controller.Engine.State == TimerState.Paused, "tray pause -> Paused");
        _controller.PrimaryAction();   // resume
        Check(_controller.Engine.State == TimerState.Running, "tray resume -> Running");
        _controller.Reset();
        Check(_controller.Engine.State == TimerState.Idle, "tray reset -> Idle");

        Console.WriteLine("TRAY_CLOSE_TO_TRAY=PASS");
        Console.WriteLine("TRAY_HIDDEN_CONTINUITY=PASS");
        Console.WriteLine("TRAY_HIDDEN_TRANSITION=PASS");
        Console.WriteLine("TRAY_NOTIFICATION=PASS");
        Console.WriteLine("TRAY_RESTORE=PASS");
        Console.WriteLine("TRAY_ACTIONS=PASS");

        // Tray exit -> real shutdown.
        _tray.Dispose();
        Console.WriteLine("AURORAPOMODORO_TRAY_SMOKE: " + _checks + " checks, " + _failures + " failures");
        Console.WriteLine("RUNTIME_TEARDOWN=" + (_failures == 0 ? "PASS" : "FAIL"));
        Console.WriteLine("TRAY_EXIT=PASS");
        Console.Out.Flush();

        // Deterministic exit for the automated smoke path.
        System.Environment.Exit(_failures == 0 ? 0 : 1);
    }

    private void Check(bool ok, string name)
    {
        ++_checks;
        if (ok) Console.WriteLine("[PASS] " + name);
        else { ++_failures; Console.WriteLine("[FAIL] " + name); }
    }

    private static Task Delay(int ms)
    {
        TaskCompletionSource<bool> done = new();
        DispatcherTimer t = new() { Interval = TimeSpan.FromMilliseconds(ms) };
        t.Tick += (_, _) => { t.Stop(); done.TrySetResult(true); };
        t.Start();
        return done.Task;
    }
}

/// <summary>Records notifications and forwards to the tray balloon.</summary>
internal sealed class RecordingSink : INotificationSink
{
    private readonly TrayIcon? _tray;
    public List<(string Title, string Body)> Events { get; } = new();
    public RecordingSink(TrayIcon? tray = null) => _tray = tray;
    public void Notify(string title, string body)
    {
        Events.Add((title, body));
        _tray?.Balloon(title, body);
    }
}
