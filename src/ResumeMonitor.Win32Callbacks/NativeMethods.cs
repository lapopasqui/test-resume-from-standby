using System.Runtime.InteropServices;

namespace ResumeMonitor.Win32Callbacks;

/// <summary>
/// Contains P/Invoke declarations for Win32 APIs related to window management
/// and message processing.
/// </summary>
public static class NativeMethods
{
    #region Window Class and Creation

    /// <summary>
    /// Registers a window class.
    /// </summary>
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern ushort RegisterClassEx(ref WNDCLASSEX lpwcx);

    /// <summary>
    /// Creates a message-only window.
    /// HWND_MESSAGE = -3, used for creating message-only windows.
    /// </summary>
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern IntPtr CreateWindowEx(
        uint dwExStyle,
        string lpClassName,
        string lpWindowName,
        uint dwStyle,
        int x,
        int y,
        int nWidth,
        int nHeight,
        IntPtr hWndParent,
        IntPtr hMenu,
        IntPtr hInstance,
        IntPtr lpParam);

    /// <summary>
    /// Destroys the specified window.
    /// </summary>
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool DestroyWindow(IntPtr hWnd);

    /// <summary>
    /// Unregisters a window class.
    /// </summary>
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UnregisterClass(string lpClassName, IntPtr hInstance);

    #endregion

    #region Message Processing

    /// <summary>
    /// Retrieves a message from the thread's message queue.
    /// </summary>
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    /// <summary>
    /// Translates virtual-key messages into character messages.
    /// </summary>
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool TranslateMessage(ref MSG lpMsg);

    /// <summary>
    /// Dispatches a message to a window procedure.
    /// </summary>
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr DispatchMessage(ref MSG lpMsg);

    /// <summary>
    /// Calls the default window procedure for messages not processed by the application.
    /// </summary>
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr DefWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    /// <summary>
    /// Posts a message to the message queue of the thread that created the target window.
    /// </summary>
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    /// <summary>
    /// Posts a quit message to the thread's message queue.
    /// </summary>
    [DllImport("user32.dll")]
    public static extern void PostQuitMessage(int nExitCode);

    #endregion

    #region Module and Instance

    /// <summary>
    /// Gets the handle to the current module/instance.
    /// </summary>
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr GetModuleHandle(string? lpModuleName);

    #endregion

    #region Constants

    // Window Messages
    public const uint WM_CLOSE = 0x0010;
    public const uint WM_POWERBROADCAST = 0x0218;
    public const uint WM_DESTROY = 0x0002;

    // Power Broadcast Events (wParam values for WM_POWERBROADCAST)
    public const uint PBT_APMSUSPEND = 0x0004;          // System is suspending
    public const uint PBT_APMRESUMESUSPEND = 0x0007;    // System resumed from suspend
    public const uint PBT_APMRESUMEAUTOMATIC = 0x0012;  // System resumed automatically
    public const uint PBT_APMRESUMECRITICAL = 0x0006;   // System resumed from critical suspend
    public const uint PBT_APMPOWERSTATUSCHANGE = 0x000A; // Power status changed
    public const uint PBT_APMOEMEVENT = 0x000B;         // OEM-specific event
    public const uint PBT_APMQUERYSUSPEND = 0x0000;     // Request to suspend
    public const uint PBT_APMQUERYSUSPENDFAILED = 0x0002; // Suspend request denied
    public const uint PBT_POWERSETTINGCHANGE = 0x8013;  // Power setting changed

    #endregion

    #region Structures

    /// <summary>
    /// Extended window class structure.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct WNDCLASSEX
    {
        public uint cbSize;
        public uint style;
        public WndProc lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string? lpszMenuName;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string lpszClassName;
        public IntPtr hIconSm;
    }

    /// <summary>
    /// Message structure for Windows messages.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public POINT pt;
    }

    /// <summary>
    /// Point structure.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
    }

    #endregion

    #region Delegates

    /// <summary>
    /// Window procedure callback delegate.
    /// </summary>
    public delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    #endregion
}
