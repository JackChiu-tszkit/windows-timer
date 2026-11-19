using System.Windows;
using WindowsTimer.Services;
using WindowsTimer.ViewModels;
using WindowsTimer.Views;

namespace WindowsTimer;

public partial class App : Application
{
    private ThemeService? _theme;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        DispatcherUnhandledException += (_, args) =>
        {
            AppLog.Error("Unhandled interface exception", args.Exception);
            args.Handled = true;
        };

        NativeMethods.SetAppUserModelId();
        if (!SingleInstance.TryAcquire())
        {
            SingleInstance.SignalExisting();
            Shutdown();
            return;
        }

        try
        {
            var settingsService = new SettingsService();
            var settings = settingsService.Load();
            _theme = new ThemeService(Dispatcher);
            _theme.Apply(settings.Theme);

            var startup = new StartupService();
            try
            {
                startup.SetEnabled(settings.LaunchOnStartup);
            }
            catch (Exception ex)
            {
                AppLog.Error("Could not sync the Windows startup entry", ex);
            }

            var engine = new TimerEngine(new StopwatchClock());
            engine.SetDuration(new TimeSpan(settings.LastHours, settings.LastMinutes, settings.LastSeconds));
            var viewModel = new MainViewModel(engine, settings, settingsService);
            var window = new MainWindow(
                viewModel,
                settingsService,
                _theme,
                new AlarmPlayer(),
                new NotificationService(),
                startup);
            MainWindow = window;
            window.Show();
            AppLog.Info("Timer started.");
            SingleInstance.StartListening(() => window.Dispatcher.BeginInvoke(window.BringToFront));
        }
        catch (Exception ex)
        {
            AppLog.Error("Startup failed", ex);
            MessageBox.Show(
                "Timer could not start. Details were written to %LocalAppData%\\WindowsTimer\\logs.",
                "Timer");
            Shutdown();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _theme?.Dispose();
        SingleInstance.Release();
        AppLog.Info("Timer exited.");
        base.OnExit(e);
    }
}
