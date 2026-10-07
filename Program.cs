using System.IO.MemoryMappedFiles;
using HidSharp;

const int LogitechVid = 0x046D;
const int G29Pid = 0xC24F;

const int VehicleSize = 1888;

const int RpmOffsetInsideVehicle = 356;
const int ThrottleOffsetInsideVehicle = 388;
const int MaxRpmOffsetInsideVehicle = 532;
const int SpeedLimiterOffsetInsideVehicle = 604;

const long FirstVehicleRpmOffset = 128824;

const long TelemetryBase =
    FirstVehicleRpmOffset - RpmOffsetInsideVehicle;

const long HeaderBase =
    TelemetryBase - 4;

Console.Title = "LMUG29Leds";

Console.WriteLine("LMUG29Leds");
Console.WriteLine("Le Mans Ultimate -> Logitech G29 LEDs");
Console.WriteLine("Compatible with lmuFFB.");
Console.WriteLine();

//
// FIND G29
//

Console.WriteLine("Searching for Logitech G29...");

HidStream? wheelStream = null;
int outputLength = 0;

foreach (var device in DeviceList.Local.GetHidDevices(
    LogitechVid,
    G29Pid))
{
    try
    {
        int length = device.GetMaxOutputReportLength();

        if (length < 8)
            continue;

        if (!device.TryOpen(out HidStream stream))
            continue;

        try
        {
            stream.Write(BuildLedReport(length, 0x00));

            wheelStream = stream;
            outputLength = length;

            Console.WriteLine(
                $"G29 LED interface found ({length}-byte output report)."
            );

            break;
        }
        catch
        {
            stream.Dispose();
        }
    }
    catch
    {
    }
}

if (wheelStream is null)
{
    Console.WriteLine("ERROR: No writable G29 LED interface found.");
    return;
}

//
// STARTUP SWEEP
//

StartupSweep(wheelStream, outputLength);

//
// CONNECT TO LMU
//

MemoryMappedFile? mmf;

try
{
    mmf = MemoryMappedFile.OpenExisting(
        "LMU_Data",
        MemoryMappedFileRights.Read
    );
}
catch
{
    Console.WriteLine("ERROR: LMU shared memory not found.");

    TrySetLeds(wheelStream, outputLength, 0x00);
    wheelStream.Dispose();

    return;
}

using (wheelStream)
using (mmf)
using (var mem = mmf.CreateViewAccessor(
    0,
    0,
    MemoryMappedFileAccess.Read))
{
    Console.WriteLine("Connected to LMU.");
    Console.WriteLine("G29 LEDs ACTIVE");
    Console.WriteLine("Press Ctrl+C to quit.");
    Console.WriteLine();

    using var cancel = new CancellationTokenSource();

    Console.CancelKeyPress += (_, e) =>
    {
        e.Cancel = true;
        cancel.Cancel();
    };

    byte lastMask = 0xFF;

    try
    {
        while (!cancel.IsCancellationRequested)
        {
            byte playerVehicleIdx =
                mem.ReadByte(HeaderBase + 1);

            byte playerHasVehicle =
                mem.ReadByte(HeaderBase + 2);

            double rpm = 0;
            double maxRpm = 0;
            double throttle = 0;

            bool limiterActive = false;

            byte ledMask = 0x00;

            if (playerHasVehicle != 0)
            {
                long playerBase =
                    TelemetryBase +
                    playerVehicleIdx * VehicleSize;

                rpm =
                    mem.ReadDouble(
                        playerBase +
                        RpmOffsetInsideVehicle
                    );

                throttle =
                    mem.ReadDouble(
                        playerBase +
                        ThrottleOffsetInsideVehicle
                    );

                maxRpm =
                    mem.ReadDouble(
                        playerBase +
                        MaxRpmOffsetInsideVehicle
                    );

                byte speedLimiter =
                    mem.ReadByte(
                        playerBase +
                        SpeedLimiterOffsetInsideVehicle
                    );

                limiterActive = speedLimiter != 0;

                //
                // PIT LIMITER OVERRIDES RPM DISPLAY.
                //
                if (limiterActive)
                {
                    ledMask = GetPitLimiterPattern();
                }
                else if (
                    double.IsFinite(rpm) &&
                    double.IsFinite(maxRpm) &&
                    rpm >= 0 &&
                    maxRpm > 0)
                {
                    double rpmRatio =
                        Math.Clamp(rpm / maxRpm, 0.0, 1.2);

                    ledMask =
                        GetLedMask(rpmRatio);
                }
            }

            if (ledMask != lastMask)
            {
                TrySetLeds(
                    wheelStream,
                    outputLength,
                    ledMask
                );

                lastMask = ledMask;
            }

            string status =
                limiterActive
                    ? "PIT LIMITER"
                    : "RACING     ";

            Console.Write(
                $"\r{status} | " +
                $"RPM {rpm,5:F0}/{maxRpm,5:F0} | " +
                $"Throttle {throttle,5:P0} | " +
                $"LEDs {MaskToString(ledMask)}    "
            );

            Thread.Sleep(20);
        }
    }
    finally
    {
        TrySetLeds(
            wheelStream,
            outputLength,
            0x00
        );

        Console.WriteLine();
        Console.WriteLine("LEDs off.");
    }
}

