using System.Runtime.InteropServices;

namespace ResumeMonitor.Win32Callbacks;

/// <summary>
/// Implements a hidden top-level window that receives WM_POWERBROADCAST notifications
/// from Windows. This is the most reliable way for a console application to receive
/// power management events.
/// 
/// Why a hidden window?
/// - Console applications don't automatically receive WM_POWERBROADCAST messages
/// - RegisterPowerSettingNotification requires a window handle
/// - A hidden top-level window can receive broadcast power messages
/// - This approach works reliably across all Windows versions
/// </summary>
public class PowerMonitorWindow : IDisposable
{
    private const string WindowClassName = "PowerMonitorWindowClass";
    
    private IntPtr _windowHandle;
    private IntPtr _moduleHandle;
    private bool _disposed;
    private bool _messageLoopRunning;
    
    // Keep a reference to the delegate to prevent garbage collection
    private readonly NativeMethods.WndProc _windowProcDelegate;

    /// <summary>
    /// Gets the handle to the created window.
    /// </summary>
    public IntPtr Handle => _windowHandle;

    /// <summary>
    /// Creates a new power monitor window.
    /// </summary>
    public PowerMonitorWindow()
    {
        // Store the delegate reference to prevent it from being garbage collected
        _windowProcDelegate = WindowProc;
        
        // Get the module handle for the current process
        _moduleHandle = NativeMethods.GetModuleHandle(null);
        if (_moduleHandle == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to get module handle");
        }

        // Register the window class
        RegisterWindowClass();

        // Create the message-only window
        CreateWindow();
    }

    /// <summary>
    /// Registers a window class for receiving power broadcast messages.
    /// </summary>
    private void RegisterWindowClass()
    {
        var wndClass = new NativeMethods.WNDCLASSEX
        {
            cbSize = (uint)Marshal.SizeOf<NativeMethods.WNDCLASSEX>(),
            style = 0,
            lpfnWndProc = _windowProcDelegate,
            cbClsExtra = 0,
            cbWndExtra = 0,
            hInstance = _moduleHandle,
            hIcon = IntPtr.Zero,
            hCursor = IntPtr.Zero,
            hbrBackground = IntPtr.Zero,
            lpszMenuName = null,
            lpszClassName = WindowClassName,
            hIconSm = IntPtr.Zero
        };

        ushort classAtom = NativeMethods.RegisterClassEx(ref wndClass);
        if (classAtom == 0)
        {
            int error = Marshal.GetLastWin32Error();
            throw new InvalidOperationException($"Failed to register window class. Error code: {error}");
        }

        ConsoleLogger.LogDebug($"Registered window class: {WindowClassName}");
    }

    /// <summary>
    /// Creates a hidden top-level window for receiving power notifications.
    /// </summary>
    private void CreateWindow()
    {
        // Create a hidden top-level window (parent = NULL).
        // WM_POWERBROADCAST notifications are broadcast to top-level windows.
        _windowHandle = NativeMethods.CreateWindowEx(
            0,                              // dwExStyle
            WindowClassName,                // lpClassName
            "PowerMonitorWindow",           // lpWindowName
            0,                              // dwStyle (not visible)
            0, 0, 0, 0,                     // position and size (hidden window)
            IntPtr.Zero,                    // hWndParent (NULL = top-level window)
            IntPtr.Zero,                    // hMenu
            _moduleHandle,                  // hInstance
            IntPtr.Zero                     // lpParam
        );

        if (_windowHandle == IntPtr.Zero)
        {
            int error = Marshal.GetLastWin32Error();
            throw new InvalidOperationException($"Failed to create window. Error code: {error}");
        }

        ConsoleLogger.LogDebug($"Created message-only window with handle: 0x{_windowHandle:X}");
    }

