using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using WindowsTimer.Services;
using WindowsTimer.ViewModels;

namespace WindowsTimer.Views;

public partial class MiniTimerWindow : Window
{
    private readonly MainViewModel _viewModel;
    private bool _forceClose;

    public MiniTimerWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        SourceInitialized += (_, _) => NativeMethods.ApplyWindowEffects(this);
    }

    public void ForceClose()
    {
        _forceClose = true;
        Close();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_forceClose)
        {
            e.Cancel = true;
            _viewModel.IsMiniMode = false;
            return;
        }

        base.OnClosing(e);
    }

    private void Expand_OnClick(object sender, RoutedEventArgs e) => _viewModel.IsMiniMode = false;

    private void Surface_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed || IsInsideButton(e.OriginalSource as DependencyObject))
            return;

        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
        }
    }

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
