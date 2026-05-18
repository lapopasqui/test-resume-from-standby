using ResumeMonitor.Shared;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

namespace ResumeMonitor.AutoCycle;

internal static class Program
{
    private const int WolServerPort = 9001;
    private const int RestartDelaySeconds = 90;
    private const string PowerEventName = @"Global\PowerTestEvent";
    private static readonly Regex MacAddressRegex = new(@"^[0-9A-Fa-f]{2}([-:][0-9A-Fa-f]{2}){5}$", RegexOptions.Compiled);

    private static int Main(string[] args)
    {
        if (!TryParseArguments(args, out var serverIp, out var timeoutSeconds, out var macOverride, out var validationError))
        {
            ConsoleLogger.LogError(validationError);
            ConsoleLogger.LogError("Usage: ResumeMonitor.AutoCycle <server-ip> <timeout-s> [mac-address]");
            return 1;
        }

        var identity = NetworkIdentity.Resolve();

        ConsoleLogger.LogInfo("ResumeMonitor.AutoCycle - Starting");
        ConsoleLogger.LogInfo($"Server IP: {serverIp}");
        ConsoleLogger.LogInfo($"TCP timeout: {timeoutSeconds} s");
        ConsoleLogger.LogInfo($"Interface: {identity.InterfaceName}");
        ConsoleLogger.LogInfo($"Local IP: {identity.IpAddress}");
        ConsoleLogger.LogInfo($"Local MAC: {identity.MacAddress}");
        var effectiveMac = macOverride ?? identity.MacAddress;
        var macSource = string.IsNullOrWhiteSpace(macOverride) ? "auto-detected from local interface" : "command line override";
        ConsoleLogger.LogInfo($"WOL MAC in use: {effectiveMac}");
        ConsoleLogger.LogInfo($"MAC source: {macSource}");
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

            SendWolRequest(serverIp, timeoutSeconds, effectiveMac);
            TriggerStandby();

            ConsoleLogger.LogInfo("Power test event signaled; if the host remains on, retrying cycle in 30 seconds.");
            Thread.Sleep(30_000);
        }
    }

    private static bool TryParseArguments(string[] args, out string serverIp, out int timeoutMilliseconds, out string? macOverride, out string errorMessage)
    {
        serverIp = string.Empty;
        timeoutMilliseconds = 0;
        macOverride = null;
        errorMessage = string.Empty;

        if (args.Length is < 2 or > 3)
        {
            errorMessage = "Required parameters: <server-ip> <timeout-ms>. Optional: [mac-address].";
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

        if (args.Length == 3)
        {
            if (!TryNormalizeMacAddress(args[2], out macOverride))
            {
                errorMessage = "Invalid mac-address format. Expected value like AA:BB:CC:DD:EE:FF.";
                return false;
            }
        }

        return true;
    }

    private static bool TryNormalizeMacAddress(string value, out string normalizedMac)
    {
        normalizedMac = string.Empty;
        var trimmed = value.Trim();
        if (!MacAddressRegex.IsMatch(trimmed))
        {
            return false;
        }

        normalizedMac = trimmed.Replace('-', ':').ToUpperInvariant();
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

    private static void TriggerStandby()
    {
        ConsoleLogger.LogWarning("Triggering machine standby (suspend)...");

        // Equivalent of SetSuspendState(PowerState.Suspend, false, true):
        // Arguments: Hibernate=0, ForceCritical=0, DisableWakeEvent=1
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "rundll32.exe",
                Arguments = "powrprof.dll,SetSuspendState 0,0,1",
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
    }
}
