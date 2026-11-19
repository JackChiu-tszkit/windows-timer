using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using WindowsTimer.Models;
using WindowsTimer.Services;
using WindowsTimer.ViewModels;

namespace WindowsTimer.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly SettingsService _settingsService;
    private readonly ThemeService _themeService;
    private readonly AlarmPlayer _alarm;
    private readonly NotificationService _notifications;
    private readonly StartupService _startup;
    private readonly TrayController _tray;
    private readonly DispatcherTimer _ticker;
    private MiniTimerWindow? _mini;
    private TimeUpWindow? _alert;
    private bool _exiting;
    private bool _cleaned;

    public MainWindow(
        MainViewModel viewModel,
        SettingsService settingsService,
        ThemeService themeService,
        AlarmPlayer alarm,
        NotificationService notifications,
        StartupService startup)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _settingsService = settingsService;
        _themeService = themeService;
        _alarm = alarm;
        _notifications = notifications;
        _startup = startup;
        DataContext = viewModel;
        _tray = new TrayController(viewModel, BringToFront, RequestExit);
        _viewModel.Completed += (_, _) => Dispatcher.BeginInvoke(ShowAlert);
        _viewModel.OpenSettingsRequested += (_, _) => ShowSettings();
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        _ticker = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _ticker.Tick += (_, _) => _viewModel.Tick();
        _ticker.Start();
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        SourceInitialized += (_, _) => NativeMethods.ApplyWindowEffects(this);
    }

    public void BringToFront()
    {
        if (_viewModel.IsMiniMode && _mini != null)
        {
            if (!_mini.IsVisible)
                _mini.Show();
            _mini.Activate();
            return;
        }

        Show();
        Activate();
    }

    public void RequestExit()
    {
        _exiting = true;
        Close();
    }

    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);
        if (_exiting || WindowState != WindowState.Minimized)
            return;

        WindowState = WindowState.Normal;
        HideToTray();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_exiting && _viewModel.Settings.CloseBehavior == CloseBehavior.MinimizeToTray)
        {
            e.Cancel = true;
            HideToTray();
            return;
        }

        _exiting = true;
        Cleanup();
        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        Application.Current.Shutdown();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.IsMiniMode))
            ApplyMiniMode();

        if (e.PropertyName is nameof(MainViewModel.IsRunning) or nameof(MainViewModel.CanEditTime)
            && (_viewModel.IsRunning || _viewModel.CanEditTime))
        {
            CloseAlert();
        }
    }

    private void ApplyMiniMode()
    {
        if (_viewModel.IsMiniMode)
        {
            _mini ??= new MiniTimerWindow(_viewModel);
            if (double.IsNaN(_mini.Left) || double.IsNaN(_mini.Top))
            {
                var area = SystemParameters.WorkArea;
                _mini.Left = area.Right - _mini.Width - 24;
                _mini.Top = area.Bottom - _mini.Height - 24;
            }

            if (!_mini.IsVisible)
                _mini.Show();
            _mini.Activate();
            Hide();
            return;
        }

        _mini?.Hide();
        Show();
        Activate();
    }

    private void ShowAlert()
    {
        if (_alert != null)
            return;

        _alarm.Start(_viewModel.Settings);
        _notifications.ShowTimeUp();
        _alert = new TimeUpWindow();
        _alert.Closed += (_, _) =>
        {
            _alarm.Stop();
            _alert = null;
        };
        _alert.Show();
        _alert.Activate();
        NativeMethods.Flash(_alert);
    }

    private void CloseAlert()
    {
        if (_alert == null)
            return;

        var alert = _alert;
        _alert = null;
        alert.Close();
    }

    private void ShowSettings()
    {
        var dialog = new SettingsWindow(
            new SettingsViewModel(
                _viewModel.Settings,
                _settingsService,
                _themeService,
                _alarm,
                _startup,
                _viewModel.NotifyAppearance))
        {
            Owner = this
        };
        dialog.ShowDialog();
        _viewModel.NotifyAppearance();
    }

    private void HideToTray()
    {
        Hide();
        _tray.ShowHintOnce();
    }

    private void Cleanup()
    {
        if (_cleaned)
            return;

        _cleaned = true;
        _ticker.Stop();
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        _alarm.Stop();
        _mini?.ForceClose();
        CloseAlert();
        _tray.Dispose();
    }

    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Suspend)
        {
            AppLog.Info("System suspending. " + SleepPolicy.Description);
            return;
        }

        if (e.Mode == PowerModes.Resume)
        {
            AppLog.Info("System resumed. " + SleepPolicy.Description);
            Dispatcher.BeginInvoke(_viewModel.Tick);
        }
    }

    private void TitleBar_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed || IsInsideButton(e.OriginalSource as DependencyObject))
            return;

        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
            // DragMove throws if the button was released before the call.
        }
    }

    private void Minimize_OnClick(object sender, RoutedEventArgs e) => HideToTray();

    private void Close_OnClick(object sender, RoutedEventArgs e) => Close();

    private void Window_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.None)
            return;

        if (e.Key == Key.Space && Keyboard.FocusedElement is not System.Windows.Controls.TextBox)
        {
            if (_viewModel.ToggleCommand.CanExecute(null))
                _viewModel.ToggleCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.R && Keyboard.FocusedElement is not System.Windows.Controls.TextBox)
        {
            _viewModel.ResetCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void TimeBox_OnPreviewTextInput(object sender, TextCompositionEventArgs e) =>
        e.Handled = e.Text.Any(ch => !char.IsDigit(ch));

    private void TimeBox_OnGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is System.Windows.Controls.TextBox box)
            box.SelectAll();
    }

    private void TimeBox_OnLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) =>
        _viewModel.NormalizeTimeText();

    private static bool IsInsideButton(DependencyObject? source)
    {
        while (source != null)
        {
            if (source is System.Windows.Controls.Button)
                return true;
            source = VisualTreeHelper.GetParent(source);
        }

        return false;
    }
}
