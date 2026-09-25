using AuroraPomodoro;

int failures = 0;
void Check(bool ok, string name)
{
    Console.WriteLine((ok ? "[PASS] " : "[FAIL] ") + name);
    if (!ok) failures++;
}

// Use short durations so cycles are fast.
const int F = 2, S = 1, L = 2;

// Defaults: Focus 25:00.
{
    var e = new TimerEngine();
    Check(e.Mode == SessionMode.Focus, "default mode Focus");
    Check(e.State == TimerState.Idle, "default state Idle");
    Check(e.Display() == "25:00", "default display 25:00");
    Check(e.CompletedFocusSessions == 0, "default completed=0");
}

// Focus completion -> ShortBreak.
{
    var e = new TimerEngine(F, S, L);
    e.Start();
    e.Tick(F);
    Check(e.Mode == SessionMode.ShortBreak, "focus complete -> ShortBreak");
    Check(e.CompletedFocusSessions == 1, "focus counted once");
    Check(e.State == TimerState.Idle, "post-complete Idle");
    Check(e.Display() == "00:01", "break duration = ShortBreak");
}

// ShortBreak completion -> next Focus.
{
    var e = new TimerEngine(F, S, L);
    e.Start(); e.Tick(F);          // focus1 -> shortbreak
    e.Start(); e.Tick(S);          // shortbreak -> focus2
    Check(e.Mode == SessionMode.Focus, "shortbreak -> Focus");
    Check(e.CompletedFocusSessions == 1, "completed still 1");
}

// Fourth focus -> LongBreak.
{
    var e = new TimerEngine(F, S, L);
    for (int i = 0; i < 3; i++) { e.Start(); e.Tick(F); e.Start(); e.Tick(S); }
    // completed 3, in Focus #4
    Check(e.Mode == SessionMode.Focus && e.CompletedFocusSessions == 3, "before 4th focus");
    e.Start(); e.Tick(F);
    Check(e.Mode == SessionMode.LongBreak, "4th focus -> LongBreak");
    Check(e.CompletedFocusSessions == 4, "completed=4");
}

// LongBreak completion -> cycle reset to Focus #1.
{
    var e = new TimerEngine(F, S, L);
    for (int i = 0; i < 4; i++) { e.Start(); e.Tick(F); if (i < 3) { e.Start(); e.Tick(S); } }
    Check(e.Mode == SessionMode.LongBreak, "reached LongBreak");
    e.Start(); e.Tick(L);
    Check(e.Mode == SessionMode.Focus, "longbreak -> Focus");
    Check(e.CompletedFocusSessions == 0, "cycle reset completed=0");
}

// Pause: time does not advance.
{
    var e = new TimerEngine(F, S, L);
    e.Start(); e.Tick(1);
    e.Pause();
    double before = e.RemainingSeconds;
    e.Tick(1);
    Check(e.RemainingSeconds == before, "paused tick no-op");
}

// Resume continues.
{
    var e = new TimerEngine(10, 10, 10);
    e.Start(); e.Tick(1); e.Pause(); e.Resume();
    double before = e.RemainingSeconds;
    Check(e.State == TimerState.Running, "resume -> Running");
    e.Tick(1);
    Check(e.RemainingSeconds < before, "resume continues");
}

// Reset resets current mode only.
{
    var e = new TimerEngine(5, 5, 5);
    e.Start(); e.Tick(5);           // -> ShortBreak (5s), completed=1
    e.Start(); e.Tick(1);           // shortbreak remaining = 4
    e.Reset();
    Check(e.Mode == SessionMode.ShortBreak, "reset keeps mode");
    Check(e.CompletedFocusSessions == 1, "reset keeps completed");
    Check(e.Display() == "00:05", "reset restores current duration");
    Check(e.State == TimerState.Idle, "reset -> Idle");
}

// Skip Focus does not increment completed.
{
    var e = new TimerEngine(F, S, L);
    e.Skip();
    Check(e.Mode == SessionMode.ShortBreak, "skip focus -> ShortBreak");
    Check(e.CompletedFocusSessions == 0, "skip focus does NOT count");
}

// Skip Break moves to next Focus.
{
    var e = new TimerEngine(F, S, L);
    e.Skip();       // -> shortbreak
    e.Skip();       // -> focus
    Check(e.Mode == SessionMode.Focus, "skip break -> Focus");
    Check(e.CompletedFocusSessions == 0, "skip break keeps count");
}

