using System;

// 소수점을 이월해 소량씩 생성하는 스포너에서도 설정한 증가율을 유지한다.
public static class EnemyMutationMath
{
    public static int AccumulateExtraSpawns(ref double remainder, float bonus)
    {
        remainder += Math.Max(0d, bonus);
        int count = (int)Math.Floor(remainder + 0.0000001d);
        remainder = Math.Max(0d, remainder - count);
        return count;
    }
}
