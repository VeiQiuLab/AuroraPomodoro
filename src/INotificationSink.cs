namespace AuroraPomodoro;

/// <summary>Thin notification boundary so completion events are testable.</summary>
public interface INotificationSink
{
    void Notify(string title, string body);
}