//
// NORMAL RPM LEDs
//

static byte GetLedMask(double rpmRatio)
{
    // Flash all five at extreme RPM.
    if (rpmRatio >= 0.98)
    {
        return
            ((Environment.TickCount64 / 100) % 2 == 0)
                ? (byte)0x1F
                : (byte)0x00;
    }

    if (rpmRatio >= 0.95)
        return 0x1F;

    if (rpmRatio >= 0.90)
        return 0x0F;

    if (rpmRatio >= 0.84)
        return 0x07;

    if (rpmRatio >= 0.78)
        return 0x03;

    if (rpmRatio >= 0.70)
        return 0x01;

    return 0x00;
}

//
// PIT LIMITER ANIMATION
//
// LED sweeps left -> right -> left.
//
static byte GetPitLimiterPattern()
{
    byte[] sequence =
    {
        0x01, // ●○○○○
        0x02, // ○●○○○
        0x04, // ○○●○○
        0x08, // ○○○●○
        0x10, // ○○○○●
        0x08, // ○○○●○
        0x04, // ○○●○○
        0x02  // ○●○○○
    };

    // Advance roughly every 90 ms.
    long step =
        (Environment.TickCount64 / 90)
        % sequence.Length;

    return sequence[step];
}

//
// G29 HID REPORT
//

static byte[] BuildLedReport(
    int outputLength,
    byte ledMask)
{
    var report =
        new byte[outputLength];

    report[0] = 0x00;
    report[1] = 0xF8;
    report[2] = 0x12;
    report[3] = ledMask;
    report[4] = 0x00;
    report[5] = 0x00;
    report[6] = 0x00;
    report[7] = 0x01;

    return report;
}

static void TrySetLeds(
    HidStream stream,
    int outputLength,
    byte mask)
{
    try
    {
        stream.Write(
            BuildLedReport(
                outputLength,
                mask
            )
        );
    }
    catch
    {
    }
}

//
// APP STARTUP ANIMATION
//

static void StartupSweep(
    HidStream stream,
    int outputLength)
{
    byte[] sequence =
    {
        0x01,
        0x03,
        0x07,
        0x0F,
        0x1F,
        0x0F,
        0x07,
        0x03,
        0x01,
        0x00
    };

    foreach (byte mask in sequence)
    {
        TrySetLeds(
            stream,
            outputLength,
            mask
        );

        Thread.Sleep(70);
    }
}

static string MaskToString(byte mask)
{
    return string.Concat(
        Enumerable.Range(0, 5)
            .Select(i =>
                (mask & (1 << i)) != 0
                    ? "●"
                    : "○"
            )
    );
}