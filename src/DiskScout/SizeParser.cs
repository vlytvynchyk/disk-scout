namespace DiskScout;

public static class SizeParser
{
    private static readonly (string Suffix, long Multiplier)[] Multipliers =
    [
        ("TB", 1024L * 1024 * 1024 * 1024),
        ("GB", 1024L * 1024 * 1024),
        ("MB", 1024L * 1024),
        ("KB", 1024L),
        ("B", 1L)
    ];

    public static long Parse(string sizeStr, long defaultValue = 1024 * 1024)
    {
        if (string.IsNullOrWhiteSpace(sizeStr))
            return defaultValue;

        sizeStr = sizeStr.Trim().ToUpperInvariant();

        foreach (var (suffix, mult) in Multipliers)
        {
            if (sizeStr.EndsWith(suffix))
            {
                var numStr = sizeStr[..^suffix.Length].Trim();
                if (double.TryParse(numStr, out double val))
                    return (long)(val * mult);
            }
        }

        if (long.TryParse(sizeStr, out long bytes))
            return bytes;

        return defaultValue;
    }
}
