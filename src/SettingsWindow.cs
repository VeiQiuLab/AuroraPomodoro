using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Button = System.Windows.Controls.Button;
using TextBlock = System.Windows.Controls.TextBlock;
using StackPanel = System.Windows.Controls.StackPanel;
using Grid = System.Windows.Controls.Grid;
using TextBox = System.Windows.Controls.TextBox;
using CheckBox = System.Windows.Controls.CheckBox;
using Color = System.Windows.Media.Color;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using FontFamily = System.Windows.Media.FontFamily;
using Orientation = System.Windows.Controls.Orientation;
using Brushes = System.Windows.Media.Brushes;
using VerticalAlignment = System.Windows.VerticalAlignment;

namespace AuroraPomodoro;

/// <summary>
/// Compact settings dialog. Edits a working copy; the controller remains the
/// source of truth (this window never owns configuration).
/// </summary>
public sealed class SettingsWindow : Window
{
    private readonly TextBox _focus = new();
    private readonly TextBox _short = new();
    private readonly TextBox _long = new();
    private readonly CheckBox _notifications = new();
    private readonly TextBlock _error = new();
    private Button? _saveButton;

    public PomodoroSettings? Result { get; private set; }

    public SettingsWindow(PomodoroSettings current)
    {
        Title = "Settings";
        Width = 360;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.FromRgb(24, 27, 34));

        _focus.Text = current.FocusMinutes.ToString();
        _short.Text = current.ShortBreakMinutes.ToString();
        _long.Text = current.LongBreakMinutes.ToString();
        _notifications.IsChecked = current.NotificationsEnabled;

        Content = BuildContent();
    }

    private UIElement BuildContent()
    {
        StackPanel root = new() { Margin = new Thickness(20) };

        root.Children.Add(Row("Focus duration (min)", _focus, PomodoroSettings.FocusMin, PomodoroSettings.FocusMax));
        root.Children.Add(Row("Short break (min)", _short, PomodoroSettings.ShortBreakMin, PomodoroSettings.ShortBreakMax));
        root.Children.Add(Row("Long break (min)", _long, PomodoroSettings.LongBreakMin, PomodoroSettings.LongBreakMax));

        _notifications.Content = "Notifications";
        _notifications.Foreground = Brushes.White;
        _notifications.Margin = new Thickness(0, 10, 0, 0);
        root.Children.Add(_notifications);

        _error.Foreground = new SolidColorBrush(Color.FromRgb(235, 120, 120));
        _error.FontSize = 12;
        _error.Margin = new Thickness(0, 8, 0, 0);
        _error.Text = "";
        root.Children.Add(_error);

        StackPanel buttons = new()
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 16, 0, 0),
        };

        Button cancel = MakeButton("Cancel");
        cancel.Click += (_, _) => { DialogResult = false; Close(); };
        cancel.Margin = new Thickness(0, 0, 8, 0);

        _saveButton = MakeButton("Save");
        _saveButton.Click += (_, _) => OnSave();

        buttons.Children.Add(cancel);
        buttons.Children.Add(_saveButton);
        root.Children.Add(buttons);

        // Live validation feedback.
        foreach (TextBox tb in new[] { _focus, _short, _long })
            tb.TextChanged += (_, _) => Validate();

        Validate();
        return root;
    }

    private UIElement Row(string label, TextBox box, int min, int max)
    {
        Grid grid = new() { Margin = new Thickness(0, 6, 0, 0) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });

        TextBlock text = new()
        {
            Text = label,
            Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(text, 0);
        grid.Children.Add(text);

        box.Width = 80;
        box.HorizontalAlignment = HorizontalAlignment.Right;
        box.FontFamily = new FontFamily("Consolas");
        box.ToolTip = $"{min}-{max}";
        Grid.SetColumn(box, 1);
        grid.Children.Add(box);

        return grid;
    }

    private static Button MakeButton(string text) => new()
    {
        Content = text,
        MinWidth = 84,
        Padding = new Thickness(14, 7, 14, 7),
        Foreground = Brushes.White,
        Background = new SolidColorBrush(Color.FromRgb(58, 110, 190)),
        BorderBrush = new SolidColorBrush(Color.FromRgb(120, 165, 235)),
        BorderThickness = new Thickness(1),
    };

    private bool TryParse(TextBox box, int min, int max, out int value)
    {
        value = 0;
        if (!int.TryParse(box.Text, out int v)) return false;
        if (v < min || v > max) return false;
        value = v;
        return true;
    }

    private void Validate()
    {
        bool ok =
            TryParse(_focus, PomodoroSettings.FocusMin, PomodoroSettings.FocusMax, out _) &&
            TryParse(_short, PomodoroSettings.ShortBreakMin, PomodoroSettings.ShortBreakMax, out _) &&
            TryParse(_long, PomodoroSettings.LongBreakMin, PomodoroSettings.LongBreakMax, out _);

        if (_saveButton is not null) _saveButton.IsEnabled = ok;
        _error.Text = ok ? "" : "Enter whole numbers within the allowed ranges.";
    }

    private void OnSave()
    {
        if (!TryParse(_focus, PomodoroSettings.FocusMin, PomodoroSettings.FocusMax, out int f) ||
            !TryParse(_short, PomodoroSettings.ShortBreakMin, PomodoroSettings.ShortBreakMax, out int s) ||
            !TryParse(_long, PomodoroSettings.LongBreakMin, PomodoroSettings.LongBreakMax, out int l))
        {
            return;
        }

        Result = new PomodoroSettings
        {
            FocusMinutes = f,
            ShortBreakMinutes = s,
            LongBreakMinutes = l,
            NotificationsEnabled = _notifications.IsChecked == true,
        };

        DialogResult = true;
        Close();
    }
}
