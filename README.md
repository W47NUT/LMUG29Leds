# LMUG29Leds

LMUG29Leds restores the Logitech G29's built-in RPM LEDs in **Le Mans Ultimate**, including when using **lmuFFB** in Exclusive mode.

It is a small, local-only Windows utility: LMU provides telemetry, lmuFFB can continue handling force feedback, and LMUG29Leds controls only the five LEDs on the G29.

## Features

- Native Windows desktop UI
- Live LMU / G29 connection status
- Live RPM and throttle display
- Progressive RPM / shift lights
- Automatic scaling from the car's reported maximum RPM
- Pit-limiter scanner animation
- Flashing near-redline shift warning
- Physical `Test on wheel` previews for each light pattern
- Automatic reconnect for LMU and the wheel
- LEDs are switched off on exit
- Works alongside lmuFFB

## Light behavior

### RPM / Shift

During normal driving, the five LEDs progressively illuminate as RPM rises.

```text
○○○○○
●○○○○
●●○○○
●●●○○
●●●●○
●●●●●
```

### Pit Limiter

While the pit limiter is active, a scanner pattern temporarily replaces the normal RPM display.

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

When the limiter is disabled, normal RPM behavior immediately resumes.

### Shift Warning

At the final shift-warning range, all five LEDs flash.

## Requirements

- Windows 10/11 x64
- Le Mans Ultimate
- Logitech G29
- LMU shared-memory plugins enabled

lmuFFB is **not required**, but restoring the G29 LEDs while using lmuFFB is the reason this project exists.

## LMU setup

In Le Mans Ultimate, enable:

```text
Settings -> Gameplay -> Enable Plugins
```

LMUG29Leds reads the `LMU_Data` shared-memory mapping.

## Download

Use the latest GitHub Release and download:

- `LMUG29Leds.exe`
- `LMUG29Leds.exe.sha256` if you want to verify the release checksum

The release executable is self-contained and does not require the .NET SDK.

> The executable is currently unsigned. Windows SmartScreen may warn about a new or low-reputation executable. Releases are built publicly by GitHub Actions and include a SHA-256 checksum.

## Security / privacy

LMUG29Leds is intentionally narrow in scope.

It:

- runs as the current user and does **not** request administrator privileges;
- does **not** use the network;
- does **not** contain an updater, downloader, analytics, or app telemetry;
- does **not** install a service or driver;
- opens LMU shared memory read-only;
- targets only Logitech VID `0x046D` / G29 PID `0xC24F`;
- constrains HID output to the five G29 LED bits;
- validates shared-memory bounds and telemetry values before use.

See [SECURITY.md](SECURITY.md) for the security policy.

## Building from source

Install the .NET 8 SDK, then:

```powershell
git clone https://github.com/W47NUT/LMUG29Leds.git
cd LMUG29Leds
dotnet restore
dotnet run
```

To create a self-contained executable locally:

```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=false
```

## Hardware

Currently tested:

- Logitech G29

Other Logitech wheels are not claimed as supported until they are explicitly implemented and tested.

## Dependency

The application intentionally has one NuGet dependency:

- HidSharp 2.6.4

GitHub Dependabot, NuGet audit, compiler analyzers, and CodeQL are enabled for the repository.

## License

MIT. See [LICENSE](LICENSE).

## Disclaimer

This is an unofficial community project and is not affiliated with Studio 397, Motorsport Games, Logitech, or lmuFFB.
