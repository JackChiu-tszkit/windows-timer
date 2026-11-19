using System.Windows;
using WindowsTimer.Services;

namespace WindowsTimer.Views;

public partial class TimeUpWindow : Window
{
    public TimeUpWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => NativeMethods.ApplyWindowEffects(this);
        Loaded += (_, _) => DismissButton.Focus();
    }

    private void Dismiss_OnClick(object sender, RoutedEventArgs e) => Close();
}
