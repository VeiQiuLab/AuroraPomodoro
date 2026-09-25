namespace AuroraPomodoro;

public enum SessionMode
{
    Focus,
    ShortBreak,
    LongBreak,
}

public enum TimerState
{
    Idle,
    Running,
    Paused,
}

/// <summary>
/// Pure, testable Pomodoro cycle state machine.
/// No UI, no AuroraGlass, no wall-clock — the host drives Tick(seconds).
/// SessionMode and TimerState are deliberately separate concepts.
/// Durations come from configuration and can be updated at runtime.
/// </summary>
public sealed class TimerEngine
{
    public const int DefaultFocusSeconds = 25 * 60;
    public const int DefaultShortBreakSeconds = 5 * 60;
    public const int DefaultLongBreakSeconds = 15 * 60;
    public const int FocusesPerCycle = 4;

    private int _focusSeconds;
    private int _shortBreakSeconds;
    private int _longBreakSeconds;

    /// <summary>Describes a natural session completion (from Tick, not Skip).</summary>
    public readonly record struct Completion(SessionMode CompletedMode, SessionMode NextMode);

    public SessionMode Mode { get; private set; } = SessionMode.Focus;
    public TimerState State { get; private set; } = TimerState.Idle;

    /// <summary>
    /// Set only by a natural completion during the most recent Tick; null otherwise.
    /// Skip and Reset never set this.
    /// </summary>
    public Completion? LastTickCompletion { get; private set; }

    /// <summary>Focus sessions completed in the current cycle (0..4).</summary>
    public int CompletedFocusSessions { get; private set; }

    public double RemainingSeconds { get; private set; }

    public TimerEngine(
        int focusSeconds = DefaultFocusSeconds,
        int shortBreakSeconds = DefaultShortBreakSeconds,
        int longBreakSeconds = DefaultLongBreakSeconds)
    {
        if (focusSeconds <= 0) throw new ArgumentOutOfRangeException(nameof(focusSeconds));
        if (shortBreakSeconds <= 0) throw new ArgumentOutOfRangeException(nameof(shortBreakSeconds));
        if (longBreakSeconds <= 0) throw new ArgumentOutOfRangeException(nameof(longBreakSeconds));

        _focusSeconds = focusSeconds;
        _shortBreakSeconds = shortBreakSeconds;
        _longBreakSeconds = longBreakSeconds;
        RemainingSeconds = _focusSeconds;
    }

    public int CurrentDurationSeconds => Mode switch
    {
        SessionMode.Focus => _focusSeconds,
        SessionMode.ShortBreak => _shortBreakSeconds,
        SessionMode.LongBreak => _longBreakSeconds,
        _ => _focusSeconds,
    };

    /// <summary>Current focus index for display: 1..4 while in Focus mode.</summary>
    public int CurrentFocusNumber => CompletedFocusSessions + 1;

    /// <summary>Next focus index for display during a break: 1..4.</summary>
    public int NextFocusIndex => CompletedFocusSessions + 1;

    /// <summary>
    /// Apply new configured durations.
    /// - Running / Paused: the current session's remaining time is preserved;
    ///   only future sessions use the new durations.
    /// - Idle: the current (not-yet-started) session refreshes to its new duration.
    /// </summary>
    public void ApplySettings(PomodoroSettings settings)
    {
        _focusSeconds = settings.FocusMinutes * 60;
        _shortBreakSeconds = settings.ShortBreakMinutes * 60;
        _longBreakSeconds = settings.LongBreakMinutes * 60;

        if (State == TimerState.Idle)
        {
            RemainingSeconds = CurrentDurationSeconds;
        }
        // Running / Paused: keep RemainingSeconds untouched.
    }

    public void Start()
    {
        if (State != TimerState.Idle) return;
        State = TimerState.Running;
    }

    public void Pause()
    {
        if (State != TimerState.Running) return;
        State = TimerState.Paused;
    }

    public void Resume()
    {
        if (State != TimerState.Paused) return;
        State = TimerState.Running;
    }

    /// <summary>Reset the CURRENT session only (mode and cycle count unchanged).</summary>
    public void Reset()
    {
        State = TimerState.Idle;
        RemainingSeconds = CurrentDurationSeconds;
        LastTickCompletion = null;
    }

    /// <summary>Reset the entire cycle back to Focus #1.</summary>
    public void ResetCycle()
    {
        Mode = SessionMode.Focus;
        CompletedFocusSessions = 0;
        State = TimerState.Idle;
        RemainingSeconds = _focusSeconds;
        LastTickCompletion = null;
    }

    /// <summary>Manually skip the current session. Skip is NOT completion.</summary>
    public void Skip()
    {
        switch (Mode)
        {
            case SessionMode.Focus:
                // Skip does not increment the completed focus count.
                Mode = SessionMode.ShortBreak;
                break;
            case SessionMode.ShortBreak:
                Mode = SessionMode.Focus;
                break;
            case SessionMode.LongBreak:
                CompletedFocusSessions = 0;
                Mode = SessionMode.Focus;
                break;
        }
        State = TimerState.Idle;
        RemainingSeconds = CurrentDurationSeconds;
        LastTickCompletion = null;
    }

    /// <summary>Advance the timer. Returns true if a value changed.</summary>
    public bool Tick(double deltaSeconds)
    {
        LastTickCompletion = null;
        if (State != TimerState.Running || deltaSeconds <= 0.0) return false;

        RemainingSeconds -= deltaSeconds;
        if (RemainingSeconds <= 0.0)
        {
            RemainingSeconds = 0.0;
            CompleteCurrent();
        }
        return true;
    }

    private void CompleteCurrent()
    {
        State = TimerState.Idle;
        SessionMode completed = Mode;

        switch (Mode)
        {
            case SessionMode.Focus:
                CompletedFocusSessions++;
                Mode = CompletedFocusSessions >= FocusesPerCycle
                    ? SessionMode.LongBreak
                    : SessionMode.ShortBreak;
                break;
            case SessionMode.ShortBreak:
                Mode = SessionMode.Focus;
                break;
            case SessionMode.LongBreak:
                CompletedFocusSessions = 0;
                Mode = SessionMode.Focus;
                break;
        }

        RemainingSeconds = CurrentDurationSeconds;
        LastTickCompletion = new Completion(completed, Mode);
    }

    public string Display()
    {
        int total = (int)Math.Ceiling(RemainingSeconds);
        if (total < 0) total = 0;
        return $"{total / 60:00}:{total % 60:00}";
    }
}
