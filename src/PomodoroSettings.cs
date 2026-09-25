using System.Text.Json.Serialization;

namespace AuroraPomodoro;

/// <summary>
/// Persistent user preferences. Never contains TimerState/SessionMode.
/// All values are validated/clamped on load and save.
/// </summary>
public sealed class PomodoroSettings
{
    public const int SchemaVersionCurrent = 1;

    public const int FocusMin = 1, FocusMax = 180;
    public const int ShortBreakMin = 1, ShortBreakMax = 60;
    public const int LongBreakMin = 1, LongBreakMax = 120;

    public int SchemaVersion { get; set; } = SchemaVersionCurrent;
    public int FocusMinutes { get; set; } = 25;
    public int ShortBreakMinutes { get; set; } = 5;
    public int LongBreakMinutes { get; set; } = 15;
    public bool NotificationsEnabled { get; set; } = true;

    public static PomodoroSettings CreateDefault() => new();

    /// <summary>Clamp every field into its legal range; never throws.</summary>
    public void Sanitize()
    {
        if (SchemaVersion <= 0) SchemaVersion = SchemaVersionCurrent;
        FocusMinutes = Clamp(FocusMinutes, FocusMin, FocusMax, 25);
        ShortBreakMinutes = Clamp(ShortBreakMinutes, ShortBreakMin, ShortBreakMax, 5);
        LongBreakMinutes = Clamp(LongBreakMinutes, LongBreakMin, LongBreakMax, 15);
    }

    public PomodoroSettings Clone() => new()
    {
        SchemaVersion = SchemaVersion,
        FocusMinutes = FocusMinutes,
        ShortBreakMinutes = ShortBreakMinutes,
        LongBreakMinutes = LongBreakMinutes,
        NotificationsEnabled = NotificationsEnabled,
    };

    public static int Clamp(int value, int min, int max, int fallback)
    {
        // Non-positive / absurd values fall back; otherwise clamp into range.
        if (value <= 0) return fallback;
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }
}
