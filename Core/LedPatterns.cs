namespace LMUG29Leds.Core;

internal static class LedPatterns
{
    private static readonly byte[] PitLimiterSequence =
    [
        0x01, 0x02, 0x04, 0x08, 0x10, 0x08, 0x04, 0x02
    ];

    public static byte GetRpmMask(double rpmRatio, long tickCount)
    {
        double ratio = Math.Clamp(rpmRatio, 0.0, 1.2);

        if (ratio >= 0.98)
        {
            return ((tickCount / 100) % 2 == 0) ? (byte)0x1F : (byte)0x00;
        }

        if (ratio >= 0.95) return 0x1F;
        if (ratio >= 0.90) return 0x0F;
        if (ratio >= 0.84) return 0x07;
        if (ratio >= 0.78) return 0x03;
        if (ratio >= 0.70) return 0x01;

        return 0x00;
    }

    public static byte GetPitLimiterMask(long tickCount)
    {
        int step = (int)((tickCount / 90) % PitLimiterSequence.Length);
        return PitLimiterSequence[step];
    }
}