    /// <summary>
    /// The window procedure that processes Windows messages.
    /// This is the callback that Windows calls when a message is sent to our window.
    /// </summary>
    private IntPtr WindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            switch (msg)
            {
                case NativeMethods.WM_POWERBROADCAST:
                    HandlePowerBroadcast(wParam, lParam);
                    return IntPtr.Zero; // Message handled

                case NativeMethods.WM_CLOSE:
                    ConsoleLogger.LogDebug("WM_CLOSE received");
                    NativeMethods.DestroyWindow(hWnd);
                    return IntPtr.Zero;

                case NativeMethods.WM_DESTROY:
                    ConsoleLogger.LogDebug("WM_DESTROY received");
                    _windowHandle = IntPtr.Zero;
                    NativeMethods.PostQuitMessage(0);
                    return IntPtr.Zero;

                default:
                    // Let Windows handle all other messages
                    return NativeMethods.DefWindowProc(hWnd, msg, wParam, lParam);
            }
        }
        catch (Exception ex)
        {
            ConsoleLogger.LogError($"Exception in WindowProc: {ex.Message}");
            return NativeMethods.DefWindowProc(hWnd, msg, wParam, lParam);
        }
    }

    /// <summary>
    /// Handles WM_POWERBROADCAST messages and logs detailed information.
    /// </summary>
    private void HandlePowerBroadcast(IntPtr wParam, IntPtr lParam)
    {
        uint eventType = (uint)wParam.ToInt32();
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var threadId = Environment.CurrentManagedThreadId;

        Console.WriteLine();
        ConsoleLogger.LogEvent($"[{timestamp}] WM_POWERBROADCAST RECEIVED");
        ConsoleLogger.LogEvent($"Thread ID: {threadId}");
        ConsoleLogger.LogEvent($"wParam: 0x{eventType:X8} ({eventType})");
        ConsoleLogger.LogEvent($"lParam: 0x{lParam:X}");

        // Decode and log the power event type
        string eventName = eventType switch
        {
            NativeMethods.PBT_APMSUSPEND => "PBT_APMSUSPEND",
            NativeMethods.PBT_APMRESUMESUSPEND => "PBT_APMRESUMESUSPEND",
            NativeMethods.PBT_APMRESUMEAUTOMATIC => "PBT_APMRESUMEAUTOMATIC",
            NativeMethods.PBT_APMRESUMECRITICAL => "PBT_APMRESUMECRITICAL",
            NativeMethods.PBT_APMPOWERSTATUSCHANGE => "PBT_APMPOWERSTATUSCHANGE",
            NativeMethods.PBT_APMOEMEVENT => "PBT_APMOEMEVENT",
            NativeMethods.PBT_APMQUERYSUSPEND => "PBT_APMQUERYSUSPEND",
            NativeMethods.PBT_APMQUERYSUSPENDFAILED => "PBT_APMQUERYSUSPENDFAILED",
            NativeMethods.PBT_POWERSETTINGCHANGE => "PBT_POWERSETTINGCHANGE",
            _ => "UNKNOWN"
        };

        ConsoleLogger.LogEvent($"Event Type: {eventName}");

        // Provide detailed explanation of each event type
        switch (eventType)
        {
            case NativeMethods.PBT_APMSUSPEND:
                ConsoleLogger.LogWarning("■ System is SUSPENDING (going to sleep/standby)");
                ConsoleLogger.LogEvent("   The system is about to enter sleep or hibernation.");
                ConsoleLogger.LogEvent("   Applications should save state and prepare for suspension.");
                break;

            case NativeMethods.PBT_APMRESUMESUSPEND:
                ConsoleLogger.LogSuccess("▶ System RESUMED from suspend (user-initiated)");
                ConsoleLogger.LogEvent("   The system has resumed from sleep due to user activity.");
                ConsoleLogger.LogEvent("   This is the most common resume event.");
                break;

            case NativeMethods.PBT_APMRESUMEAUTOMATIC:
                ConsoleLogger.LogSuccess("▶ System RESUMED automatically");
                ConsoleLogger.LogEvent("   The system resumed automatically (e.g., Wake-on-LAN, scheduled task).");
                ConsoleLogger.LogEvent("   This resume was NOT triggered by direct user interaction.");
                break;

            case NativeMethods.PBT_APMRESUMECRITICAL:
                ConsoleLogger.LogSuccess("▶ System RESUMED from CRITICAL suspend");
                ConsoleLogger.LogEvent("   The system resumed from a critical suspend state.");
                ConsoleLogger.LogEvent("   Applications may not have received PBT_APMSUSPEND before this.");
                break;

            case NativeMethods.PBT_APMPOWERSTATUSCHANGE:
                ConsoleLogger.LogEvent("⚡ Power STATUS CHANGED");
                ConsoleLogger.LogEvent("   Power source changed (AC/battery) or battery level changed.");
                break;

            case NativeMethods.PBT_APMQUERYSUSPEND:
                ConsoleLogger.LogEvent("? System is REQUESTING permission to suspend");
                ConsoleLogger.LogEvent("   Applications can deny this request (return BROADCAST_QUERY_DENY).");
                break;

            case NativeMethods.PBT_APMQUERYSUSPENDFAILED:
                ConsoleLogger.LogWarning("✗ Suspend request was DENIED");
                ConsoleLogger.LogEvent("   An application denied the suspend request.");
                break;

            case NativeMethods.PBT_APMOEMEVENT:
                ConsoleLogger.LogEvent("🔧 OEM-specific power event");
                break;

            case NativeMethods.PBT_POWERSETTINGCHANGE:
                ConsoleLogger.LogEvent("⚙ Power SETTING changed");
                ConsoleLogger.LogEvent("   A power setting notification was received.");
                break;

            default:
                ConsoleLogger.LogEvent($"⚠ Unknown power event: 0x{eventType:X}");
                break;
        }

        Console.WriteLine();
    }

    /// <summary>
    /// Runs the Windows message loop to process messages.
    /// This method blocks until the message loop exits.
    /// </summary>
    public void RunMessageLoop()
    {
        _messageLoopRunning = true;
        ConsoleLogger.LogDebug("Starting Windows message loop");

        // Standard Windows message loop
        // GetMessage returns:
        //   > 0: Message retrieved successfully
        //   0: WM_QUIT received (exit loop)
        //   < 0: Error occurred
        while (_messageLoopRunning)
        {
            int result = NativeMethods.GetMessage(out NativeMethods.MSG msg, IntPtr.Zero, 0, 0);

            if (result == 0)
            {
                // WM_QUIT received
                ConsoleLogger.LogDebug("WM_QUIT received, exiting message loop");
                break;
            }
            else if (result < 0)
            {
                // Error occurred
                int error = Marshal.GetLastWin32Error();
                ConsoleLogger.LogError($"GetMessage failed with error code: {error}");
                break;
            }

            // Translate and dispatch the message
            NativeMethods.TranslateMessage(ref msg);
            NativeMethods.DispatchMessage(ref msg);
        }

        ConsoleLogger.LogDebug("Message loop exited");
    }

    /// <summary>
    /// Stops the message loop, causing RunMessageLoop to return.
    /// </summary>
    public void StopMessageLoop()
    {
        _messageLoopRunning = false;

        if (_windowHandle == IntPtr.Zero)
        {
            return;
        }

        if (!NativeMethods.PostMessage(_windowHandle, NativeMethods.WM_CLOSE, IntPtr.Zero, IntPtr.Zero))
        {
            int error = Marshal.GetLastWin32Error();
            ConsoleLogger.LogError($"Failed to post WM_CLOSE to the monitor window. Error code: {error}");
        }
    }

    /// <summary>
    /// Disposes of the window and native resources.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            if (_windowHandle != IntPtr.Zero)
            {
                NativeMethods.DestroyWindow(_windowHandle);
                _windowHandle = IntPtr.Zero;
            }

            if (_moduleHandle != IntPtr.Zero)
            {
                NativeMethods.UnregisterClass(WindowClassName, _moduleHandle);
            }

            _disposed = true;
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Finalizer to ensure native resources are cleaned up.
    /// </summary>
    ~PowerMonitorWindow()
    {
        Dispose();
    }
}
