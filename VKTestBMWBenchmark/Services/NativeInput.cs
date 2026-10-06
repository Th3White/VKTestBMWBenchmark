using System.Runtime.InteropServices;

namespace VKTestBMWBenchmark.Services;

/// <summary>
/// Минимальная обёртка над WinAPI: активация окна,
/// нажатие клавиш и клик мышью в относительных координатах окна.
/// </summary>
public static class NativeInput
{
    public const byte VkReturn = 0x0D;
    private const byte VkMenu = 0x12; // Alt

    private const uint KeyEventKeyUp = 0x0002;
    private const uint MouseEventLeftDown = 0x0002;
    private const uint MouseEventLeftUp = 0x0004;

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll")]
    private static extern bool SetProcessDPIAware();

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    private static extern bool GetClientRect(IntPtr hWnd, out Rect rect);

    [DllImport("user32.dll")]
    private static extern bool ClientToScreen(IntPtr hWnd, ref Point point);

    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    private static extern uint MapVirtualKey(uint uCode, uint uMapType);

    [DllImport("user32.dll")]
    private static extern void keybd_event(
        byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    [DllImport("user32.dll")]
    private static extern void mouse_event(
        uint dwFlags, int dx, int dy, uint dwData, UIntPtr dwExtraInfo);

    static NativeInput()
    {
        // Без этого GetWindowRect/SetCursorPos работают
        // в масштабированных координатах при DPI != 100%.
        SetProcessDPIAware();
    }

    public static bool IsForeground(IntPtr hWnd)
    {
        return GetForegroundWindow() == hWnd;
    }

    public static bool TryActivate(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero)
            return false;

        if (IsIconic(hWnd))
        {
            ShowWindow(hWnd, 9); // SW_RESTORE
        }

        // Windows запрещает фоновому процессу забирать фокус.
        // Нажатие Alt снимает эту блокировку для SetForegroundWindow.
        keybd_event(VkMenu, 0, 0, UIntPtr.Zero);
        SetForegroundWindow(hWnd);
        keybd_event(VkMenu, 0, KeyEventKeyUp, UIntPtr.Zero);

        Thread.Sleep(300);

        return IsForeground(hWnd);
    }

    public static void PressKey(byte virtualKey)
    {
        // Unreal читает Raw Input, поэтому передаём и скан-код.
        byte scanCode = (byte)MapVirtualKey(virtualKey, 0);

        keybd_event(virtualKey, scanCode, 0, UIntPtr.Zero);
        Thread.Sleep(60);
        keybd_event(virtualKey, scanCode, KeyEventKeyUp, UIntPtr.Zero);
    }

    /// <summary>
    /// Клик левой кнопкой в точке, заданной долями ширины и высоты
    /// клиентской области окна (без рамки и заголовка).
    /// Так клик не зависит ни от разрешения, ни от режима окна.
    /// </summary>
    public static void ClickRelative(IntPtr hWnd, double relX, double relY)
    {
        if (!GetClientRect(hWnd, out Rect client))
            return;

        var point = new Point
        {
            X = (int)(client.Right * relX),
            Y = (int)(client.Bottom * relY)
        };

        if (!ClientToScreen(hWnd, ref point))
            return;

        SetCursorPos(point.X, point.Y);
        Thread.Sleep(150);

        mouse_event(MouseEventLeftDown, 0, 0, 0, UIntPtr.Zero);
        Thread.Sleep(60);
        mouse_event(MouseEventLeftUp, 0, 0, 0, UIntPtr.Zero);
    }
}
