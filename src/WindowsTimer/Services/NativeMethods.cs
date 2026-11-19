using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace WindowsTimer.Services;

internal static class NativeMethods
{
    public const string AppUserModelId = "WindowsTimer.Timer";

    public static void SetAppUserModelId()
    {
        try
        {
            SetCurrentProcessExplicitAppUserModelID(AppUserModelId);
        }
        catch (Exception ex)
        {
            AppLog.Warn("Could not set the application identity. " + ex.GetType().Name);
        }
    }

    public static void ApplyWindowEffects(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero)
            return;

        var corner = 2;
        DwmSetWindowAttribute(handle, 33, ref corner, sizeof(int));
    }

    public static void Flash(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero)
            return;

        var info = new FlashInfo
        {
            Size = (uint)Marshal.SizeOf<FlashInfo>(),
            Hwnd = handle,
            Flags = 3,
            Count = 3,
            Timeout = 0
        };
        FlashWindowEx(ref info);
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SetCurrentProcessExplicitAppUserModelID(string appId);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    [DllImport("user32.dll")]
    private static extern bool FlashWindowEx(ref FlashInfo info);

    [StructLayout(LayoutKind.Sequential)]
    private struct FlashInfo
    {
        public uint Size;
        public IntPtr Hwnd;
        public uint Flags;
        public uint Count;
        public uint Timeout;
    }
}
