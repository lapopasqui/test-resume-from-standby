# Test Resume From Standby

A Windows-focused .NET solution for monitoring and comparing system power management events during sleep/standby and resume cycles.

## Overview

This solution contains two console applications that monitor Windows power management events (suspend/resume from sleep or standby). The purpose is to compare the behavior and reliability of two different implementation approaches:

1. **ResumeMonitor.ManagedEvents** - Uses the managed .NET API (`Microsoft.Win32.SystemEvents`)
2. **ResumeMonitor.Win32Callbacks** - Uses native Win32 APIs through P/Invoke with `WM_POWERBROADCAST` messages

Both applications log detailed information about power events, including timestamps, event types, and technical details, making it easy to observe and compare their behavior during system suspend/resume cycles.

## Why Two Implementations?

The .NET ecosystem provides multiple ways to detect power management events, each with its own characteristics:

- **Managed .NET (`SystemEvents`)**: Easier to implement, cross-platform aspirations, but with less control and fewer details
- **Win32 P/Invoke**: More complex but provides lower-level access, more detailed event information, and potentially more reliable delivery

This solution allows direct comparison of:
- Event delivery reliability
- Level of detail provided
- Timing differences
- Implementation complexity
- Distinguishing between automatic vs. user-initiated resume

## Prerequisites

- **Operating System**: Windows 10 or Windows 11 (Windows-only solution)
- **.NET SDK**: .NET 8.0 or later
- **Development Tools** (optional): Visual Studio 2022, Visual Studio Code, or JetBrains Rider
- **Administrator Rights**: Not required for normal operation

To verify .NET is installed:
```bash
dotnet --version
```

## Building the Solution

### Using the Command Line

1. Clone or navigate to the repository:
```bash
cd test-resume-from-standby
```

2. Restore dependencies and build:
```bash
dotnet build test-resume-from-standby.sln
```

Or build in Release mode:
```bash
dotnet build test-resume-from-standby.sln -c Release
```

### Using Visual Studio

1. Open `test-resume-from-standby.sln` in Visual Studio 2022
2. Select **Build → Build Solution** (or press `Ctrl+Shift+B`)

### Build Output

After building, executables will be located at:
- `src/ResumeMonitor.ManagedEvents/bin/Debug/net8.0/ResumeMonitor.ManagedEvents.exe`
- `src/ResumeMonitor.Win32Callbacks/bin/Debug/net8.0/ResumeMonitor.Win32Callbacks.exe`

## Running the Applications

### ResumeMonitor.ManagedEvents

Run from the command line:
```bash
dotnet run --project src/ResumeMonitor.ManagedEvents/ResumeMonitor.ManagedEvents.csproj
```

Or run the built executable directly:
```bash
src\ResumeMonitor.ManagedEvents\bin\Debug\net8.0\ResumeMonitor.ManagedEvents.exe
```

### ResumeMonitor.Win32Callbacks

Run from the command line:
```bash
dotnet run --project src/ResumeMonitor.Win32Callbacks/ResumeMonitor.Win32Callbacks.csproj
```

Or run the built executable directly:
```bash
src\ResumeMonitor.Win32Callbacks\bin\Debug\net8.0\ResumeMonitor.Win32Callbacks.exe
```

### What You'll See

Both applications will:
1. Display startup information (process ID, thread ID)
2. Indicate successful registration for power events
3. Wait for power events
4. Log detailed information when power events occur (suspend/resume)
5. Continue running until you press `Ctrl+C`

## Testing Suspend/Resume Behavior

### Test Procedure

Follow these steps to observe power event detection:

#### Method 1: Manual Sleep (Recommended for Testing)

1. **Start the application**:
   ```bash
   dotnet run --project src/ResumeMonitor.ManagedEvents/ResumeMonitor.ManagedEvents.csproj
   ```

2. **Put Windows to sleep**:
   - Press `Windows + X` and select "Sleep"
   - Or: Click Start → Power → Sleep
   - Or: Close the laptop lid (if configured to sleep)
   - Or: Press the power button (if configured to sleep)

