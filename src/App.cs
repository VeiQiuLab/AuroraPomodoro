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
        bool instanceSmoke = HasFlag(args, "--instance-smoke");
        bool settingsSmoke = HasFlag(args, "--settings-smoke");
        string? shotDir = GetOption(args, "--shots");

        // Pure headless settings smoke (no window / no tray).
        if (settingsSmoke)
        {
            return SettingsSmokeRunner.Run();
        }

        // Single-instance guard applies to production and the instance smoke,
        // but NOT to the pure functional smokes (which must always run).
        bool useGuard = !smoke && !traySmoke;

        SingleInstanceGuard? guard = null;
        if (useGuard)
        {
            guard = new SingleInstanceGuard();
            bool primary = guard.TryAcquire();
            Console.WriteLine(primary ? "ROLE=PRIMARY" : "ROLE=SECONDARY");
            Console.Out.Flush();
            if (!primary)
            {
                // Secondary: wake the primary, then exit before creating any
                // product resource (no second timer/window/tray).
                SingleInstanceGuard.SignalPrimary();
                Console.WriteLine("SECONDARY_SIGNALED");
                Console.Out.Flush();
                guard.Dispose();
                return 0;
            }
        }

        Application app = new()
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown
        };

        TimerEngine engine = (smoke || traySmoke || instanceSmoke)
            ? new TimerEngine(2, 1, 2)
            : new TimerEngine();

        PomodoroController controller = new(null, engine);

        // ---- Instance smoke: primary stays hidden, secondary must restore it ----
        if (instanceSmoke)
        {
            MainWindow iw = new(controller, smoke: false, shotDir: null);
            app.MainWindow = iw;

            guard!.ShowRequested += () => app.Dispatcher.BeginInvoke(new Action(() =>
            {
                iw.RestoreFromTray();
                Console.WriteLine("SHOW_RECEIVED visible=" + iw.IsVisible);
                Console.Out.Flush();
            }));

            iw.Loaded += async (_, _) =>
            {
                iw.Hide();
                Console.WriteLine("PRIMARY_HIDDEN");
                Console.Out.Flush();

                await Task.Delay(9000);

                Console.WriteLine("PRIMARY_EXITING");
                Console.Out.Flush();
                guard.Dispose();
                iw.ExitApplication();
            };

            iw.Show();
            int irc = app.Run();
            return irc;
        }

        bool guiSmoke = smoke && !traySmoke;
        MainWindow window = new(controller, guiSmoke, shotDir);
        app.MainWindow = window;

        // Primary: restore on secondary request.
        guard?.ShowRequested += () =>
            app.Dispatcher.BeginInvoke(new Action(() => window.RestoreFromTray()));

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
                exitAction: () => window.ExitApplication(),
                settingsAction: () => window.OpenSettings());
            controller.Sink = new TrayNotificationSink(normalTray);
        }

        window.Show();
        int result = app.Run();

        normalTray?.Dispose();
        guard?.Dispose();
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
