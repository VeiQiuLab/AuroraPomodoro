namespace AuroraPomodoro;

/// <summary>
/// Shared product logic used by both the main window and the tray menu.
/// Owns the single TimerEngine instance and forwards natural completions
/// to a notification sink. Skip/Reset never notify.
/// </summary>
public sealed class PomodoroController
{
    /// <summary>Notification sink. Settable so the tray can be wired after construction.</summary>
    public INotificationSink? Sink { get; set; }

    public TimerEngine Engine { get; }

    /// <summary>Raised after any state/mode change so the UI can refresh.</summary>
    public event Action? Changed;

    public PomodoroController(INotificationSink? sink = null, TimerEngine? engine = null)
    {
        Sink = sink;
        Engine = engine ?? new TimerEngine();
    }

    public void PrimaryAction()
    {
        switch (Engine.State)
        {
            case TimerState.Idle: Engine.Start(); break;
            case TimerState.Running: Engine.Pause(); break;
            case TimerState.Paused: Engine.Resume(); break;
        }
        Changed?.Invoke();
    }

    public void Reset()
    {
        Engine.Reset();
        Changed?.Invoke();
    }

    public void Skip()
    {
        Engine.Skip();
        Changed?.Invoke();
    }

    /// <summary>Advance time. Fires a notification on natural completion.</summary>
    public void Tick(double deltaSeconds)
    {
        if (!Engine.Tick(deltaSeconds)) return;

        if (Engine.LastTickCompletion is { } c)
        {
            var note = TimerPresentation.CompletionNotification(c);
            if (note is { } n)
            {
                Sink?.Notify(n.Title, n.Body);
            }
        }
        Changed?.Invoke();
    }
}
