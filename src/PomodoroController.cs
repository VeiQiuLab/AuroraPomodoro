namespace AuroraPomodoro;

/// <summary>
/// Shared product logic used by the main window, settings window, and tray menu.
/// Owns the single TimerEngine instance and the current settings, and forwards
/// natural completions to a notification sink (when notifications are enabled).
/// Skip/Reset never notify.
/// </summary>
public sealed class PomodoroController
{
    private readonly SettingsStore _store;
    private PomodoroSettings _settings;

    /// <summary>Notification sink. Settable so the tray can be wired after construction.</summary>
    public INotificationSink? Sink { get; set; }

    public TimerEngine Engine { get; }

    /// <summary>The current (already-applied) settings. Treat as read-only.</summary>
    public PomodoroSettings Settings => _settings;

    /// <summary>Raised after any state/mode change so the UI can refresh.</summary>
    public event Action? Changed;

    public PomodoroController(
        INotificationSink? sink = null,
        TimerEngine? engine = null,
        SettingsStore? store = null)
    {
        Sink = sink;
        _store = store ?? new SettingsStore();
        _settings = _store.Load();

        if (engine is null)
        {
            // Production path: durations come from persisted settings.
            Engine = new TimerEngine(
                _settings.FocusMinutes * 60,
                _settings.ShortBreakMinutes * 60,
                _settings.LongBreakMinutes * 60);
        }
        else
        {
            // Test/injected path: engine supplied directly; settings do not
            // silently override the injected engine until ApplySettings runs.
            Engine = engine;
        }
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

    /// <summary>
    /// Replace the current settings, persist them, and apply to the engine.
    /// Running/Paused sessions keep their remaining time; Idle refreshes.
    /// </summary>
    public void UpdateSettings(PomodoroSettings settings)
    {
        settings.Sanitize();
        _settings = settings.Clone();
        _store.Save(_settings);
        Engine.ApplySettings(_settings);
        Changed?.Invoke();
    }

    /// <summary>Advance time. Fires a notification on natural completion (if enabled).</summary>
    public void Tick(double deltaSeconds)
    {
        if (!Engine.Tick(deltaSeconds)) return;

        if (Engine.LastTickCompletion is { } c)
        {
            var note = TimerPresentation.CompletionNotification(c);
            if (note is { } n && _settings.NotificationsEnabled)
            {
                Sink?.Notify(n.Title, n.Body);
            }
        }
        Changed?.Invoke();
    }
}
