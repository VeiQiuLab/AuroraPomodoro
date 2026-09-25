namespace AuroraPomodoro;

public enum PomodoroState
{
    Idle,
    Running,
    Paused,
}

/// <summary>
/// Pure, testable Pomodoro timer state machine.
/// No UI, no AuroraGlass, no timers — the host drives Tick(seconds).
/// </summary>
public sealed class TimerEngine
{
    public const int DefaultDurationSeconds = 25 * 60;

    private readonly int _durationSeconds;
    private double _remainingSeconds;

    public PomodoroState State { get; private set; } = PomodoroState.Idle;
    public int DurationSeconds => _durationSeconds;
    public double RemainingSeconds => _remainingSeconds;

    public TimerEngine(int durationSeconds = DefaultDurationSeconds)
    {
        if (durationSeconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(durationSeconds));
        _durationSeconds = durationSeconds;
        _remainingSeconds = durationSeconds;
    }

    public void Start()
    {
        if (State != PomodoroState.Idle) return;
        State = PomodoroState.Running;
    }

    public void Pause()
    {
        if (State != PomodoroState.Running) return;
        State = PomodoroState.Paused;
    }

    public void Resume()
    {
        if (State != PomodoroState.Paused) return;
        State = PomodoroState.Running;
    }

    public void Reset()
    {
        State = PomodoroState.Idle;
        _remainingSeconds = _durationSeconds;
    }

    /// <summary>Advance the timer by deltaSeconds. Returns true if the value changed.</summary>
    public bool Tick(double deltaSeconds)
    {
        if (State != PomodoroState.Running || deltaSeconds <= 0.0) return false;
        _remainingSeconds -= deltaSeconds;
        if (_remainingSeconds <= 0.0)
        {
            _remainingSeconds = 0.0;
            State = PomodoroState.Idle;
        }
        return true;
    }

    public string Display()
    {
        int total = (int)Math.Ceiling(_remainingSeconds);
        if (total < 0) total = 0;
        return $"{total / 60:00}:{total % 60:00}";
    }
}
