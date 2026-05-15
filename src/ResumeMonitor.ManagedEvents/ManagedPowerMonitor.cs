using Microsoft.Win32;

namespace ResumeMonitor.ManagedEvents;

internal sealed class ManagedPowerMonitor : IDisposable
{
    private readonly ConsoleLogger _logger;
    private bool _isStarted;

    public ManagedPowerMonitor(ConsoleLogger logger)
    {
        _logger = logger;
    }

    public bool TryStart(out string? error)
    {
        error = null;

        try
        {
            // Registering this event is the key initialization step for console apps.
            // .NET wires internal window/message processing needed for SystemEvents.
            SystemEvents.PowerModeChanged += OnPowerModeChanged;
            _isStarted = true;
            _logger.Success("Subscribed to Microsoft.Win32.SystemEvents.PowerModeChanged.");
            return true;
        }
        catch (Exception ex)
        {
            error = $"Unable to subscribe to power mode events: {ex.Message}";
            return false;
        }
    }

    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs eventArgs)
    {
        var mode = eventArgs.Mode;
        var details = mode switch
        {
            PowerModes.Suspend => "System is entering suspend/sleep.",
            PowerModes.Resume => "System resumed from suspend/sleep (managed API does not expose automatic-vs-user distinction).",
            PowerModes.StatusChange => "Power status changed (e.g., battery/AC state).",
            _ => "Unknown PowerModes value."
        };

        _logger.Info($"PowerModeChanged received | Mode: {mode} | Detail: {details}");
    }

    public void Dispose()
    {
        if (!_isStarted)
        {
            return;
        }

        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        _isStarted = false;
        _logger.Info("Unsubscribed from SystemEvents.PowerModeChanged.");
    }
}
