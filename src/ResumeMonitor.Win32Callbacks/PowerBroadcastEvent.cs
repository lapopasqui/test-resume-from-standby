namespace ResumeMonitor.Win32Callbacks;

internal sealed record PowerBroadcastEvent(
    uint MessageId,
    nuint WParam,
    nint LParam,
    string Interpretation);
