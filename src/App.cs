using System.Windows;

namespace AuroraPomodoro;

public static class App
{
    [STAThread]
    public static int Main(string[] args)
    {
        bool smoke = HasFlag(args, "--smoke");
        string? shotDir = GetOption(args, "--shots");

        Application app = new()
        {
            ShutdownMode = ShutdownMode.OnMainWindowClose
        };

        MainWindow window = new(smoke, shotDir);
        app.MainWindow = window;
        window.Show();

        int result = app.Run();
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
