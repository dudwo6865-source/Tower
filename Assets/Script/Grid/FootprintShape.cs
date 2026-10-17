using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 건물 점유 칸의 모양(마스크) 계산을 한곳에 모은 헬퍼입니다.
/// 마스크는 footprint 직사각형(바깥 틀) 안에서 실제로 쓰는 칸만 true인 배열이고,
/// 인덱스는 x + z * 가로 칸 수입니다. x는 월드 +X, z는 월드 +Z 방향입니다.
/// 마스크가 null이거나 크기가 맞지 않으면 직사각형 전체를 씁니다.
/// </summary>
public static class FootprintShape
{
    public static bool IsValidMask(bool[] mask, Vector2Int size)
    {
        return mask != null && size.x > 0 && size.y > 0 && mask.Length == size.x * size.y;
    }

    /// <summary>(x, z) 칸을 쓰는지 돌려줍니다. x, z는 footprint 원점 기준 상대 좌표입니다.</summary>
    public static bool Contains(bool[] mask, Vector2Int size, int x, int z)
    {
        if (x < 0 || z < 0 || x >= size.x || z >= size.y)
            return false;

        if (!IsValidMask(mask, size))
            return true;

        return mask[x + z * size.x];
    }

    /// <summary>쓰는 칸들의 격자 좌표를 결과 목록에 채웁니다.</summary>
    public static void CollectCells(
        Vector2Int origin,
        Vector2Int size,
        bool[] mask,
        List<Vector2Int> results)
    {
        results.Clear();

        for (int z = 0; z < size.y; z++)
        {
            for (int x = 0; x < size.x; x++)
            {
                if (Contains(mask, size, x, z))
                    results.Add(new Vector2Int(origin.x + x, origin.y + z));
            }
        }
    }

    public static IEnumerable<Vector2Int> IterateCells(
        Vector2Int origin,
        Vector2Int size,
        bool[] mask)
    {
        for (int z = 0; z < size.y; z++)
        {
            for (int x = 0; x < size.x; x++)
            {
                if (Contains(mask, size, x, z))
                    yield return new Vector2Int(origin.x + x, origin.y + z);
            }
        }
    }

    /// <summary>쓰는 칸 중 첫 번째(원점에 가장 가까운) 칸의 상대 좌표입니다. 하나도 없으면 false.</summary>
    public static bool TryGetFirstCell(bool[] mask, Vector2Int size, out Vector2Int cell)
    {
        for (int z = 0; z < size.y; z++)
        {
            for (int x = 0; x < size.x; x++)
            {
                if (!Contains(mask, size, x, z))
                    continue;

                cell = new Vector2Int(x, z);
                return true;
            }
        }

        cell = Vector2Int.zero;
        return false;
    }

    public static int CountCells(bool[] mask, Vector2Int size)
    {
        int count = 0;

        for (int z = 0; z < size.y; z++)
        {
            for (int x = 0; x < size.x; x++)
            {
                if (Contains(mask, size, x, z))
                    count++;
            }
        }

        return count;
    }

    /// <summary>두 모양이 같은 칸을 쓰는지 비교합니다. (마스크 없음 = 직사각형 전체)</summary>
    public static bool SameShape(Vector2Int sizeA, bool[] maskA, Vector2Int sizeB, bool[] maskB)
    {
        if (sizeA != sizeB)
            return false;

        for (int z = 0; z < sizeA.y; z++)
        {
            for (int x = 0; x < sizeA.x; x++)
            {
                if (Contains(maskA, sizeA, x, z) != Contains(maskB, sizeB, x, z))
                    return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 쓰는 칸들을 겹치지 않는 직사각형 몇 개로 묶습니다. NavMesh 장애물 박스를 칸마다 만들지 않고
    /// 최소한으로 만들기 위해 쓴다. (위쪽 행부터 가로로 최대한 늘린 뒤 세로로 늘리는 탐욕 방식)
    /// </summary>
    public static List<RectInt> ToRects(bool[] mask, Vector2Int size)
    {
        var rects = new List<RectInt>();

        if (size.x <= 0 || size.y <= 0)
            return rects;

        bool[] used = new bool[size.x * size.y];

        for (int z = 0; z < size.y; z++)
        {
            for (int x = 0; x < size.x; x++)
            {
                if (used[x + z * size.x] || !Contains(mask, size, x, z))
                    continue;

                int width = 1;
                while (x + width < size.x &&
                       !used[x + width + z * size.x] &&
                       Contains(mask, size, x + width, z))
                {
                    width++;
                }

                int height = 1;
                while (z + height < size.y && IsRowFree(mask, size, used, x, z + height, width))
                    height++;

                for (int dz = 0; dz < height; dz++)
                {
                    for (int dx = 0; dx < width; dx++)
                        used[x + dx + (z + dz) * size.x] = true;
                }

                rects.Add(new RectInt(x, z, width, height));
            }
        }

        return rects;
    }

    static bool IsRowFree(bool[] mask, Vector2Int size, bool[] used, int x, int z, int width)
    {
        for (int dx = 0; dx < width; dx++)
        {
            if (used[x + dx + z * size.x] || !Contains(mask, size, x + dx, z))
                return false;
        }

        return true;
    }
}
