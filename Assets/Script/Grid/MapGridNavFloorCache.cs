using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// MapGrid의 칸별 NavMesh 높이 프로필을 계산하고 캐싱한다.
/// NavMesh는 건물이 놓이거나 철거될 때, 또는 맵 바운즈가 바뀔 때만 실제로 변하므로
/// 칸마다 한 번만 계산해두고 Invalidate가 불릴 때까지 재사용한다.
/// MapGrid 전용 내부 클래스이며 다른 곳에서 직접 쓰지 않는다.
/// </summary>
internal sealed class MapGridNavFloorCache
{
    public readonly struct CellFloors
    {
        // 이 칸 중심에서 관측된 NavMesh 표면 높이(오름차순, 층마다 하나).
        public readonly float[] Raw;

        // Raw 중 칸 전체(중심+네 모서리)가 실제로 덮인 높이만 남긴 것.
        public readonly float[] Covered;

        public CellFloors(float[] raw, float[] covered)
        {
            Raw = raw;
            Covered = covered;
        }

        public static readonly CellFloors Empty =
            new CellFloors(System.Array.Empty<float>(), System.Array.Empty<float>());
    }

    readonly MapGrid grid;
    readonly Dictionary<Vector2Int, CellFloors> cache = new Dictionary<Vector2Int, CellFloors>();
    readonly List<float> rawScratch = new List<float>(4);

    public MapGridNavFloorCache(MapGrid grid)
    {
        this.grid = grid;
    }

    public void Invalidate()
    {
        cache.Clear();
    }

    public CellFloors Get(Vector2Int cell)
    {
        if (cache.TryGetValue(cell, out CellFloors cached))
            return cached;

        CellFloors computed = Compute(cell);
        cache[cell] = computed;
        return computed;
    }

    CellFloors Compute(Vector2Int cell)
    {
        int count = grid.CollectNavMeshSurfaceHeights(cell, rawScratch);

        if (count <= 0)
            return CellFloors.Empty;

        float[] raw = rawScratch.ToArray();
        List<float> coveredList = null;

        for (int i = 0; i < raw.Length; i++)
        {
            if (!grid.IsCellCoveredAtHeight(cell, raw[i]))
                continue;

            if (coveredList == null)
                coveredList = new List<float>(raw.Length);

            coveredList.Add(raw[i]);
        }

        float[] covered = coveredList != null
            ? coveredList.ToArray()
            : System.Array.Empty<float>();

        return new CellFloors(raw, covered);
    }
}
