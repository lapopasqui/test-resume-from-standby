namespace ResumeMonitor.Win32Callbacks;

/// <summary>
/// Console application that monitors Windows power management events using
/// Win32 API callbacks through P/Invoke.
/// 
/// This implementation creates a hidden window to receive WM_POWERBROADCAST messages,
/// which is the most reliable way for a console application to receive power notifications.
/// </summary>
class Program
{
    private static PowerMonitorWindow? _monitorWindow;

    static void Main(string[] args)
    {
        ConsoleLogger.LogInfo("ResumeMonitor.Win32Callbacks - Starting");
        ConsoleLogger.LogInfo($"Process ID: {Environment.ProcessId}");
        ConsoleLogger.LogInfo($"Main Thread ID: {Environment.CurrentManagedThreadId}");
        ConsoleLogger.LogInfo("Monitoring system power events using Win32 API (P/Invoke)");
        ConsoleLogger.LogInfo("Implementation: Hidden window + WM_POWERBROADCAST messages");
        ConsoleLogger.LogInfo("Press Ctrl+C to exit");
        Console.WriteLine();

        // Set up Ctrl+C handler for graceful shutdown
        Console.CancelKeyPress += OnCancelKeyPress;

        try
        {
            // Create a hidden window to receive power broadcast messages
            _monitorWindow = new PowerMonitorWindow();
            ConsoleLogger.LogSuccess("Successfully created message-only window for power notifications");
            ConsoleLogger.LogInfo($"Window Handle: 0x{_monitorWindow.Handle:X}");
            Console.WriteLine();

            ConsoleLogger.LogInfo("Waiting for power events... (Put the system to sleep to test)");
            Console.WriteLine();

            // Run the Windows message pump
            // This is required to receive and process WM_POWERBROADCAST messages
            _monitorWindow.RunMessageLoop();
        }
        catch (Exception ex)
        {
            ConsoleLogger.LogError($"Fatal error: {ex.Message}");
            ConsoleLogger.LogError($"Stack trace: {ex.StackTrace}");
        }
        finally
        {
            // Clean up the window and native resources
            _monitorWindow?.Dispose();
            ConsoleLogger.LogInfo("Cleaned up native resources");
            ConsoleLogger.LogInfo("Application shutting down");
        }
    }

    /// <summary>
    /// Handles Ctrl+C to allow graceful shutdown.
    /// </summary>
    private static void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
    {
        e.Cancel = true; // Prevent immediate termination
        ConsoleLogger.LogWarning("Ctrl+C detected - shutting down gracefully...");
        
        _monitorWindow?.StopMessageLoop();
    }
}
