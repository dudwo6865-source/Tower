using System;

// 화면과 무관하게 점수 상승 연출의 현재 정수 점수를 계산한다.
public static class ScoreRevealProgress
{
    public static double Sample(double total, double elapsed, double duration)
    {
        if (total <= 0 || double.IsNaN(total) || double.IsInfinity(total)) return 0;
        if (duration <= 0 || elapsed >= duration) return total;
        double t = Math.Max(0, Math.Min(1, elapsed / duration));
        double eased = 1 - Math.Pow(1 - t, 3);
        return Math.Min(total, Math.Floor(total * eased));
    }
}
