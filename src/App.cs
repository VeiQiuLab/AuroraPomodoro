using System.Windows;

namespace AuroraPomodoro;

public static class App
{
    [STAThread]
    public static int Main(string[] args)
    {
        bool smoke = Array.Exists(args,
            a => string.Equals(a, "--smoke", StringComparison.OrdinalIgnoreCase));

        Application app = new()
        {
            ShutdownMode = ShutdownMode.OnMainWindowClose
        };

        MainWindow window = new(smoke);
        app.MainWindow = window;
        window.Show();

        int result = app.Run();
        return smoke ? window.SmokeExitCode : result;
    }
}
