namespace ResumeMonitor.ManagedEvents;

internal sealed class ConsoleLogger
{
    public void Info(string message) => Write("INFO", message, ConsoleColor.Gray);

    public void Success(string message) => Write("OK", message, ConsoleColor.Green);

    public void Warning(string message) => Write("WARN", message, ConsoleColor.Yellow);

    public void Error(string message) => Write("ERROR", message, ConsoleColor.Red);

    private static void Write(string level, string message, ConsoleColor color)
    {
        var timestamp = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz");
        var processId = Environment.ProcessId;
        var threadId = Environment.CurrentManagedThreadId;

        var originalColor = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.WriteLine($"[{timestamp}] [{level}] [PID:{processId}] [TID:{threadId}] {message}");
        Console.ForegroundColor = originalColor;
    }
}
