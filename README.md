# test-resume-from-standby

Windows-focused .NET 8 solution for observing and comparing power resume behavior with two approaches:

- **Managed .NET events** (`ResumeMonitor.ManagedEvents`)
- **Native Win32 callbacks** (`ResumeMonitor.Win32Callbacks`)

## Project overview

The goal is to capture suspend/resume-related notifications when a machine enters standby/sleep and wakes again, then compare what each approach reports and how reliably it reports it in a console process.

## Why there are two implementations

- **Managed implementation**: Uses `Microsoft.Win32.SystemEvents.PowerModeChanged` to observe high-level power mode changes with minimal native code.
- **Win32 implementation**: Uses a hidden message-only window and `WM_POWERBROADCAST` to capture raw native power broadcast events and technical details (`wParam`, `lParam`).

This side-by-side setup helps compare convenience vs fidelity.

## Prerequisites

- Windows 10/11
- .NET SDK 8.0+
- A shell with permission to put the system to sleep and wake it

> The projects target `net8.0-windows` and are intended to run on Windows only.

## Solution structure

- `test-resume-from-standby.sln`
- `src/ResumeMonitor.ManagedEvents`
- `src/ResumeMonitor.Win32Callbacks`
- `README.md`

## Build instructions

From repository root:

```bash
dotnet restore test-resume-from-standby.sln
dotnet build test-resume-from-standby.sln -c Release
```

## Run instructions

### 1) Managed events monitor

```bash
dotnet run --project src/ResumeMonitor.ManagedEvents/ResumeMonitor.ManagedEvents.csproj
```

Logs include:

- timestamp
- event type (`PowerModeChanged`)
- power mode (`Suspend`, `Resume`, `StatusChange`)
- diagnostic context (PID/TID)

### 2) Win32 callbacks monitor

```bash
dotnet run --project src/ResumeMonitor.Win32Callbacks/ResumeMonitor.Win32Callbacks.csproj
```

Logs include:

- timestamp
- raw message ID (`WM_POWERBROADCAST`)
- `wParam` and `lParam`
- human-readable interpretation (e.g. `PBT_APMSUSPEND`, `PBT_APMRESUMEAUTOMATIC`, `PBT_APMRESUMESUSPEND`)
- diagnostic context (PID/TID)

## How to test suspend/resume behavior on Windows

1. Build the solution.
2. Start one of the console applications.
3. Put the machine into sleep/standby.
4. Wake the machine.
5. Observe the logged events.
6. Stop that app and run the second application.
7. Repeat sleep/wake.
8. Compare output and behavior.

## Comparison of Approaches

- **Managed (`SystemEvents`)**
  - Pros: simple API, minimal interop, easy to maintain.
  - Cons: fewer low-level details; limited distinction around resume reasons.

- **Win32 (`WM_POWERBROADCAST`)**
  - Pros: lower-level visibility, includes more distinct resume event variants and raw payload data.
  - Cons: requires native interop, explicit message-loop handling, and more cleanup responsibility.

## Observed Limitations / Caveats

- Console apps are not naturally message-loop-driven like GUI apps. Event delivery can depend on correctly initializing and maintaining message processing.
- `SystemEvents.PowerModeChanged` does not provide the same detailed resume distinctions as Win32 (`PBT_APMRESUMEAUTOMATIC` vs `PBT_APMRESUMESUSPEND`).
- Hardware/firmware, power plans, wake source, and Windows version can influence which power events are emitted.
- Sleeping in virtual machines or remote sessions may produce different behavior than physical desktops/laptops.

## Why the Win32 strategy uses a hidden message window

A hidden **message-only window** is a reliable and lightweight mechanism for a console process to receive `WM_POWERBROADCAST` notifications. Compared with alternatives (such as service-specific notifications or polling), this approach keeps the app as a standard console executable while still using the canonical Windows message path for power events.
