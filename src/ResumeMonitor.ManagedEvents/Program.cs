using ResumeMonitor.ManagedEvents;

if (!OperatingSystem.IsWindows())
{
    Console.Error.WriteLine("ResumeMonitor.ManagedEvents is intended for Windows only.");
    return;
}

var logger = new ConsoleLogger();
using var shutdownSignal = new ManualResetEventSlim(false);
using var monitor = new ManagedPowerMonitor(logger);

if (!monitor.TryStart(out var error))
{
    logger.Error(error ?? "Unknown startup error.");
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

logger.Info("Managed power monitor started.");
logger.Info("Press Ctrl+C to exit.");
shutdownSignal.Wait();
logger.Info("Managed power monitor stopped.");
