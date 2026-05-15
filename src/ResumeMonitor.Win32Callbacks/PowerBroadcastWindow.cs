using System.Runtime.InteropServices;

namespace ResumeMonitor.Win32Callbacks;

internal sealed class PowerBroadcastWindow : IDisposable
{
    private readonly ConsoleLogger _logger;
    private readonly Action<PowerBroadcastEvent> _onPowerEvent;
    private readonly ManualResetEventSlim _startupComplete = new(false);
    private readonly ManualResetEventSlim _threadStopped = new(false);

    private Thread? _messageThread;
    private Exception? _startupException;
    private NativeMethods.WndProcDelegate? _wndProc;
    private string? _windowClassName;
    private nint _windowHandle;
    private uint _threadId;
    private bool _isRunning;

    public PowerBroadcastWindow(ConsoleLogger logger, Action<PowerBroadcastEvent> onPowerEvent)
    {
        _logger = logger;
        _onPowerEvent = onPowerEvent;
    }

    public void Start()
    {
        if (_isRunning)
        {
            return;
        }

        _messageThread = new Thread(MessageLoopThreadMain)
        {
            Name = "Win32 Power Broadcast Listener",
            IsBackground = true
        };

        _messageThread.Start();
        _startupComplete.Wait();

        if (_startupException is not null)
        {
            throw new InvalidOperationException("Failed to start Win32 power listener.", _startupException);
        }

        _isRunning = true;
    }

    private void MessageLoopThreadMain()
    {
        nint instanceHandle = nint.Zero;

        try
        {
            _threadId = NativeMethods.GetCurrentThreadId();
            instanceHandle = NativeMethods.GetModuleHandle(null);

            _wndProc = WindowProcedure;
            _windowClassName = $"ResumeMonitorWin32_{Guid.NewGuid():N}";

            var windowClass = new NativeMethods.WndClassEx
            {
                cbSize = (uint)Marshal.SizeOf<NativeMethods.WndClassEx>(),
                lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProc),
                hInstance = instanceHandle,
                lpszClassName = _windowClassName
            };

            var classAtom = NativeMethods.RegisterClassEx(ref windowClass);
            if (classAtom == 0)
            {
                throw new InvalidOperationException($"RegisterClassEx failed. LastWin32Error={Marshal.GetLastWin32Error()}");
            }

            // A message-only hidden window is enough for WM_POWERBROADCAST delivery.
            _windowHandle = NativeMethods.CreateWindowEx(
                0,
                _windowClassName,
                "ResumeMonitor.Win32Callbacks.HiddenWindow",
                0,
                0,
                0,
                0,
                0,
                NativeMethods.HwndMessage,
                nint.Zero,
                instanceHandle,
                nint.Zero);

            if (_windowHandle == nint.Zero)
            {
                throw new InvalidOperationException($"CreateWindowEx failed. LastWin32Error={Marshal.GetLastWin32Error()}");
            }

            _logger.Success("Win32 hidden message window created successfully.");
            _startupComplete.Set();

            while (NativeMethods.GetMessage(out var message, nint.Zero, 0, 0) > 0)
            {
                NativeMethods.TranslateMessage(ref message);
                NativeMethods.DispatchMessage(ref message);
            }
        }
        catch (Exception ex)
        {
            _startupException = ex;
            _startupComplete.Set();
        }
        finally
        {
            if (_windowHandle != nint.Zero)
            {
                NativeMethods.DestroyWindow(_windowHandle);
                _windowHandle = nint.Zero;
            }

            if (!string.IsNullOrWhiteSpace(_windowClassName) && instanceHandle != nint.Zero)
            {
                NativeMethods.UnregisterClass(_windowClassName, instanceHandle);
            }

            _threadStopped.Set();
        }
    }

    private nint WindowProcedure(nint hWnd, uint messageId, nuint wParam, nint lParam)
    {
        if (messageId != NativeMethods.WM_POWERBROADCAST)
        {
            return NativeMethods.DefWindowProc(hWnd, messageId, wParam, lParam);
        }

        var interpretation = wParam switch
        {
            NativeMethods.PBT_APMSUSPEND => "PBT_APMSUSPEND: system is entering suspend.",
            NativeMethods.PBT_APMRESUMEAUTOMATIC => "PBT_APMRESUMEAUTOMATIC: automatic resume triggered by event source.",
            NativeMethods.PBT_APMRESUMESUSPEND => "PBT_APMRESUMESUSPEND: resume from suspend via user interaction.",
            NativeMethods.PBT_APMRESUMECRITICAL => "PBT_APMRESUMECRITICAL: critical resume with possible data loss.",
            NativeMethods.PBT_POWERSETTINGCHANGE => "PBT_POWERSETTINGCHANGE: specific power setting changed (lParam points to POWERBROADCAST_SETTING).",
            _ => "Unknown WM_POWERBROADCAST event."
        };

        _onPowerEvent(new PowerBroadcastEvent(messageId, wParam, lParam, interpretation));
        return new nint(1);
    }

    public void Dispose()
    {
        if (!_isRunning)
        {
            return;
        }

        // The message thread exits cleanly by receiving WM_QUIT.
        if (_threadId != 0)
        {
            NativeMethods.PostThreadMessage(_threadId, NativeMethods.WM_QUIT, 0, 0);
        }

        _threadStopped.Wait();
        _isRunning = false;
    }
}