3. **Wake the system**:
   - Press any key on the keyboard
   - Or: Click the mouse
   - Or: Press the power button
   - Or: Open the laptop lid

4. **Observe the logs** in the console window:
   - You should see suspend and resume events with timestamps
   - Note the event types and details logged

5. **Repeat with the second application**:
   - Stop the first application (`Ctrl+C`)
   - Start the Win32Callbacks version
   - Repeat steps 2-4

6. **Compare the output**:
   - Compare timing, event details, and reliability
   - Check if automatic vs. user-initiated resume is distinguishable

#### Method 2: Command Line Sleep

You can also use the Windows command-line tool:
```bash
# Open a separate command prompt and run:
rundll32.exe powrprof.dll,SetSuspendState 0,1,0
```

#### Method 3: Scheduled Task (For Automatic Resume Testing)

1. Create a scheduled task to wake the computer at a specific time
2. Put the system to sleep manually
3. Let the scheduled task wake the system automatically
4. Compare how each application reports automatic vs. manual resume

### Expected Behavior

**On Suspend:**
- Both applications should detect and log the suspend event
- Timestamp should be recorded before the system sleeps

**On Resume:**
- Both applications should detect and log the resume event immediately after waking
- The Win32 version may distinguish between automatic and user-initiated resume
- The managed version provides less granular resume information

## Comparison of Approaches

### Managed .NET (`SystemEvents`)

**Pros:**
- ✅ Simple and clean API
- ✅ Minimal code required
- ✅ Automatic message pump setup
- ✅ No P/Invoke complexity

**Cons:**
- ❌ Limited event detail (only `Resume`, `Suspend`, `StatusChange`)
- ❌ Cannot distinguish between automatic and user-initiated resume
- ❌ Less control over threading and message processing
- ❌ Requires STA-compatible message pump (handled internally)

**Use Cases:**
- Simple power event monitoring
- Applications that don't need detailed power event information
- Cross-platform code with Windows-specific extensions

### Win32 P/Invoke (`WM_POWERBROADCAST`)

**Pros:**
- ✅ Detailed power event information
- ✅ Distinguishes between `PBT_APMRESUMESUSPEND` (user-initiated) and `PBT_APMRESUMEAUTOMATIC` (automatic)
- ✅ More granular control over event handling
- ✅ Direct access to wParam/lParam values
- ✅ Can handle additional power events (`PBT_APMQUERYSUSPEND`, `PBT_POWERSETTINGCHANGE`, etc.)

**Cons:**
- ❌ More complex implementation
- ❌ Requires P/Invoke declarations
- ❌ Must create a hidden window and message pump
- ❌ More boilerplate code
- ❌ Manual resource management (window cleanup)

**Use Cases:**
- Applications that need to distinguish resume types
- Advanced power management scenarios
- Applications that need to deny suspend requests
- Diagnostic and monitoring tools

### Why the Win32 Implementation Uses a Hidden Window

**Alternatives Considered:**
1. **`RegisterPowerSettingNotification` with console window**: Requires a window handle; console windows don't reliably receive broadcasts
2. **`SetConsoleCtrlHandler`**: Only handles Ctrl+C/logoff, not power events
3. **Polling**: Inefficient and unreliable
4. **Service**: Overkill for a console application

**Chosen Approach: Message-Only Window**
- A message-only window (parent = `HWND_MESSAGE`) is lightweight and invisible
- Reliably receives `WM_POWERBROADCAST` messages
- Standard Windows pattern for console apps needing window messages
- Clean separation of concerns with dedicated window class

## Observed Limitations / Caveats

### Console Application Limitations

**General:**
- Console applications don't automatically participate in Windows message broadcasting
- Both implementations require special setup to receive power notifications
- Fast User Switching may affect event delivery
- Locked screen may delay or prevent event delivery in some configurations

**Managed (`SystemEvents`):**
- Events are delivered on a background thread, not the main thread
- The internal message pump can be affected by thread synchronization issues
- Limited documentation on thread safety guarantees
- No control over event delivery timing

