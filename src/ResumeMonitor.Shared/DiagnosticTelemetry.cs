using System.Globalization;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;

namespace ResumeMonitor.Shared;

public enum PowerTransition
{
    On,
    Off,
    Other
}

public sealed class NetworkIdentity
{
    public string InterfaceName { get; }
    public string IpAddress { get; }
    public string MacAddress { get; }

    private NetworkIdentity(string interfaceName, string ipAddress, string macAddress)
    {
        InterfaceName = interfaceName;
        IpAddress = ipAddress;
        MacAddress = macAddress;
    }

    public static NetworkIdentity Resolve()
    {
        foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (networkInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
                networkInterface.OperationalStatus != OperationalStatus.Up)
            {
                continue;
            }

            var ipProperties = networkInterface.GetIPProperties();
            var ipv4Address = ipProperties
                .UnicastAddresses
                .FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork)?
                .Address
                .ToString();

            if (string.IsNullOrWhiteSpace(ipv4Address))
            {
                continue;
            }

            var macBytes = networkInterface.GetPhysicalAddress().GetAddressBytes();
            var macAddress = macBytes.Length == 0
                ? "N/A"
                : string.Join(":", macBytes.Select(b => b.ToString("X2", CultureInfo.InvariantCulture)));

            return new NetworkIdentity(networkInterface.Name, ipv4Address, macAddress);
        }

        return new NetworkIdentity("Unknown", "N/A", "N/A");
    }
}

public sealed class DiagnosticEventTracker
{
    private static readonly object FileLock = new();

    private readonly string _applicationName;
    private readonly NetworkIdentity _identity;
    private readonly string _commonLogPath;
    private long _sequence;
    private long _onCount;
    private long _offCount;

    public DiagnosticEventTracker(string applicationName, NetworkIdentity identity)
    {
        _applicationName = applicationName;
        _identity = identity;
        _commonLogPath = ResolveCommonLogPath();
    }

    public string CommonLogPath => _commonLogPath;
    public long OnCount => Interlocked.Read(ref _onCount);
    public long OffCount => Interlocked.Read(ref _offCount);

    public void LogStartup()
    {
        WriteLine("STARTUP", PowerTransition.Other, "Application startup");
    }

    public void LogPowerEvent(string eventName, PowerTransition transition)
    {
        if (transition == PowerTransition.On)
        {
            Interlocked.Increment(ref _onCount);
        }
        else if (transition == PowerTransition.Off)
        {
            Interlocked.Increment(ref _offCount);
        }

        WriteLine(eventName, transition, "Power notification");
    }

    private void WriteLine(string eventName, PowerTransition transition, string details)
    {
        var sequence = Interlocked.Increment(ref _sequence);
        var utcTimestamp = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        var localTimestamp = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz", CultureInfo.InvariantCulture);

        var line = string.Join('|',
            utcTimestamp,
            localTimestamp,
            Environment.MachineName,
            _applicationName,
            Environment.ProcessId.ToString(CultureInfo.InvariantCulture),
            sequence.ToString(CultureInfo.InvariantCulture),
            transition,
            eventName,
            $"on={OnCount.ToString(CultureInfo.InvariantCulture)}",
            $"off={OffCount.ToString(CultureInfo.InvariantCulture)}",
            $"if={_identity.InterfaceName}",
            $"ip={_identity.IpAddress}",
            $"mac={_identity.MacAddress}",
            details);

        lock (FileLock)
        {
            EnsureHeader();
            File.AppendAllText(_commonLogPath, line + Environment.NewLine, Encoding.UTF8);
        }
    }

    private void EnsureHeader()
    {
        if (File.Exists(_commonLogPath))
        {
            return;
        }

        var directory = Path.GetDirectoryName(_commonLogPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        const string header = "utc|local|machine|app|pid|seq|transition|event|onCount|offCount|interface|ip|mac|details";
        File.AppendAllText(_commonLogPath, header + Environment.NewLine, Encoding.UTF8);
    }

    private static string ResolveCommonLogPath()
    {
        var configuredPath = Environment.GetEnvironmentVariable("RESUME_MONITOR_COMMON_LOG_PATH");
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            return Path.GetFullPath(configuredPath);
        }

        return Path.Combine(AppContext.BaseDirectory, "resume-monitor-common.log");
    }
}
