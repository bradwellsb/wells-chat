using System.Runtime.InteropServices;
using Application = Microsoft.Maui.Controls.Application;

namespace WellsChat.Maui.Platforms.Windows;

internal static class WindowsFlashHelper
{
    [StructLayout(LayoutKind.Sequential)]
    private struct FLASHWINFO
    {
        public uint cbSize;
        public nint hwnd;
        public uint dwFlags;
        public uint uCount;
        public uint dwTimeout;
    }

    private const uint FLASHW_ALL = 3;
    private const uint FLASHW_TIMERNOFG = 12;

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FlashWindowEx(ref FLASHWINFO pwfi);

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    public static void FlashWindow()
    {
        var window = Application.Current?.Windows.FirstOrDefault();
        if (window?.Handler?.PlatformView is null) return;

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window.Handler.PlatformView);
        if (hwnd == nint.Zero) return;

        // Only flash if the window is not currently in the foreground
        if (GetForegroundWindow() == hwnd) return;

        var fInfo = new FLASHWINFO
        {
            cbSize = (uint)Marshal.SizeOf<FLASHWINFO>(),
            hwnd = hwnd,
            dwFlags = FLASHW_ALL | FLASHW_TIMERNOFG,
            uCount = 0,
            dwTimeout = 0
        };

        FlashWindowEx(ref fInfo);
    }
}
