using System.IO.Pipes;
using System.Text;
using System.Threading;

namespace AuroraPomodoro;

/// <summary>
/// Windows single-instance guard using a named Mutex + named pipe.
/// Owns no product state (no timer, no tray, no settings).
/// - Primary: holds the mutex and listens for SHOW requests.
/// - Secondary: sends SHOW to the primary, then the caller exits.
/// </summary>
public sealed class SingleInstanceGuard : IDisposable
{
    // Local = per interactive session, the normal scope for a desktop tray app.
    private const string MutexName = @"Local\AuroraPomodoro.SingleInstance";
    private const string PipeName = "AuroraPomodoro.SingleInstance.Pipe";
    private const string ShowCommand = "SHOW";

    private Mutex? _mutex;
    private bool _isPrimary;
    private CancellationTokenSource? _cts;
    private Task? _serverTask;

    public bool IsPrimary => _isPrimary;

    /// <summary>Raised on the primary when a secondary requests the window.</summary>
    public event Action? ShowRequested;

    /// <summary>
    /// Attempts to become the primary instance. Returns true if this process is
    /// the primary. On the secondary, call SignalPrimary() and exit.
    /// </summary>
    public bool TryAcquire()
    {
        _mutex = new Mutex(initiallyOwned: false, MutexName);

        // Canonical single-instance check: try to take ownership immediately.
        // - new / abandoned mutex -> we own it -> primary
        // - already owned by a live instance -> WaitOne(0) == false -> secondary
        try
        {
            _isPrimary = _mutex.WaitOne(0, false);
        }
        catch (AbandonedMutexException)
        {
            // Prior instance crashed while holding it; we now own it.
            _isPrimary = true;
        }

        if (_isPrimary)
        {
            StartServer();
        }

        return _isPrimary;
    }

    private void StartServer()
    {
        _cts = new CancellationTokenSource();
        CancellationToken token = _cts.Token;

        _serverTask = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    using NamedPipeServerStream server = new(
                        PipeName, PipeDirection.In, 1,
                        PipeTransmissionMode.Byte, PipeOptions.Asynchronous);

                    await server.WaitForConnectionAsync(token);

                    byte[] buffer = new byte[64];
                    int read = await server.ReadAsync(buffer, token);
                    string msg = Encoding.UTF8.GetString(buffer, 0, read).Trim();

                    if (string.Equals(msg, ShowCommand, StringComparison.Ordinal))
                    {
                        ShowRequested?.Invoke();
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    // A transient pipe error should not kill the loop.
                    await Task.Delay(50, token).ContinueWith(_ => { });
                }
            }
        }, token);
    }

    /// <summary>Sends the SHOW request to the primary instance (secondary side).</summary>
    public static void SignalPrimary(int timeoutMs = 3000)
    {
        try
        {
            using NamedPipeClientStream client = new(
                ".", PipeName, PipeDirection.Out);
            client.Connect(timeoutMs);
            byte[] data = Encoding.UTF8.GetBytes(ShowCommand);
            client.Write(data, 0, data.Length);
            client.Flush();
        }
        catch
        {
            // Best-effort: if the primary is gone, nothing to signal.
        }
    }

    public void Dispose()
    {
        try { _cts?.Cancel(); } catch { }
        try { _serverTask?.Wait(500); } catch { }
        _cts?.Dispose();

        if (_mutex is not null)
        {
            if (_isPrimary)
            {
                try { _mutex.ReleaseMutex(); } catch { }
            }
            _mutex.Dispose();
            _mutex = null;
        }
    }
}
