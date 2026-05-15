namespace ResumeMonitor.ManagedEvents;

/// <summary>
/// Helper class for formatted console logging with color support.
/// </summary>
public static class ConsoleLogger
{
    private static readonly object _consoleLock = new();

    /// <summary>
    /// Logs a general informational message in white.
    /// </summary>
    public static void LogInfo(string message)
    {
        lock (_consoleLock)
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"[INFO] {message}");
            Console.ResetColor();
        }
    }

    /// <summary>
    /// Logs a success message in green.
    /// </summary>
    public static void LogSuccess(string message)
    {
        lock (_consoleLock)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[SUCCESS] {message}");
            Console.ResetColor();
        }
    }

    /// <summary>
    /// Logs a warning message in yellow.
    /// </summary>
    public static void LogWarning(string message)
    {
        lock (_consoleLock)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[WARNING] {message}");
            Console.ResetColor();
        }
    }

    /// <summary>
    /// Logs an error message in red.
    /// </summary>
    public static void LogError(string message)
    {
        lock (_consoleLock)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[ERROR] {message}");
            Console.ResetColor();
        }
    }

    /// <summary>
    /// Logs a power event message in cyan.
    /// </summary>
    public static void LogEvent(string message)
    {
        lock (_consoleLock)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(message);
            Console.ResetColor();
        }
    }
}