// ResetCycle -> Focus #1 / 25:00 (using defaults).
{
    var e = new TimerEngine(F, S, L);
    e.Start(); e.Tick(F); e.Start(); e.Tick(S); e.Start(); e.Tick(F);
    e.ResetCycle();
    Check(e.Mode == SessionMode.Focus, "resetcycle -> Focus");
    Check(e.CompletedFocusSessions == 0, "resetcycle completed=0");
    Check(e.State == TimerState.Idle, "resetcycle Idle");
    Check(e.Display() == "00:02", "resetcycle duration = focus (short test)");
}

// Presentation mappings.
{
    Check(TimerPresentation.ModeTitle(SessionMode.Focus) == "Focus", "title Focus");
    Check(TimerPresentation.ModeTitle(SessionMode.ShortBreak) == "Short Break", "title Short Break");
    Check(TimerPresentation.ModeTitle(SessionMode.LongBreak) == "Long Break", "title Long Break");

    Check(TimerPresentation.PrimaryButtonText(TimerState.Idle) == "Start", "btn Idle=Start");
    Check(TimerPresentation.PrimaryButtonText(TimerState.Running) == "Pause", "btn Running=Pause");
    Check(TimerPresentation.PrimaryButtonText(TimerState.Paused) == "Resume", "btn Paused=Resume");

    var e = new TimerEngine(2, 1, 2);
    Check(TimerPresentation.ProgressText(e) == "Focus 1 / 4", "progress focus 1");
    e.Start(); e.Tick(2);   // -> ShortBreak
    Check(TimerPresentation.ProgressText(e) == "Next Focus: 2 / 4", "progress next focus 2");
}

// --- Controller / notification behaviour ---

// Natural Focus completion notifies.
{
    var sink = new RecordingSink();
    var c = new PomodoroController(sink, new TimerEngine(2, 1, 2));
    c.PrimaryAction();          // start
    c.Tick(2);                  // focus completes
    Check(sink.Events.Count == 1, "focus completion notifies once");
    Check(sink.Events[0].Title == "Focus complete", "focus notify title");
    Check(sink.Events[0].Body == "Time for a short break.", "focus notify body short");
}

// Fourth Focus completion -> long break notification.
{
    var sink = new RecordingSink();
    var c = new PomodoroController(sink, new TimerEngine(1, 1, 1));
    for (int i = 0; i < 4; i++)
    {
        c.PrimaryAction(); c.Tick(1);   // focus i completes -> break
        if (i < 3) { c.PrimaryAction(); c.Tick(1); } // break completes -> next focus
    }
    var last = sink.Events[^1];
    Check(last.Title == "Focus complete", "4th focus notify title");
    Check(last.Body == "Time for a long break.", "4th focus notify body long");
}

// Break completion notifies.
{
    var sink = new RecordingSink();
    var c = new PomodoroController(sink, new TimerEngine(1, 1, 1));
    c.PrimaryAction(); c.Tick(1);   // focus -> short break (+notify)
    int after = sink.Events.Count;
    c.PrimaryAction(); c.Tick(1);   // short break -> focus (+notify)
    Check(sink.Events.Count == after + 1, "break completion notifies");
    Check(sink.Events[^1].Title == "Break complete", "break notify title");
    Check(sink.Events[^1].Body == "Ready for the next focus session.", "break notify body");
}

// Skip -> no notification.
{
    var sink = new RecordingSink();
    var c = new PomodoroController(sink, new TimerEngine(1, 1, 1));
    c.Skip();
    Check(sink.Events.Count == 0, "skip produces no notification");
}

// Reset -> no notification.
{
    var sink = new RecordingSink();
    var c = new PomodoroController(sink, new TimerEngine(1, 1, 1));
    c.PrimaryAction(); c.Tick(0.5); c.Reset();
    Check(sink.Events.Count == 0, "reset produces no notification");
}

// Tray-style actions delegate to the same timer state.
{
    var c = new PomodoroController(null, new TimerEngine(5, 5, 5));
    c.PrimaryAction();          // Start
    Check(c.Engine.State == TimerState.Running, "tray primary start");
    c.PrimaryAction();          // Pause
    Check(c.Engine.State == TimerState.Paused, "tray primary pause");
    c.PrimaryAction();          // Resume
    Check(c.Engine.State == TimerState.Running, "tray primary resume");
    c.Tick(1);
    c.Reset();
    Check(c.Engine.State == TimerState.Idle, "tray reset -> Idle");
    Check(c.Engine.RemainingSeconds == c.Engine.CurrentDurationSeconds, "tray reset restores duration");
}

Console.WriteLine("TIMER_ENGINE_TESTS: " + (failures == 0 ? "PASS" : "FAIL"));
return failures == 0 ? 0 : 1;

sealed class RecordingSink : INotificationSink
{
    public List<(string Title, string Body)> Events { get; } = new();
    public void Notify(string title, string body) => Events.Add((title, body));
}
