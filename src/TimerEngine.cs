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
/// </summary>
public sealed class TimerEngine
{
    public const int DefaultFocusSeconds = 25 * 60;
    public const int DefaultShortBreakSeconds = 5 * 60;
    public const int DefaultLongBreakSeconds = 15 * 60;
    public const int FocusesPerCycle = 4;

    private readonly int _focusSeconds;
    private readonly int _shortBreakSeconds;
    private readonly int _longBreakSeconds;

    public SessionMode Mode { get; private set; } = SessionMode.Focus;
    public TimerState State { get; private set; } = TimerState.Idle;

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
    public int NextFocusNumber => CompletedFocusSessions + 1;

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
    }

    /// <summary>Reset the entire cycle back to Focus #1.</summary>
    public void ResetCycle()
    {
        Mode = SessionMode.Focus;
        CompletedFocusSessions = 0;
        State = TimerState.Idle;
        RemainingSeconds = _focusSeconds;
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
    }

    /// <summary>Advance the timer. Returns true if a value changed.</summary>
    public bool Tick(double deltaSeconds)
    {
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
    }

    public string Display()
    {
        int total = (int)Math.Ceiling(RemainingSeconds);
        if (total < 0) total = 0;
        return $"{total / 60:00}:{total % 60:00}";
    }
}
