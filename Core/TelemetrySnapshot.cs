namespace LMUG29Leds.Core;

internal readonly record struct TelemetrySnapshot(
    bool HasVehicle,
    double Rpm,
    double MaxRpm,
    double Throttle,
    bool SpeedLimiter);
