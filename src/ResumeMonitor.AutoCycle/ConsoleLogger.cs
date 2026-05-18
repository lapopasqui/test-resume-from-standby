namespace ResumeMonitor.AutoCycle;

public static class ConsoleLogger
{
    private static readonly object ConsoleLock = new();

    public static void LogInfo(string message) => Log(message, ConsoleColor.White, "INFO");
    public static void LogWarning(string message) => Log(message, ConsoleColor.Yellow, "WARNING");
    public static void LogError(string message) => Log(message, ConsoleColor.Red, "ERROR");
    public static void LogSuccess(string message) => Log(message, ConsoleColor.Green, "SUCCESS");

    private static void Log(string message, ConsoleColor color, string level)
    {
        lock (ConsoleLock)
        {
            Console.ForegroundColor = color;
            Console.WriteLine($"[{level}] {message}");
            Console.ResetColor();
        }
    }
}
