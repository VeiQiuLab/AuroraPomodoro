namespace AuroraPomodoro;

/// <summary>
/// Pure UI mapping helpers (no WPF types), so they are unit-testable.
/// </summary>
public static class TimerPresentation
{
    public static string ModeTitle(SessionMode mode) => mode switch
    {
        SessionMode.Focus => "Focus",
        SessionMode.ShortBreak => "Short Break",
        SessionMode.LongBreak => "Long Break",
        _ => "Focus",
    };

    public static string PrimaryButtonText(TimerState state) => state switch
    {
        TimerState.Idle => "Start",
        TimerState.Running => "Pause",
        TimerState.Paused => "Resume",
        _ => "Start",
    };

    public static string ProgressText(TimerEngine engine)
    {
        int n = Math.Min(
            engine.Mode == SessionMode.Focus
                ? engine.CurrentFocusNumber
                : engine.NextFocusNumber,
            TimerEngine.FocusesPerCycle);

        return engine.Mode == SessionMode.Focus
            ? $"Focus {n} / {TimerEngine.FocusesPerCycle}"
            : $"Next Focus: {n} / {TimerEngine.FocusesPerCycle}";
    }

    /// <summary>Notification text for a natural completion, or null if none.</summary>
    public static (string Title, string Body)? CompletionNotification(TimerEngine.Completion c)
    {
        return c.CompletedMode switch
        {
            SessionMode.Focus when c.NextMode == SessionMode.LongBreak =>
                ("Focus complete", "Time for a long break."),
            SessionMode.Focus =>
                ("Focus complete", "Time for a short break."),
            SessionMode.ShortBreak or SessionMode.LongBreak =>
                ("Break complete", "Ready for the next focus session."),
            _ => null,
        };
    }

    /// <summary>Accent ARGB per session mode (subtle differentiation).</summary>
    public static (byte A, byte R, byte G, byte B) Accent(SessionMode mode) => mode switch
    {
        SessionMode.Focus => (255, 150, 200, 255),      // cool blue
        SessionMode.ShortBreak => (255, 150, 230, 190), // mint green
        SessionMode.LongBreak => (255, 235, 195, 150),  // warm amber
        _ => (255, 150, 200, 255),
    };
}
