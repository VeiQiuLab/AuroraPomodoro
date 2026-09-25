using System.IO;

namespace AuroraPomodoro;

/// <summary>
/// Automated settings smoke. Uses a temporary storage root so the real
/// %LOCALAPPDATA%\AuroraPomodoro is never touched.
/// </summary>
internal static class SettingsSmokeRunner
{
    public static int Run()
    {
        int checks = 0, failures = 0;
        void Check(bool ok, string name)
        {
            checks++;
            Console.WriteLine((ok ? "[PASS] " : "[FAIL] ") + name);
            if (!ok) failures++;
        }

        Console.WriteLine("AURORAPOMODORO_SETTINGS_SMOKE_BEGIN");
        Console.Out.Flush();

        string root = Path.Combine(Path.GetTempPath(), "AuroraPomodoroSettingsSmoke-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            // 1. First "launch": defaults persisted.
            {
                var c = new PomodoroController(null, engine: null, store: new SettingsStore(root));
                Check(c.Settings.FocusMinutes == 25, "fresh launch defaults focus 25");
                Check(c.Engine.Display() == "25:00", "fresh launch displays 25:00");
            }

            // 2. Change settings + save (simulating the dialog Save path).
            {
                var c = new PomodoroController(null, engine: null, store: new SettingsStore(root));
                c.UpdateSettings(new PomodoroSettings
                {
                    FocusMinutes = 30, ShortBreakMinutes = 6, LongBreakMinutes = 18,
                    NotificationsEnabled = false,
                });
                Check(c.Settings.FocusMinutes == 30, "settings updated focus 30");
            }

            // 3. "Restart": values restored from disk.
            {
                var c = new PomodoroController(null, engine: null, store: new SettingsStore(root));
                Check(c.Settings.FocusMinutes == 30, "restart restores focus 30");
                Check(c.Settings.ShortBreakMinutes == 6, "restart restores short 6");
                Check(c.Settings.LongBreakMinutes == 18, "restart restores long 18");
                Check(!c.Settings.NotificationsEnabled, "restart restores notifications off");
                Check(c.Engine.Display() == "30:00", "restart engine uses 30:00");
            }

            // 4. Running timer preserved on settings change; reset uses new duration.
            {
                var store = new SettingsStore(root);
                var c = new PomodoroController(null, engine: null, store: store);
                c.PrimaryAction(); c.Tick(5);
                double before = c.Engine.RemainingSeconds;
                c.UpdateSettings(c.Settings.Clone());  // same values
                Check(Math.Abs(c.Engine.RemainingSeconds - before) < 0.001, "running remaining preserved");
                c.Reset();
                Check(c.Engine.Display() == "30:00", "reset uses configured duration");
            }

            // 5. Corrupt config -> app still starts with defaults.
            {
                string corruptRoot = Path.Combine(root, "corrupt");
                Directory.CreateDirectory(corruptRoot);
                File.WriteAllText(Path.Combine(corruptRoot, "settings.json"), "}{ broken");
                var c = new PomodoroController(null, engine: null, store: new SettingsStore(corruptRoot));
                Check(c.Settings.FocusMinutes == 25, "corrupt config -> defaults (focus 25)");
                Check(c.Engine.Display() == "25:00", "corrupt config -> engine 25:00");
            }
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
        }

        Console.WriteLine("SETTINGS_SMOKE: " + checks + " checks, " + failures + " failures");
        Console.WriteLine("SETTINGS_SMOKE_RESULT=" + (failures == 0 ? "PASS" : "FAIL"));
        Console.Out.Flush();
        return failures == 0 ? 0 : 1;
    }
}
