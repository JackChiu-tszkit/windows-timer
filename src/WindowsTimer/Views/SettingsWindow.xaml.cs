using System.Windows;
using System.Windows.Input;
using WindowsTimer.Services;
using WindowsTimer.ViewModels;

namespace WindowsTimer.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        SourceInitialized += (_, _) => NativeMethods.ApplyWindowEffects(this);
    }

    private void Done_OnClick(object sender, RoutedEventArgs e) => Close();

    private void Title_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed || e.OriginalSource is System.Windows.Controls.Button)
            return;

        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
        }
    }
}