**Win32 P/Invoke:**
- Requires manual window and message pump management
- Window procedure runs on the message loop thread
- Must be careful with thread safety when logging from window procedure
- Native resources require proper cleanup (Dispose pattern)

### Timing Considerations

- **Suspend event**: Delivered shortly before actual suspension; minimal time to respond
- **Resume event**: Delivered shortly after resume; system may still be initializing
- **Hibernate vs. Sleep**: Both are treated as suspend/resume events
- **Hybrid Sleep**: Treated as suspend/resume

### Known Issues

1. **Missed Events**: If the application is suspended while initializing, early events may be missed
2. **Rapid Sleep/Wake**: Very fast sleep/wake cycles may result in batched events
3. **Critical Battery**: System may suspend without delivering PBT_APMSUSPEND
4. **Permission Issues**: No special permissions required, but some enterprise policies may affect event delivery

## Project Structure

```
test-resume-from-standby/
├── test-resume-from-standby.sln          # Solution file
├── README.md                             # This file
├── src/
│   ├── ResumeMonitor.ManagedEvents/      # Managed .NET implementation
│   │   ├── ResumeMonitor.ManagedEvents.csproj
│   │   ├── Program.cs                    # Main entry point and event handling
│   │   └── ConsoleLogger.cs              # Colored logging helper
│   └── ResumeMonitor.Win32Callbacks/     # Win32 P/Invoke implementation
│       ├── ResumeMonitor.Win32Callbacks.csproj
│       ├── Program.cs                    # Main entry point
│       ├── ConsoleLogger.cs              # Colored logging helper
│       ├── NativeMethods.cs              # P/Invoke declarations
│       └── PowerMonitorWindow.cs         # Hidden window and message loop
```

## Implementation Highlights

### Managed Events (SystemEvents)

- Uses `Microsoft.Win32.SystemEvents.PowerModeChanged`
- Handles `PowerModes.Resume`, `PowerModes.Suspend`, and `PowerModes.StatusChange`
- Graceful Ctrl+C handling with proper cleanup
- Thread-safe console logging with colors

### Win32 Callbacks (P/Invoke)

- Creates a message-only window (`HWND_MESSAGE` parent)
- Implements custom window procedure (`WndProc`) to receive `WM_POWERBROADCAST`
- Decodes all power event types:
  - `PBT_APMSUSPEND` - System suspending
  - `PBT_APMRESUMESUSPEND` - User-initiated resume
  - `PBT_APMRESUMEAUTOMATIC` - Automatic resume
  - `PBT_APMRESUMECRITICAL` - Critical resume
  - `PBT_APMPOWERSTATUSCHANGE` - Power status change
  - And more...
- Proper native resource cleanup with `IDisposable`
- Standard Windows message loop

## Troubleshooting

**Application doesn't start:**
- Ensure .NET 8.0 SDK is installed: `dotnet --version`
- Try building first: `dotnet build`

**No events detected:**
- Ensure the application is running before putting the system to sleep
- Try a full sleep cycle (not just screen off)
- Check Windows power settings (some policies may prevent sleep)
- Try running from an elevated command prompt (admin)

**Events are delayed:**
- This is normal; resume events are delivered after system wake-up
- Network and disk operations may delay event delivery

**Application crashes on startup (Win32 version):**
- Ensure the application has permission to create windows
- Check that no antivirus is blocking window creation

## License

This is a demonstration/testing solution. Refer to repository license for details.

## Contributing

This is a test solution for comparing power event monitoring approaches. Contributions to improve testing procedures or add additional comparison metrics are welcome.

## Additional Resources

- [Microsoft.Win32.SystemEvents Documentation](https://learn.microsoft.com/en-us/dotnet/api/microsoft.win32.systemevents)
- [WM_POWERBROADCAST Message](https://learn.microsoft.com/en-us/windows/win32/power/wm-powerbroadcast)
- [Power Management Events](https://learn.microsoft.com/en-us/windows/win32/power/power-management-events)
- [.NET 8 Documentation](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-8)