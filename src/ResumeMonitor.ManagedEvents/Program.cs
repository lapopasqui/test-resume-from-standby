using System.Diagnostics;
using Microsoft.Win32;

namespace ResumeMonitor.ManagedEvents;

/// <summary>
/// Console application that monitors Windows power management events using
/// the managed .NET SystemEvents API.
/// </summary>
class Program
{
    private static bool _isRunning = true;
    private static readonly object _lockObject = new();

    static void Main(string[] args)
    {
        ConsoleLogger.LogInfo("ResumeMonitor.ManagedEvents - Starting");
        ConsoleLogger.LogInfo($"Process ID: {Environment.ProcessId}");
        ConsoleLogger.LogInfo($"Main Thread ID: {Environment.CurrentManagedThreadId}");
        ConsoleLogger.LogInfo("Monitoring system power events using Microsoft.Win32.SystemEvents");
        ConsoleLogger.LogInfo("Press Ctrl+C to exit");
        Console.WriteLine();

        // Set up Ctrl+C handler for graceful shutdown
        Console.CancelKeyPress += OnCancelKeyPress;

        try
        {
            // Register for power mode changed events
            // IMPORTANT: SystemEvents requires a message pump to work correctly in a console app.
            // The events are raised on a background thread that processes Windows messages.
            SystemEvents.PowerModeChanged += OnPowerModeChanged;
            ConsoleLogger.LogSuccess("Successfully registered for PowerModeChanged events");

            // Keep the application alive
            // SystemEvents uses a hidden window and message pump internally,
            // so we just need to keep the main thread alive.
            ConsoleLogger.LogInfo("Waiting for power events... (Put the system to sleep to test)");
            Console.WriteLine();

            lock (_lockObject)
            {
                while (_isRunning)
                {
                    Monitor.Wait(_lockObject, 1000);
                }
            }
        }
        catch (Exception ex)
        {
            ConsoleLogger.LogError($"Error in main loop: {ex.Message}");
            ConsoleLogger.LogError($"Stack trace: {ex.StackTrace}");
        }
        finally
        {
            // Clean up event handlers
            SystemEvents.PowerModeChanged -= OnPowerModeChanged;
            ConsoleLogger.LogInfo("Unregistered power mode event handlers");
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

        lock (_lockObject)
        {
            _isRunning = false;
            Monitor.Pulse(_lockObject);
        }
    }

    /// <summary>
    /// Handles PowerModeChanged events from the system.
    /// This event is fired when the system power mode changes (suspend, resume, etc.).
    /// NOTE: This handler runs on a background thread managed by SystemEvents.
    /// </summary>
    private static void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        // Log the event with timestamp and thread information
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var threadId = Environment.CurrentManagedThreadId;

        Console.WriteLine();
        ConsoleLogger.LogEvent($"[{timestamp}] POWER EVENT DETECTED");
        ConsoleLogger.LogEvent($"Thread ID: {threadId}");
        ConsoleLogger.LogEvent($"Event Type: {e.Mode}");

        // Provide detailed information about each power mode
        switch (e.Mode)
        {
            case PowerModes.Resume:
                ConsoleLogger.LogSuccess("▶ System RESUMED from suspend/sleep");
                ConsoleLogger.LogEvent("   This event fires when the system wakes from sleep or hibernation.");
                break;

            case PowerModes.Suspend:
                ConsoleLogger.LogWarning("■ System is SUSPENDING (going to sleep)");
                ConsoleLogger.LogEvent("   This event fires when the system is about to enter sleep or hibernation.");
                break;

            case PowerModes.StatusChange:
                ConsoleLogger.LogEvent("⚡ Power STATUS CHANGED");
                ConsoleLogger.LogEvent("   This may indicate a change in power source (AC/battery) or battery level.");
                ConsoleLogger.LogEvent("   Note: SystemEvents does not provide additional details for StatusChange.");
                break;

            default:
                ConsoleLogger.LogEvent($"⚠ Unknown power mode: {e.Mode}");
                break;
        }

        // Log any additional information available from the event args
        ConsoleLogger.LogEvent($"Event Args Type: {e.GetType().Name}");
        Console.WriteLine();
    }
}
