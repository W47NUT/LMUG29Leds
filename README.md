# LMUG29Leds

Restores the Logitech G29's built-in RPM LEDs in **Le Mans Ultimate**, including when using **lmuFFB** in Exclusive mode.

## Why?

Le Mans Ultimate normally controls the G29's RPM LEDs itself.

When using lmuFFB in Exclusive mode, force feedback works, but the G29's RPM LEDs no longer receive their normal updates.

LMUG29Leds works around that by reading LMU's shared-memory telemetry and controlling the G29 LEDs directly through USB HID.

This allows:

- **lmuFFB** to handle force feedback
- **LMUG29Leds** to handle the wheel LEDs

at the same time.

## Current Features

- Live RPM / shift LEDs
- Automatically scales using the car's reported maximum RPM
- Works while lmuFFB is running
- Pit limiter scanner animation
- Startup LED sweep
- Flashing shift warning near maximum RPM
- Automatically turns LEDs off when the program exits
- Reads the player's vehicle directly from LMU telemetry

### Normal RPM display

The five LEDs progressively illuminate as engine RPM increases.

```text
○○○○○
●○○○○
●●○○○
●●●○○
●●●●○
●●●●●
```

### Pit limiter

When the pit limiter is active, the RPM display is temporarily replaced by a scanner animation:

```text
●○○○○
○●○○○
○○●○○
○○○●○
○○○○●
○○○●○
○○●○○
○●○○○
```

When the limiter is switched off, normal RPM LEDs immediately resume.

## Requirements

- Windows
- Le Mans Ultimate
- Logitech G29
- LMU shared-memory plugins enabled
- .NET 8

lmuFFB is **not required**, but solving the loss of G29 LEDs while using lmuFFB is the primary reason this project exists.

## LMU Setup

In Le Mans Ultimate, make sure:

```text
Settings -> Gameplay -> Enable Plugins
```

is enabled.

LMUG29Leds connects to LMU's:

```text
LMU_Data
```

shared-memory mapping.

## Running From Source

Clone the repository:

```powershell
git clone https://github.com/W47NUT/LMUG29Leds.git
cd LMUG29Leds
```

Restore dependencies:

```powershell
dotnet restore
```

Run:

```powershell
dotnet run
```

You should see:

```text
G29 LED interface found
Connected to LMU
G29 LEDs ACTIVE
```

Enter a session in LMU and the LEDs should respond to engine RPM.

Press `Ctrl+C` to exit.

## Hardware

Currently tested with:

- Logitech G29

G923 support may be added later, but is not currently tested or claimed.

## Status

LMUG29Leds is currently an early working prototype.

The LMU telemetry layout used by the application has been verified against the current game build, but future LMU updates could require changes.

Planned improvements include:

- Standalone Windows executable
- Configuration file
- Configurable RPM thresholds
- Configurable LED animations
- Additional useful race/session indications
- Improved device detection
- Additional Logitech wheel support

## License

License information will be added before the first packaged release.

## Disclaimer

This is an unofficial community project.

It is not affiliated with Studio 397, Motorsport Games, Logitech, or lmuFFB.
