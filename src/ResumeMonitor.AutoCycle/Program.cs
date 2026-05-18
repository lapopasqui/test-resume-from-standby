using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using ResumeMonitor.Shared;

namespace ResumeMonitor.AutoCycle;

internal static class Program
{
    private const int WolServerPort = 9001;
    private const int RestartDelaySeconds = 90;
    private const string PowerEventName = @"Global\PowerTestEvent";

    private static int Main(string[] args)
    {
        if (!TryParseArguments(args, out var serverIp, out var timeoutMilliseconds, out var validationError))
        {
            ConsoleLogger.LogError(validationError);
            ConsoleLogger.LogError("Usage: ResumeMonitor.AutoCycle <server-ip> <timeout-ms>");
            return 1;
        }

        var identity = NetworkIdentity.Resolve();

        ConsoleLogger.LogInfo("ResumeMonitor.AutoCycle - Starting");
        ConsoleLogger.LogInfo($"Server IP: {serverIp}");
        ConsoleLogger.LogInfo($"TCP timeout: {timeoutMilliseconds} ms");
        ConsoleLogger.LogInfo($"Interface: {identity.InterfaceName}");
        ConsoleLogger.LogInfo($"Local IP: {identity.IpAddress}");
        ConsoleLogger.LogInfo($"Local MAC: {identity.MacAddress}");
        ConsoleLogger.LogInfo($"Power event name: {PowerEventName}");
        ConsoleLogger.LogWarning("Proceed with automatic power cycle loop? [y/N]");

        var key = Console.ReadKey(intercept: true).Key;
        Console.WriteLine();
        if (key != ConsoleKey.Y)
        {
            ConsoleLogger.LogWarning("Operation cancelled.");
            return 2;
        }

        var cycle = 0;
        while (true)
        {
            cycle++;
            ConsoleLogger.LogInfo($"Starting cycle #{cycle}");

            SendWolRequest(serverIp, timeoutMilliseconds, identity.MacAddress);
            SignalPowerTestEvent();
            TriggerShutdown();

            ConsoleLogger.LogInfo("Shutdown requested; if the host remains on, retrying cycle in 30 seconds.");
            Thread.Sleep(30_000);
        }
    }

    private static bool TryParseArguments(string[] args, out string serverIp, out int timeoutMilliseconds, out string errorMessage)
    {
        serverIp = string.Empty;
        timeoutMilliseconds = 0;
        errorMessage = string.Empty;

        if (args.Length != 2)
        {
            errorMessage = "Exactly 2 parameters are required: <server-ip> <timeout-ms>.";
            return false;
        }

        serverIp = args[0];
        if (string.IsNullOrWhiteSpace(serverIp))
        {
            errorMessage = "Server IP cannot be empty.";
            return false;
        }

        if (!int.TryParse(args[1], out timeoutMilliseconds))
        {
            errorMessage = "Timeout must be a valid integer expressed in milliseconds.";
            return false;
        }

        if (timeoutMilliseconds <= 0)
        {
            errorMessage = "Timeout must be greater than zero.";
            return false;
        }

        return true;
    }

    private static void SendWolRequest(string serverIp, int timeoutMilliseconds, string macAddress)
    {
        ConsoleLogger.LogInfo("Sending WOL packet request...");

        using var clientSocket = new TcpClient();
        var retry = 0;
        var okConnect = false;

        while (retry < 10)
        {
            try
            {
                var connectTask = clientSocket.ConnectAsync(serverIp, WolServerPort);
                if (!connectTask.Wait(timeoutMilliseconds))
                {
                    throw new TimeoutException("TCP connect timeout");
                }

                okConnect = true;
                break;
            }
            catch (Exception ex)
            {
                ConsoleLogger.LogWarning($"Unable to connect to WOL server (attempt {retry + 1}/10): {ex.Message}");
                Thread.Sleep(10_000);
                retry++;
            }
        }

        if (!okConnect)
        {
            throw new InvalidOperationException("Unable to send message to server after retries.");
        }

        try
        {
            clientSocket.ReceiveTimeout = timeoutMilliseconds;
            clientSocket.SendTimeout = timeoutMilliseconds;

            using NetworkStream serverStream = clientSocket.GetStream();
            var message = $"{macAddress};{RestartDelaySeconds}";
            var outStream = Encoding.ASCII.GetBytes(message);

            serverStream.Write(outStream, 0, outStream.Length);
            serverStream.Flush();
            ConsoleLogger.LogInfo($"Message sent to server: {message}");

            var inStream = new byte[256];
            try
            {
                var bytesRead = serverStream.Read(inStream, 0, inStream.Length);
                var response = Encoding.ASCII.GetString(inStream, 0, bytesRead).Trim();

                if (response.Equals("OK", StringComparison.OrdinalIgnoreCase) || response.Equals("RECEIVED", StringComparison.OrdinalIgnoreCase))
                {
                    ConsoleLogger.LogSuccess($"WOL request acknowledged by server: {response}");
                }
                else
                {
                    ConsoleLogger.LogWarning($"Unexpected response from server: {response}");
                }
            }
            catch (IOException ex)
            {
                ConsoleLogger.LogWarning($"Timeout or error waiting for server response: {ex.Message}");
            }

            ConsoleLogger.LogInfo($"Restart in {RestartDelaySeconds} seconds");
        }
        catch (Exception ex)
        {
            ConsoleLogger.LogWarning($"Unable to send message to server: {ex}");
        }
    }

    private static void SignalPowerTestEvent()
    {
        using var ewh = new EventWaitHandle(false, EventResetMode.ManualReset, PowerEventName, out _);
        // The delay gives the WOL server request time to be transmitted and flushed before shutdown starts.
        Thread.Sleep(3_000);
        ewh.Set();
        ConsoleLogger.LogInfo($"Set event {PowerEventName}");
    }

    private static void TriggerShutdown()
    {
        ConsoleLogger.LogWarning("Triggering machine shutdown...");

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "shutdown",
                Arguments = "/s /t 0 /f",
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
    }
}
