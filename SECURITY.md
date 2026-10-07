# Security Policy

## Supported version

Security fixes are currently provided for the latest release only.

## Security posture

LMUG29Leds is intentionally small and local-only.

The application:

- does not require administrator privileges;
- does not install a driver or Windows service;
- does not modify the registry or Windows startup;
- does not listen on a network port;
- does not make HTTP or other network requests;
- does not include an updater, downloader, analytics, or app telemetry;
- opens only the Logitech G29 USB HID device identified by Logitech VID `0x046D` and G29 PID `0xC24F`;
- writes only the fixed G29 LED output report, with the LED bit mask constrained to five bits;
- opens Le Mans Ultimate shared memory (`LMU_Data`) read-only;
- bounds-checks the player-vehicle index and shared-memory offsets before reading telemetry.

Release binaries are built by GitHub Actions from the public repository and are published with a SHA-256 checksum.

## Reporting a vulnerability

Please avoid posting exploit details in a public issue.

Use GitHub's private vulnerability reporting / Security Advisory flow when available. If that is unavailable, open a minimal issue requesting a private contact channel without including sensitive exploit details.
