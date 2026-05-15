using ResumeMonitor.Win32Callbacks;

if (!OperatingSystem.IsWindows())
{
    Console.Error.WriteLine("ResumeMonitor.Win32Callbacks is intended for Windows only.");
    return;
}

var logger = new ConsoleLogger();
using var shutdownSignal = new ManualResetEventSlim(false);

using var window = new PowerBroadcastWindow(logger, evt =>
{
    logger.PowerEvent(
        $"WM_POWERBROADCAST received | Msg=0x{evt.MessageId:X} | wParam=0x{evt.WParam:X} | lParam=0x{evt.LParam:X} | {evt.Interpretation}");
});

try
{
    window.Start();
}
catch (Exception ex)
{
    logger.Error($"Failed to initialize Win32 callback listener: {ex.Message}");
    return;
}

void RequestShutdown(string reason)
{
    logger.Warning($"Shutdown requested: {reason}");
    shutdownSignal.Set();
}

Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    RequestShutdown("Ctrl+C pressed.");
};

AppDomain.CurrentDomain.ProcessExit += (_, _) => RequestShutdown("Process exit signaled.");

logger.Info("Win32 power broadcast listener started.");
logger.Info("Press Ctrl+C to exit.");
shutdownSignal.Wait();
logger.Info("Win32 power broadcast listener stopped.");
