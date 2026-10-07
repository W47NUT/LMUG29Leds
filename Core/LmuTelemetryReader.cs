using System.IO;
using System.IO.MemoryMappedFiles;

namespace LMUG29Leds.Core;

internal sealed class LmuTelemetryReader : IDisposable
{
    private const string MappingName = "LMU_Data";

    private const int VehicleSize = 1888;
    private const int RpmOffset = 356;
    private const int ThrottleOffset = 388;
    private const int MaxRpmOffset = 532;
    private const int SpeedLimiterOffset = 604;

    private const long FirstVehicleRpmOffset = 128824;
    private const long TelemetryBase = FirstVehicleRpmOffset - RpmOffset;
    private const long HeaderBase = TelemetryBase - 4;

    private MemoryMappedFile? _mapping;
    private MemoryMappedViewAccessor? _view;

    public bool IsConnected => _view is not null;

    public bool TryConnect()
    {
        if (_view is not null)
        {
            return true;
        }

        try
        {
            MemoryMappedFile mapping = MemoryMappedFile.OpenExisting(
                MappingName,
                MemoryMappedFileRights.Read);

            MemoryMappedViewAccessor view = mapping.CreateViewAccessor(
                0,
                0,
                MemoryMappedFileAccess.Read);

            if (view.Capacity < TelemetryBase + VehicleSize ||
                view.Capacity <= HeaderBase + 2)
            {
                view.Dispose();
                mapping.Dispose();
                return false;
            }

            _mapping = mapping;
            _view = view;
            return true;
        }
        catch (FileNotFoundException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
    }

    public bool TryRead(out TelemetrySnapshot snapshot)
    {
        snapshot = default;

        MemoryMappedViewAccessor? view = _view;
        if (view is null)
        {
            return false;
        }

        try
        {
            long availableSlots = (view.Capacity - TelemetryBase) / VehicleSize;
            if (availableSlots <= 0)
            {
                Disconnect();
                return false;
            }

            byte activeVehicles = view.ReadByte(HeaderBase);
            byte playerVehicleIndex = view.ReadByte(HeaderBase + 1);
            byte playerHasVehicle = view.ReadByte(HeaderBase + 2);

            if (activeVehicles > availableSlots)
            {
                Disconnect();
                return false;
            }

            if (playerHasVehicle == 0)
            {
                snapshot = new TelemetrySnapshot(
                    HasVehicle: false,
                    Rpm: 0,
                    MaxRpm: 0,
                    Throttle: 0,
                    SpeedLimiter: false);

                return true;
            }

            if (activeVehicles == 0 ||
                playerVehicleIndex >= activeVehicles ||
                playerVehicleIndex >= availableSlots)
            {
                return false;
            }

            long playerBase = checked(
                TelemetryBase + (playerVehicleIndex * (long)VehicleSize));

            if (!CanRead(view, playerBase + RpmOffset, sizeof(double)) ||
                !CanRead(view, playerBase + ThrottleOffset, sizeof(double)) ||
                !CanRead(view, playerBase + MaxRpmOffset, sizeof(double)) ||
                !CanRead(view, playerBase + SpeedLimiterOffset, sizeof(byte)))
            {
                Disconnect();
                return false;
            }

            double rpm = view.ReadDouble(playerBase + RpmOffset);
            double throttle = view.ReadDouble(playerBase + ThrottleOffset);
            double maxRpm = view.ReadDouble(playerBase + MaxRpmOffset);
            bool speedLimiter = view.ReadByte(playerBase + SpeedLimiterOffset) != 0;

            rpm = SanitizeRpm(rpm);
            maxRpm = SanitizeMaxRpm(maxRpm);
            throttle = double.IsFinite(throttle)
                ? Math.Clamp(throttle, 0.0, 1.0)
                : 0.0;

            if (maxRpm > 0 && rpm > maxRpm * 1.5)
            {
                rpm = 0;
            }

            snapshot = new TelemetrySnapshot(
                HasVehicle: true,
                Rpm: rpm,
                MaxRpm: maxRpm,
                Throttle: throttle,
                SpeedLimiter: speedLimiter);

            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            Disconnect();
            return false;
        }
        catch (IOException)
        {
            Disconnect();
            return false;
        }
        catch (ObjectDisposedException)
        {
            Disconnect();
            return false;
        }
    }

    public void Disconnect()
    {
        _view?.Dispose();
        _mapping?.Dispose();

        _view = null;
        _mapping = null;
    }

    public void Dispose()
    {
        Disconnect();
    }

    private static bool CanRead(
        MemoryMappedViewAccessor view,
        long offset,
        int length)
    {
        if (offset < 0 || length < 0)
        {
            return false;
        }

        return offset <= view.Capacity - length;
    }

    private static double SanitizeRpm(double rpm)
    {
        return double.IsFinite(rpm) && rpm is >= 0 and <= 50_000
            ? rpm
            : 0;
    }

    private static double SanitizeMaxRpm(double maxRpm)
    {
        return double.IsFinite(maxRpm) && maxRpm is >= 500 and <= 50_000
            ? maxRpm
            : 0;
    }
}
