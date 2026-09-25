using AuroraPomodoro;

int failures = 0;
void Check(bool ok, string name)
{
    Console.WriteLine((ok ? "[PASS] " : "[FAIL] ") + name);
    if (!ok) failures++;
}

// Initial state.
var e = new TimerEngine();
Check(e.State == PomodoroState.Idle, "initial Idle");
Check(e.Display() == "25:00", "initial display 25:00");

// Idle -> Start -> Running.
e.Start();
Check(e.State == PomodoroState.Running, "start -> Running");

// Tick decreases display.
e.Tick(60);
Check(e.Display() == "24:00", "tick 60s -> 24:00");

// Running -> Pause -> Paused; tick does not advance.
e.Pause();
Check(e.State == PomodoroState.Paused, "pause -> Paused");
e.Tick(60);
Check(e.Display() == "24:00", "paused tick no-op");

// Paused -> Resume -> Running.
e.Resume();
Check(e.State == PomodoroState.Running, "resume -> Running");
e.Tick(60);
Check(e.Display() == "23:00", "resume tick continues");

// Reset -> Idle + 25:00.
e.Reset();
Check(e.State == PomodoroState.Idle, "reset -> Idle");
Check(e.Display() == "25:00", "reset -> 25:00");

// Run to completion.
e.Start();
e.Tick(25 * 60);
Check(e.State == PomodoroState.Idle, "completion -> Idle");
Check(e.Display() == "00:00", "completion -> 00:00");

// Short-duration engine.
var s = new TimerEngine(3);
s.Start();
s.Tick(1);
Check(s.Display() == "00:02", "short duration tick");

Console.WriteLine("TIMER_ENGINE_TESTS: " + (failures == 0 ? "PASS" : "FAIL"));
return failures == 0 ? 0 : 1;
