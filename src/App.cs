using System.Windows;
using Application = System.Windows.Application;

namespace AuroraPomodoro;

public static class App
{
    [STAThread]
    public static int Main(string[] args)
    {
        bool smoke = HasFlag(args, "--smoke");
        bool traySmoke = HasFlag(args, "--tray-smoke");
        string? shotDir = GetOption(args, "--shots");

        Application app = new()
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown
        };

        // Smoke modes use short durations; production stays 25/5/15.
        TimerEngine engine = (smoke || traySmoke)
            ? new TimerEngine(2, 1, 2)
            : new TimerEngine();

        PomodoroController controller = new(null, engine);

        bool guiSmoke = smoke && !traySmoke;
        MainWindow window = new(controller, guiSmoke, shotDir);
        app.MainWindow = window;

        int exitCode = 0;

        if (traySmoke)
        {
            RecordingSink sink = new();
            TrayIcon tray = new(
                controller,
                openAction: () => window.RestoreFromTray(),
                exitAction: () => window.ExitApplication());
            sink = new RecordingSink(tray);
            controller.Sink = sink;

            TraySmokeRunner runner = new(controller, window, tray, sink);
            window.Loaded += async (_, _) =>
            {
                await runner.RunAsync();
                exitCode = runner.ExitCode;
                app.Shutdown();
            };
            window.Show();
            app.Run();
            return exitCode;
        }

        TrayIcon? normalTray = null;
        if (!smoke)
        {
            normalTray = new TrayIcon(
                controller,
                openAction: () => window.RestoreFromTray(),
                exitAction: () => window.ExitApplication());
            controller.Sink = new TrayNotificationSink(normalTray);
        }

        window.Show();
        int result = app.Run();

        normalTray?.Dispose();
        return smoke ? window.SmokeExitCode : result;
    }

    private static bool HasFlag(string[] args, string name) =>
        Array.Exists(args, a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase));

    private static string? GetOption(string[] args, string name)
    {
        for (int i = 0; i + 1 < args.Length; i++)
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        return null;
    }
}

/// <summary>Routes completion notifications to the tray balloon.</summary>
internal sealed class TrayNotificationSink : INotificationSink
{
    private readonly TrayIcon _tray;
    public TrayNotificationSink(TrayIcon tray) => _tray = tray;
    public void Notify(string title, string body) => _tray.Balloon(title, body);
}
