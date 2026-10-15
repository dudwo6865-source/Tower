using System;

public static class NightSpawnMath
{
    // 나머지는 앞 출격부터 한 마리씩 배분하여 총량을 정확히 보존한다.
    public static int BatchSize(int total, int batch)
    {
        if (batch < 0 || batch >= 3) return 0;
        total = Math.Max(0, total);
        return total / 3 + (batch < total % 3 ? 1 : 0);
    }
    public static int GuardDeficit(int capacity, int alive, int pending) =>
        Math.Max(0, Math.Max(0, capacity) - Math.Max(0, alive) - Math.Max(0, pending));
}
