using System.Drawing;
using System.Windows.Forms;

namespace AuroraPomodoro;

/// <summary>
/// System tray icon + context menu. Delegates all actions to the controller.
/// The tray never owns timer state.
/// </summary>
public sealed class TrayIcon : IDisposable
{
    private readonly NotifyIcon _icon;
    private readonly ToolStripMenuItem _primaryItem;
    private readonly PomodoroController _controller;
    private readonly Action _openAction;
    private readonly Action _exitAction;
    private readonly Action? _settingsAction;

    public TrayIcon(
        PomodoroController controller,
        Action openAction,
        Action exitAction,
        Action? settingsAction = null)
    {
        _controller = controller;
        _openAction = openAction;
        _exitAction = exitAction;
        _settingsAction = settingsAction;

        ContextMenuStrip menu = new();

        ToolStripMenuItem open = new("Open");
        open.Click += (_, _) => _openAction();
        menu.Items.Add(open);

        menu.Items.Add(new ToolStripSeparator());

        _primaryItem = new ToolStripMenuItem("Start");
        _primaryItem.Click += (_, _) => _controller.PrimaryAction();
        menu.Items.Add(_primaryItem);

        ToolStripMenuItem reset = new("Reset");
        reset.Click += (_, _) => _controller.Reset();
        menu.Items.Add(reset);

        menu.Items.Add(new ToolStripSeparator());

        if (_settingsAction is not null)
        {
            ToolStripMenuItem settings = new("Settings");
            settings.Click += (_, _) => _settingsAction();
            menu.Items.Add(settings);
        }

        ToolStripMenuItem exit = new("Exit");
        exit.Click += (_, _) => _exitAction();
        menu.Items.Add(exit);

        _icon = new NotifyIcon
        {
            Icon = CreateIcon(),
            Text = "AuroraPomodoro",
            ContextMenuStrip = menu,
            Visible = true,
        };
        _icon.DoubleClick += (_, _) => _openAction();

        _controller.Changed += SyncPrimaryText;
        SyncPrimaryText();
    }

    private void SyncPrimaryText()
    {
        _primaryItem.Text = TimerPresentation.PrimaryButtonText(_controller.Engine.State);
    }

    /// <summary>Show a tray balloon (used as the stable notification path).</summary>
    public void Balloon(string title, string body)
    {
        _icon.BalloonTipTitle = title;
        _icon.BalloonTipText = body;
        _icon.ShowBalloonTip(4000);
    }

    /// <summary>Simple project-created 16x16 icon (a filled rounded square).</summary>
    private static Icon CreateIcon()
    {
        using Bitmap bmp = new(16, 16);
        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            using SolidBrush bg = new(Color.FromArgb(255, 58, 110, 190));
            g.FillEllipse(bg, 1, 1, 14, 14);
            using SolidBrush fg = new(Color.White);
            g.FillRectangle(fg, 7, 4, 2, 5);
            g.FillRectangle(fg, 7, 10, 2, 2);
        }
        IntPtr h = bmp.GetHicon();
        try { return Icon.FromHandle(h); }
        finally { /* handle released by Icon copy semantics on dispose path */ }
    }

    public void Dispose()
    {
        _controller.Changed -= SyncPrimaryText;
        _icon.Visible = false;
        _icon.Dispose();
    }
}
