using System;
using System.Collections.Generic;
using UnityEngine;

// 씬에 두는 절벽 레이어입니다.
// Top 셀 + 가장자리(벽/코너), Hill 한 칸 페인트 시 주변 벽/코너 오토타일.
[DisallowMultipleComponent]
[ExecuteAlways]
public class CliffPainter : MonoBehaviour
{
    public enum EdgeKind
    {
        Straight,
        OuterCorner,
        InnerCorner,
    }

    [Serializable]
    public struct CellCoord : IEquatable<CellCoord>
    {
        public int x;
        public int z;

        public CellCoord(int x, int z)
        {
            this.x = x;
            this.z = z;
        }

        public bool Equals(CellCoord other) => x == other.x && z == other.z;
        public override bool Equals(object obj) => obj is CellCoord other && Equals(other);
        public override int GetHashCode() => (x * 397) ^ z;
        public override string ToString() => $"({x},{z})";
    }

    [Serializable]
    public struct TopEntry
    {
        public CellCoord cell;
        [Tooltip("이 칸의 최고 층 (0=1층). 아래에 0..maxLayer 가 모두 있다고 봅니다.")]
        public int maxLayer;
    }

    [Serializable]
    public struct HillEntry
    {
        public CellCoord cell;
        [Tooltip("Hill이 속한 Top 층 (0=1층).")]
        public int layer;
    }

    [Serializable]
    public struct RampEntry
    {
        public CellCoord cell;
        public int direction;
    }

    [Header("Setup")]
    public CliffTileSet tileSet;

    [Tooltip("언덕용 타일 세트입니다. 여러 종류를 만들어 바꿔 끼울 수 있습니다.")]
    public HillTileSet hillTileSet;

    [Tooltip("생성된 모듈이 들어갈 부모입니다. 비워두면 자동 생성합니다.")]
    public Transform generatedRoot;

    [Tooltip("맵 원점(왼쪽 아래 코너). 타일 (0,0)의 남서 코너입니다.")]
    public Vector3 gridOrigin;

    [Tooltip("저지대(절벽면이 서는 바닥) 높이(Y)입니다. Top은 이 값 + cliffHeight×(층+1)에 배치됩니다.")]
    public float baseHeight;

    [Header("Paint Data")]
    [Tooltip("칠해진 Top입니다. maxLayer가 높을수록 윗층이 쌓입니다.")]
    public List<TopEntry> tops = new List<TopEntry>();

    [Tooltip("구버전 호환용. 자동으로 tops로 이전됩니다.")]
    public List<CellCoord> topCells = new List<CellCoord>();

    [Tooltip("칠해진 Hill입니다. layer는 Top 층과 같습니다 (0=1층).")]
    public List<HillEntry> hills = new List<HillEntry>();

    [Tooltip("구버전 호환용. 자동으로 hills로 이전됩니다.")]
    public List<CellCoord> hillCells = new List<CellCoord>();

    [Tooltip("칠해진 바닥(Ground) 셀입니다.")]
    public List<CellCoord> groundCells = new List<CellCoord>();

    [Tooltip("수동으로 배치한 램프입니다.")]
    public List<RampEntry> ramps = new List<RampEntry>();

    [Header("Gizmo")]
    public bool drawGizmos = true;
    public Color topGizmoColor = new Color(0.2f, 0.8f, 0.3f, 0.35f);
    public Color hillGizmoColor = new Color(0.3f, 0.6f, 1f, 0.4f);
    public Color groundGizmoColor = new Color(0.55f, 0.4f, 0.25f, 0.3f);
    public Color rampGizmoColor = new Color(0.9f, 0.7f, 0.2f, 0.5f);

    readonly HashSet<CellCoord> elevatedSet = new HashSet<CellCoord>();
    readonly Dictionary<CellCoord, int> topMaxLayer = new Dictionary<CellCoord, int>();
    readonly HashSet<CellCoord> hillSet = new HashSet<CellCoord>();
    readonly Dictionary<CellCoord, int> hillLayerMap = new Dictionary<CellCoord, int>();
    readonly HashSet<CellCoord> groundSet = new HashSet<CellCoord>();
    readonly Dictionary<(CellCoord cell, int dir), int> rampLookup =
        new Dictionary<(CellCoord, int), int>();

    // SpawnHill* 처리 중 현재 층 (커넥터 오토타일 조회용)
    int hillBuildLayer;

    // 프리팹별 메시 바닥 오프셋 캐시 (재생성마다 Instantiate 임시 오브젝트 방지)
    readonly Dictionary<int, float> prefabBottomOffsetCache = new Dictionary<int, float>();

    bool lookupDirty = true;

    public float TileSize => tileSet != null ? Mathf.Max(0.01f, tileSet.tileSize) : 8f;
    public float CliffHeight => tileSet != null ? Mathf.Max(0f, tileSet.cliffHeight) : 0f;

    // 한 층을 올릴 때 쓰는 실제 높이 간격.
    // cliffHeight > 0 → 그대로 사용
    // cliffHeight == 0 → |topHeightOffset - edgeHeightOffset| 로 추정 (메시 오프셋만 쓰는 타일셋)
    public float LayerStepHeight
    {
        get
        {
            if (CliffHeight > 0.0001f)
                return CliffHeight;

            if (tileSet == null)
                return 0f;

            if (tileSet.layerStepHeight > 0.0001f)
                return tileSet.layerStepHeight;

            return Mathf.Abs(tileSet.topHeightOffset - tileSet.edgeHeightOffset);
        }
    }

    // 1층(layer 0) Top 상면 / 그 아래 절벽면 (하위 호환)
    public float TopSurfaceHeight => GetTopSurfaceHeight(0);
    public float EdgeSurfaceHeight => GetEdgeSurfaceHeight(0);

    // cliffHeight가 0인 타일셋도 윗층만 LayerStep만큼 추가로 올립니다.
    public float GetTopSurfaceHeight(int layer)
    {
        layer = Mathf.Max(0, layer);
        return baseHeight + CliffHeight * (layer + 1) + ExtraLayerLift(layer);
    }

    public float GetEdgeSurfaceHeight(int layer)
    {
        layer = Mathf.Max(0, layer);
        return baseHeight + CliffHeight * layer + ExtraLayerLift(layer);
    }

    float ExtraLayerLift(int layer)
    {
        if (layer <= 0)
            return 0f;

        // cliffHeight를 이미 쓰는 경우 이중 가산 방지
        if (CliffHeight > 0.0001f)
            return 0f;

        return LayerStepHeight * layer;
    }

    public int GetTopMaxLayer(CellCoord cell) =>
        topMaxLayer.TryGetValue(cell, out int layer) ? layer : -1;

    public bool HasTop(int x, int z) => elevatedSet.Contains(new CellCoord(x, z));
    public bool HasTop(CellCoord cell) => elevatedSet.Contains(cell);
    public bool HasTopLayer(CellCoord cell, int layer) =>
        topMaxLayer.TryGetValue(cell, out int max) && max >= layer;
    public bool HasHill(CellCoord cell) => hillSet.Contains(cell);
    public bool HasHillOnLayer(CellCoord cell, int layer) =>
        hillLayerMap.TryGetValue(cell, out int hillLayer) && hillLayer == layer;
    public int GetHillLayer(CellCoord cell) =>
        hillLayerMap.TryGetValue(cell, out int hillLayer) ? hillLayer : 0;
    public bool HasGround(CellCoord cell) => groundSet.Contains(cell);

    public int TopCount => tops != null ? tops.Count : 0;

    public void InvalidateLookup() => lookupDirty = true;

    public void EnsureLookup()
    {
        if (!lookupDirty)
            return;
        RebuildLookup();
    }

    void MigrateLegacyTopsIfNeeded()
    {
        if (tops == null)
            tops = new List<TopEntry>();

        if (topCells == null || topCells.Count == 0)
            return;

        if (tops.Count == 0)
        {
            for (int i = 0; i < topCells.Count; i++)
                tops.Add(new TopEntry { cell = topCells[i], maxLayer = 0 });
        }

        topCells.Clear();
    }

    void MigrateLegacyHillsIfNeeded()
    {
        if (hills == null)
            hills = new List<HillEntry>();

        if (hillCells == null || hillCells.Count == 0)
            return;

        if (hills.Count == 0)
        {
            for (int i = 0; i < hillCells.Count; i++)
                hills.Add(new HillEntry { cell = hillCells[i], layer = 0 });
        }

        hillCells.Clear();
    }

    public void RebuildLookup()
    {
        MigrateLegacyTopsIfNeeded();
        MigrateLegacyHillsIfNeeded();

        elevatedSet.Clear();
        topMaxLayer.Clear();
        hillSet.Clear();
        hillLayerMap.Clear();
        groundSet.Clear();

        if (tops != null)
        {
            for (int i = 0; i < tops.Count; i++)
            {
                TopEntry entry = tops[i];
                int layer = Mathf.Max(0, entry.maxLayer);
                elevatedSet.Add(entry.cell);
                if (!topMaxLayer.TryGetValue(entry.cell, out int existing) || layer > existing)
                    topMaxLayer[entry.cell] = layer;
            }
        }

        if (hills != null)
        {
            for (int i = 0; i < hills.Count; i++)
            {
                HillEntry entry = hills[i];
                hillSet.Add(entry.cell);
                hillLayerMap[entry.cell] = Mathf.Max(0, entry.layer);
            }
        }

        if (groundCells != null)
        {
            for (int i = 0; i < groundCells.Count; i++)
                groundSet.Add(groundCells[i]);
        }

        rampLookup.Clear();

        if (ramps != null)
        {
            for (int i = 0; i < ramps.Count; i++)
            {
                RampEntry ramp = ramps[i];
                int dir = NormalizeDir(ramp.direction);
                rampLookup[(ramp.cell, dir)] = i;
            }
        }

        lookupDirty = false;
    }

    int FindTopIndex(CellCoord cell)
    {
        if (tops == null)
            return -1;

        for (int i = 0; i < tops.Count; i++)
        {
            if (tops[i].cell.Equals(cell))
                return i;
        }

        return -1;
    }

    void SetTopMaxLayer(CellCoord cell, int maxLayer)
    {
        maxLayer = Mathf.Max(0, maxLayer);
        int index = FindTopIndex(cell);
        if (index >= 0)
        {
            TopEntry entry = tops[index];
            entry.maxLayer = maxLayer;
            tops[index] = entry;
        }
        else
        {
            tops.Add(new TopEntry { cell = cell, maxLayer = maxLayer });
        }

        elevatedSet.Add(cell);
        topMaxLayer[cell] = maxLayer;
    }

    void RemoveTopEntry(CellCoord cell)
    {
        int index = FindTopIndex(cell);
        if (index >= 0)
            tops.RemoveAt(index);

        elevatedSet.Remove(cell);
        topMaxLayer.Remove(cell);
    }

    // layer 0: Ground 위 1층. layer>=1: 아랫층 가장자리에서 한 칸 안쪽만.
    // 아랫층 Inner 코너를 구성하는 Top 위에는 윗층을 올리지 않습니다.
    public bool CanPlaceTop(CellCoord cell, int layer)
    {
        layer = Mathf.Max(0, layer);

        if (HasTopLayer(cell, layer))
            return false;

        if (layer == 0)
            return true;

        // 아랫층이 있어야 하고, 직교 4칸 모두 아랫층 → 가장자리 링은 비움
        int below = layer - 1;
        if (!HasTopLayer(cell, below))
            return false;

        for (int dir = 0; dir < 4; dir++)
        {
            if (!HasTopLayer(Neighbor(cell, dir), below))
                return false;
        }

        // 1층 In 코너(및 그 위층 In 코너)를 받치는 Top 칸 위에는 배치 불가
        if (IsSupportTopOfAnyInnerCorner(cell, below))
            return false;

        return true;
    }

    public bool TryAddTop(CellCoord cell) => TryAddTop(cell, 0);

    public bool TryAddTop(CellCoord cell, int layer)
    {
        layer = Mathf.Max(0, layer);

        if (!CanPlaceTop(cell, layer))
            return false;

        if (layer == 0)
        {
            SetTopMaxLayer(cell, 0);
            TryRemoveGround(cell);
            TryRemoveHill(cell);
            return true;
        }

        // 윗층: 기존 max를 올리기만 함 (아래 층은 유지)
        int current = GetTopMaxLayer(cell);
        SetTopMaxLayer(cell, Mathf.Max(current, layer));
        return true;
    }

    public bool TryRemoveTop(CellCoord cell) => TryRemoveTop(cell, 0);

    // 지정 층 이상 제거. layer 0이면 칸의 Top 전부와 위 지형(램프/언덕)을 지운 뒤 Ground를 채웁니다.
    public bool TryRemoveTop(CellCoord cell, int layer)
    {
        layer = Mathf.Max(0, layer);
        int current = GetTopMaxLayer(cell);
        if (current < layer)
        {
            // Top은 없어도 같은 칸의 Hill/Ramp는 지운다 (지우개 = 위 지형 정리).
            bool clearedExtras = ClearAboveTerrainOnCell(cell, layer);
            return clearedExtras;
        }

        if (layer == 0)
        {
            RemoveTopEntry(cell);
            ClearAboveTerrainOnCell(cell, 0);
            RemoveInvalidHills();
            RemoveInvalidUpperLayers();
            // Top이 사라진 빈 칸은 Ground로 채운다.
            TryAddGround(cell);
            return true;
        }

        // layer 이상만 깎음 → max = layer-1
        SetTopMaxLayer(cell, layer - 1);
        ClearAboveTerrainOnCell(cell, layer);
        RemoveInvalidHills();
        RemoveInvalidUpperLayers();
        return true;
    }

    // 해당 칸에서 minLayer 이상 Hill / (1층이면) Ramp를 제거합니다.
    bool ClearAboveTerrainOnCell(CellCoord cell, int minLayer)
    {
        bool changed = false;
        minLayer = Mathf.Max(0, minLayer);

        if (minLayer == 0)
        {
            int rampCount = ramps != null ? ramps.Count : 0;
            RemoveRampsOnCell(cell);
            if (ramps != null && ramps.Count != rampCount)
                changed = true;
        }

        if (RemoveHillDataIfLayerAtLeast(cell, minLayer))
            changed = true;

        return changed;
    }

    bool RemoveHillDataIfLayerAtLeast(CellCoord cell, int minLayer)
    {
        if (!hillSet.Contains(cell))
            return false;

        int hillLayer = GetHillLayer(cell);
        if (hillLayer < minLayer)
            return false;

        hillSet.Remove(cell);
        hillLayerMap.Remove(cell);
        int index = FindHillIndex(cell);
        if (index >= 0)
            hills.RemoveAt(index);

        return true;
    }

    /// <summary>
    /// Ground 지우개: 해당 칸의 Top / Hill / Ramp / Ground를 모두 제거합니다.
    /// </summary>
    public bool TryEraseGroundStack(CellCoord cell)
    {
        bool changed = false;

        if (HasTop(cell))
        {
            RemoveTopEntry(cell);
            changed = true;
        }

        if (RemoveHillDataIfLayerAtLeast(cell, 0))
            changed = true;

        int rampCount = ramps != null ? ramps.Count : 0;
        RemoveRampsOnCell(cell);
        if (ramps != null && ramps.Count != rampCount)
            changed = true;

        if (TryRemoveGround(cell))
            changed = true;

        RemoveInvalidHills();
        RemoveInvalidUpperLayers();
        return changed;
    }

    // 아랫층이 사라졌거나 가장자리 조건을 깨면 윗층 정리
    void RemoveInvalidUpperLayers()
    {
        bool changed = true;
        while (changed)
        {
            changed = false;
            for (int i = tops.Count - 1; i >= 0; i--)
            {
                TopEntry entry = tops[i];
                int max = entry.maxLayer;
                while (max >= 1 && !CanKeepTopLayer(entry.cell, max))
                {
                    max--;
                    changed = true;
                }

                if (max < 0)
                {
                    CellCoord cleared = entry.cell;
                    RemoveTopEntry(cleared);
                    TryAddGround(cleared);
                    changed = true;
                    continue;
                }

                if (max != entry.maxLayer)
                {
                    SetTopMaxLayer(entry.cell, max);
                    changed = true;
                }
            }
        }
    }

    bool CanKeepTopLayer(CellCoord cell, int layer)
    {
        if (layer <= 0)
            return HasTopLayer(cell, 0);

        int below = layer - 1;
        if (!HasTopLayer(cell, below))
            return false;

        for (int dir = 0; dir < 4; dir++)
        {
            if (!HasTopLayer(Neighbor(cell, dir), below))
                return false;
        }

        if (IsSupportTopOfAnyInnerCorner(cell, below))
            return false;

        return true;
    }

    // cell이 지정 층 Inner 코너를 구성하는 세 Top(직교 2 + 대각 1) 중 하나인지.
    bool IsSupportTopOfAnyInnerCorner(CellCoord cell, int layer)
    {
        if (!HasTopLayer(cell, layer))
            return false;

        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dz = -1; dz <= 1; dz++)
            {
                if (dx == 0 && dz == 0)
                    continue;

                CellCoord empty = new CellCoord(cell.x + dx, cell.z + dz);
                if (!IsInnerCornerCell(empty, layer))
                    continue;

                if (InnerCornerUsesTop(empty, cell, layer))
                    return true;
            }
        }

        return false;
    }

    bool InnerCornerUsesTop(CellCoord empty, CellCoord top, int layer)
    {
        CellCoord n = Neighbor(empty, 0);
        CellCoord e = Neighbor(empty, 1);
        CellCoord s = Neighbor(empty, 2);
        CellCoord w = Neighbor(empty, 3);
        CellCoord ne = new CellCoord(empty.x + 1, empty.z + 1);
        CellCoord se = new CellCoord(empty.x + 1, empty.z - 1);
        CellCoord sw = new CellCoord(empty.x - 1, empty.z - 1);
        CellCoord nw = new CellCoord(empty.x - 1, empty.z + 1);

        bool hasN = HasTopLayer(n, layer);
        bool hasE = HasTopLayer(e, layer);
        bool hasS = HasTopLayer(s, layer);
        bool hasW = HasTopLayer(w, layer);

        if (hasN && hasE && HasTopLayer(ne, layer))
            return top.Equals(n) || top.Equals(e) || top.Equals(ne);

        if (hasE && hasS && HasTopLayer(se, layer))
            return top.Equals(e) || top.Equals(s) || top.Equals(se);

        if (hasS && hasW && HasTopLayer(sw, layer))
            return top.Equals(s) || top.Equals(w) || top.Equals(sw);

        if (hasW && hasN && HasTopLayer(nw, layer))
            return top.Equals(w) || top.Equals(n) || top.Equals(nw);

        return false;
    }

    void RemoveRampsOnCell(CellCoord cell)
    {
        if (ramps == null)
            return;

        bool removed = false;
        for (int i = ramps.Count - 1; i >= 0; i--)
        {
            if (ramps[i].cell.Equals(cell))
            {
                ramps.RemoveAt(i);
                removed = true;
            }
        }

        if (removed)
            RebuildRampLookupOnly();
    }

    void RebuildRampLookupOnly()
    {
        rampLookup.Clear();
        if (ramps == null)
            return;

        for (int i = 0; i < ramps.Count; i++)
        {
            RampEntry ramp = ramps[i];
            rampLookup[(ramp.cell, NormalizeDir(ramp.direction))] = i;
        }
    }

    public void ClearAllTops()
    {
        if (tops != null)
            tops.Clear();
        if (topCells != null)
            topCells.Clear();
        if (hills != null)
            hills.Clear();
        if (hillCells != null)
            hillCells.Clear();
        groundCells.Clear();
        ramps.Clear();
        elevatedSet.Clear();
        topMaxLayer.Clear();
        hillSet.Clear();
        hillLayerMap.Clear();
        groundSet.Clear();
        rampLookup.Clear();
        lookupDirty = false;
    }

    // 바닥(Ground)은 남기고 Top / Hill / 램프만 지웁니다.
    public void ClearTopsAndHills()
    {
        if (tops != null)
            tops.Clear();
        if (topCells != null)
            topCells.Clear();
        if (hills != null)
            hills.Clear();
        if (hillCells != null)
            hillCells.Clear();
        ramps.Clear();
        elevatedSet.Clear();
        topMaxLayer.Clear();
        hillSet.Clear();
        hillLayerMap.Clear();
        rampLookup.Clear();
        lookupDirty = false;
    }

    public bool TryAddGround(CellCoord cell)
    {
        if (groundSet.Contains(cell))
            return false;

        // Top / Hill 자리에는 Ground 데이터를 올리지 않는다.
        if (HasTopLayer(cell, 0) || HasHill(cell))
            return false;

        groundCells.Add(cell);
        groundSet.Add(cell);
        return true;
    }

    public bool TryRemoveGround(CellCoord cell)
    {
        if (!groundSet.Remove(cell))
            return false;

        groundCells.Remove(cell);
        return true;
    }

    // origin 기준 size×size 바닥을 채우거나 지웁니다. 변경된 칸 수를 반환합니다.
    public int FillGround(CellCoord origin, int size, bool erase)
    {
        size = Mathf.Max(1, size);
        int changed = 0;

        for (int dx = 0; dx < size; dx++)
        {
            for (int dz = 0; dz < size; dz++)
            {
                CellCoord cell = new CellCoord(origin.x + dx, origin.z + dz);
                if (erase)
                {
                    if (TryEraseGroundStack(cell))
                        changed++;
                }
                else if (TryAddGround(cell))
                {
                    changed++;
                }
            }
        }

        return changed;
    }

    // Top 층 가장자리 슬롯에 Hill을 칠합니다.
    // 1층(layer 0): Ground 필요. 2층+: 해당 층 Top 가장자리(아랫층 위).
    public bool TryAddHill(CellCoord cell) => TryAddHill(cell, 0);

    public bool TryAddHill(CellCoord cell, int layer)
    {
        layer = Mathf.Max(0, layer);

        if (!CanPlaceHill(cell, layer))
            return false;

        int index = FindHillIndex(cell);
        if (index >= 0)
        {
            HillEntry entry = hills[index];
            entry.layer = layer;
            hills[index] = entry;
        }
        else
        {
            hills.Add(new HillEntry { cell = cell, layer = layer });
        }

        hillSet.Add(cell);
        hillLayerMap[cell] = layer;

        if (layer == 0)
            TryRemoveGround(cell);

        return true;
    }

    public bool TryRemoveHill(CellCoord cell)
    {
        if (!hillSet.Contains(cell))
            return false;

        int layer = GetHillLayer(cell);

        if (!hillSet.Remove(cell))
            return false;

        hillLayerMap.Remove(cell);
        int index = FindHillIndex(cell);
        if (index >= 0)
            hills.RemoveAt(index);

        // 1층 Hill 배치 때 제거한 Ground를 복구해, 같은 자리에 벽/코너가 다시 생기게 한다.
        if (layer == 0 && !HasTopLayer(cell, 0) && IsEdgeSlot(cell, 0))
            TryAddGround(cell);

        return true;
    }

    int FindHillIndex(CellCoord cell)
    {
        if (hills == null)
            return -1;

        for (int i = 0; i < hills.Count; i++)
        {
            if (hills[i].cell.Equals(cell))
                return i;
        }

        return -1;
    }

    public bool CanPlaceHill(CellCoord cell) => CanPlaceHill(cell, 0);

    public bool CanPlaceHill(CellCoord cell, int layer)
    {
        layer = Mathf.Max(0, layer);

        if (HasHill(cell) || !IsEdgeSlot(cell, layer))
            return false;

        if (layer == 0)
            return HasGround(cell);

        // 윗층 Hill: 해당 층 Top 가장자리 (보통 아랫층 Top 위 링)
        return true;
    }

    public bool IsEdgeSlot(CellCoord cell) => IsEdgeSlot(cell, 0);

    // 지정 Top 층 기준 가장자리 슬롯 (그 층 Top이 없는 칸)
    public bool IsEdgeSlot(CellCoord cell, int layer)
    {
        layer = Mathf.Max(0, layer);

        if (HasTopLayer(cell, layer))
            return false;

        for (int dir = 0; dir < 4; dir++)
        {
            if (HasTopLayer(Neighbor(cell, dir), layer))
                return true;
        }

        return IsOuterCornerSlot(cell, layer) || IsInnerCornerCell(cell, layer);
    }

    // Hill에 직교로 붙어 자동 연결될 칸인지 (벽/코너 자리, Hill 아님).
    public bool IsAutoConnectorSlot(CellCoord cell)
    {
        int layer = HasHill(cell) ? GetHillLayer(cell) : 0;
        return IsAutoConnectorSlot(cell, layer);
    }

    public bool IsAutoConnectorSlot(CellCoord cell, int layer)
    {
        if (HasTopLayer(cell, layer) || HasHill(cell) || !IsEdgeSlot(cell, layer))
            return false;

        return TryGetDirTowardHill(cell, layer, out _);
    }

    public void CollectAutoConnectorNeighbors(CellCoord hillCell, List<CellCoord> into)
    {
        int layer = GetHillLayer(hillCell);
        CollectAutoConnectorNeighbors(hillCell, layer, into);
    }

    public void CollectAutoConnectorNeighbors(CellCoord hillCell, int layer, List<CellCoord> into)
    {
        if (into == null)
            return;

        for (int dir = 0; dir < 4; dir++)
        {
            CellCoord n = Neighbor(hillCell, dir);
            if (IsEdgeSlot(n, layer) && !HasHill(n) && !HasTopLayer(n, layer))
                into.Add(n);
        }
    }

    void RemoveInvalidHills()
    {
        if (hills == null || hills.Count == 0)
            return;

        for (int i = hills.Count - 1; i >= 0; i--)
        {
            HillEntry entry = hills[i];
            if (IsEdgeSlot(entry.cell, entry.layer))
                continue;

            hillSet.Remove(entry.cell);
            hillLayerMap.Remove(entry.cell);
            hills.RemoveAt(i);
        }
    }

    public static int NormalizeDir(int dir) => ((dir % 4) + 4) % 4;
    public static int OppositeDir(int dir) => (NormalizeDir(dir) + 2) % 4;

    public bool TrySetRamp(CellCoord cell, int direction)
    {
        direction = NormalizeDir(direction);

        if (!elevatedSet.Contains(cell))
            return false;

        CellCoord neighbor = Neighbor(cell, direction);

        if (elevatedSet.Contains(neighbor))
            return false;

        if (rampLookup.TryGetValue((cell, direction), out int index))
        {
            RampEntry existing = ramps[index];
            existing.direction = direction;
            ramps[index] = existing;
            return true;
        }

        ramps.Add(new RampEntry { cell = cell, direction = direction });
        rampLookup[(cell, direction)] = ramps.Count - 1;
        return true;
    }

    public bool TryRemoveRamp(CellCoord cell, int direction)
    {
        direction = NormalizeDir(direction);

        if (!rampLookup.TryGetValue((cell, direction), out int index))
            return false;

        ramps.RemoveAt(index);
        RebuildRampLookupOnly();
        return true;
    }

    public bool HasRamp(CellCoord cell, int direction)
    {
        direction = NormalizeDir(direction);
        return rampLookup.ContainsKey((cell, direction));
    }

    public static CellCoord Neighbor(CellCoord cell, int direction)
    {
        switch (NormalizeDir(direction))
        {
            case 0: return new CellCoord(cell.x, cell.z + 1); // N
            case 1: return new CellCoord(cell.x + 1, cell.z); // E
            case 2: return new CellCoord(cell.x, cell.z - 1); // S
            default: return new CellCoord(cell.x - 1, cell.z); // W
        }
    }

    public Vector3 CellCornerWorld(CellCoord cell)
    {
        float size = TileSize;
        return new Vector3(
            gridOrigin.x + cell.x * size,
            GetTopSurfaceHeight(0),
            gridOrigin.z + cell.z * size);
    }

    public Vector3 CellCenterWorld(CellCoord cell)
    {
        float size = TileSize;
        return new Vector3(
            gridOrigin.x + (cell.x + 0.5f) * size,
            GetTopSurfaceHeight(0),
            gridOrigin.z + (cell.z + 0.5f) * size);
    }

    public Vector3 CellCenterWorldAtEdge(CellCoord cell, int layer)
    {
        Vector3 p = CellCenterWorld(cell);
        p.y = GetEdgeSurfaceHeight(layer);
        return p;
    }

    public Vector3 CellCenterWorldAtTop(CellCoord cell, int layer)
    {
        Vector3 p = CellCenterWorld(cell);
        p.y = GetTopSurfaceHeight(layer);
        return p;
    }

    public Vector3 EdgeVertexWorld(int vx, int vz)
    {
        return EdgeVertexWorld(vx, vz, 0);
    }

    public Vector3 EdgeVertexWorld(int vx, int vz, int layer)
    {
        float size = TileSize;
        return new Vector3(
            gridOrigin.x + vx * size,
            GetEdgeSurfaceHeight(layer),
            gridOrigin.z + vz * size);
    }

    public CellCoord WorldToCell(Vector3 world)
    {
        float size = TileSize;
        int x = Mathf.FloorToInt((world.x - gridOrigin.x) / size);
        int z = Mathf.FloorToInt((world.z - gridOrigin.z) / size);
        return new CellCoord(x, z);
    }

    public Transform EnsureGeneratedRoot()
    {
        if (generatedRoot != null)
            return generatedRoot;

        Transform existing = transform.Find("Generated");

        if (existing != null)
        {
            generatedRoot = existing;
            return generatedRoot;
        }

        GameObject go = new GameObject("Generated");
        go.transform.SetParent(transform, false);
        generatedRoot = go.transform;
        return generatedRoot;
    }

    // Top 연결에 맞춰 가장자리 모듈을 전부 다시 생성합니다.
    public void RebuildGeometry()
    {
        RebuildLookup();
        // 배치 규칙이 바뀌었거나 잘못된 윗층이 있으면 정리한다.
        RemoveInvalidUpperLayers();
        RebuildLookup();

        if (tileSet == null)
        {
            Debug.LogWarning("[CliffPainter] CliffTileSet이 없습니다.", this);
            return;
        }

        Transform root = EnsureGeneratedRoot();
        MarkGeneratedStatic(root.gameObject);

        for (int i = root.childCount - 1; i >= 0; i--)
            DestroyImmediate(root.GetChild(i).gameObject);

        SpawnGrounds(root);

        int highest = 0;
        foreach (KeyValuePair<CellCoord, int> kv in topMaxLayer)
        {
            if (kv.Value > highest)
                highest = kv.Value;
        }

        for (int layer = 0; layer <= highest; layer++)
            SpawnLayerGeometry(layer, root);

        SpawnRamps(root);
    }

    void SpawnLayerGeometry(int layer, Transform root)
    {
        HashSet<CellCoord> spawnedSlots = new HashSet<CellCoord>();
        List<CellCoord> layerCells = new List<CellCoord>();

        foreach (KeyValuePair<CellCoord, int> kv in topMaxLayer)
        {
            if (kv.Value >= layer)
                layerCells.Add(kv.Key);
        }

        for (int i = 0; i < layerCells.Count; i++)
        {
            CellCoord cell = layerCells[i];
            Vector3 pos = CellCenterWorldAtTop(cell, layer);
            Spawn(
                tileSet.top,
                pos,
                0f,
                tileSet.topHeightOffset,
                $"Top_L{layer}_{cell.x}_{cell.z}",
                root);
        }

        for (int i = 0; i < layerCells.Count; i++)
            PlaceEdgesForCell(layerCells[i], layer, root, spawnedSlots);

        HashSet<CellCoord> visitedInners = new HashSet<CellCoord>();
        for (int i = 0; i < layerCells.Count; i++)
        {
            CellCoord cell = layerCells[i];
            TryPlaceInnerAtVertex(cell.x, cell.z, layer, visitedInners, root, spawnedSlots);
            TryPlaceInnerAtVertex(cell.x + 1, cell.z, layer, visitedInners, root, spawnedSlots);
            TryPlaceInnerAtVertex(cell.x, cell.z + 1, layer, visitedInners, root, spawnedSlots);
            TryPlaceInnerAtVertex(cell.x + 1, cell.z + 1, layer, visitedInners, root, spawnedSlots);
        }
    }

    void SpawnGrounds(Transform root)
    {
        if (tileSet.ground == null || groundCells == null || groundCells.Count == 0)
            return;

        for (int i = 0; i < groundCells.Count; i++)
        {
            CellCoord cell = groundCells[i];

            // 월/코너 슬롯은 Top과 같이 Ground 메시를 올리지 않는다.
            // (Ground 데이터는 남겨 1층 벽 마스크·Hill 배치에 사용)
            if (IsEdgeSlot(cell, 0))
                continue;

            Vector3 pos = CellCenterWorld(cell);
            pos.y = GetEdgeSurfaceHeight(0);
            Spawn(
                tileSet.ground,
                pos,
                tileSet.groundYawOffset,
                tileSet.groundHeightOffset,
                $"Ground_{cell.x}_{cell.z}",
                root);
        }
    }

    // Top 한 칸 기준 가장자리. layer>0 은 아랫층 상면 높이에 벽이 섭니다.
    void PlaceEdgesForCell(
        CellCoord cell,
        int layer,
        Transform root,
        HashSet<CellCoord> spawnedSlots)
    {
        bool n = HasTopLayer(Neighbor(cell, 0), layer);
        bool e = HasTopLayer(Neighbor(cell, 1), layer);
        bool s = HasTopLayer(Neighbor(cell, 2), layer);
        bool w = HasTopLayer(Neighbor(cell, 3), layer);

        float size = TileSize;
        Vector3 center = CellCenterWorldAtEdge(cell, layer);

        CellCoord nCell = Neighbor(cell, 0);
        CellCoord eCell = Neighbor(cell, 1);
        CellCoord sCell = Neighbor(cell, 2);
        CellCoord wCell = Neighbor(cell, 3);

        if (!n && !(layer == 0 && HasRamp(cell, 0)) && !IsInnerCornerCell(nCell, layer))
            SpawnEdgeSlot(
                nCell, EdgeKind.Straight, layer,
                center + new Vector3(0f, 0f, size), 0f,
                $"Straight_L{layer}_N_{cell.x}_{cell.z}", root, spawnedSlots);

        if (!e && !(layer == 0 && HasRamp(cell, 1)) && !IsInnerCornerCell(eCell, layer))
            SpawnEdgeSlot(
                eCell, EdgeKind.Straight, layer,
                center + new Vector3(size, 0f, 0f), 90f,
                $"Straight_L{layer}_E_{cell.x}_{cell.z}", root, spawnedSlots);

        if (!s && !(layer == 0 && HasRamp(cell, 2)) && !IsInnerCornerCell(sCell, layer))
            SpawnEdgeSlot(
                sCell, EdgeKind.Straight, layer,
                center + new Vector3(0f, 0f, -size), 180f,
                $"Straight_L{layer}_S_{cell.x}_{cell.z}", root, spawnedSlots);

        if (!w && !(layer == 0 && HasRamp(cell, 3)) && !IsInnerCornerCell(wCell, layer))
            SpawnEdgeSlot(
                wCell, EdgeKind.Straight, layer,
                center + new Vector3(-size, 0f, 0f), 270f,
                $"Straight_L{layer}_W_{cell.x}_{cell.z}", root, spawnedSlots);

        CellCoord ne = new CellCoord(cell.x + 1, cell.z + 1);
        CellCoord se = new CellCoord(cell.x + 1, cell.z - 1);
        CellCoord sw = new CellCoord(cell.x - 1, cell.z - 1);
        CellCoord nw = new CellCoord(cell.x - 1, cell.z + 1);

        if (!n && !e)
            SpawnEdgeSlot(
                ne, EdgeKind.OuterCorner, layer,
                center + new Vector3(size, 0f, size), 0f,
                $"Outer_L{layer}_NE_{cell.x}_{cell.z}", root, spawnedSlots);

        if (!e && !s)
            SpawnEdgeSlot(
                se, EdgeKind.OuterCorner, layer,
                center + new Vector3(size, 0f, -size), 90f,
                $"Outer_L{layer}_SE_{cell.x}_{cell.z}", root, spawnedSlots);

        if (!s && !w)
            SpawnEdgeSlot(
                sw, EdgeKind.OuterCorner, layer,
                center + new Vector3(-size, 0f, -size), 180f,
                $"Outer_L{layer}_SW_{cell.x}_{cell.z}", root, spawnedSlots);

        if (!w && !n)
            SpawnEdgeSlot(
                nw, EdgeKind.OuterCorner, layer,
                center + new Vector3(-size, 0f, size), 270f,
                $"Outer_L{layer}_NW_{cell.x}_{cell.z}", root, spawnedSlots);
    }

    // 가장자리 슬롯: Hill / 자동 연결 / 일반 벽·코너
    void SpawnEdgeSlot(
        CellCoord slot,
        EdgeKind kind,
        int layer,
        Vector3 pos,
        float yaw,
        string name,
        Transform root,
        HashSet<CellCoord> spawnedSlots)
    {
        if (spawnedSlots != null && !spawnedSlots.Add(slot))
            return;

        pos.y = GetEdgeSurfaceHeight(layer);
        hillBuildLayer = layer;

        if (HasHillOnLayer(slot, layer))
        {
            SpawnHillModule(slot, kind, yaw, root, layer);
            return;
        }

        // 1층만 Ground 마스크 적용 (맵 가장자리 등 Ground 없는 칸에는 벽 미생성)
        if (layer == 0 && !HasGround(slot))
            return;

        if (CountOrthoHillNeighbors(slot, layer) >= 2)
        {
            SpawnHillModule(slot, kind, yaw, root, layer);
            return;
        }

        if (TryGetDirTowardHill(slot, layer, out int dirToHill))
        {
            SpawnHillConnector(slot, kind, pos, yaw, dirToHill, root, layer);
            return;
        }

        SpawnEdge(kind, pos, yaw, name, root);
    }

    int CountOrthoHillNeighbors(CellCoord cell, int layer)
    {
        int count = 0;
        for (int dir = 0; dir < 4; dir++)
        {
            if (HasHillOnLayer(Neighbor(cell, dir), layer))
                count++;
        }

        return count;
    }

    // 칠한 Hill 칸: 직선 → Hill, Outer → HXH_Out, Inner → HXH_In
    void SpawnHillModule(CellCoord slot, EdgeKind kind, float edgeYaw, Transform root, int layer)
    {
        if (hillTileSet == null)
            return;

        hillBuildLayer = layer;

        GameObject prefab;
        float yaw;
        float yOffset;

        switch (kind)
        {
            case EdgeKind.OuterCorner:
                prefab = hillTileSet.toOuterCorner != null
                    ? hillTileSet.toOuterCorner
                    : hillTileSet.hill;
                yaw = edgeYaw + (hillTileSet.toOuterCorner != null
                    ? hillTileSet.toOuterCornerYawOffset
                    : hillTileSet.hillYawOffset);
                yOffset = hillTileSet.toOuterCorner != null
                    ? hillTileSet.connectorHeightOffset
                    : hillTileSet.hillHeightOffset;
                break;

            case EdgeKind.InnerCorner:
                prefab = hillTileSet.toInnerCorner != null
                    ? hillTileSet.toInnerCorner
                    : hillTileSet.hill;
                yaw = edgeYaw + (hillTileSet.toInnerCorner != null
                    ? hillTileSet.toInnerCornerYawOffset
                    : hillTileSet.hillYawOffset);
                yOffset = hillTileSet.toInnerCorner != null
                    ? hillTileSet.connectorHeightOffset
                    : hillTileSet.hillHeightOffset;
                break;

            default:
                prefab = hillTileSet.hill;
                yaw = edgeYaw + hillTileSet.hillYawOffset;
                yOffset = hillTileSet.hillHeightOffset;
                break;
        }

        if (prefab == null)
            return;

        Vector3 pos = CellCenterWorld(slot);
        pos.y = GetEdgeSurfaceHeight(layer);
        Spawn(
            prefab,
            pos,
            yaw,
            yOffset,
            $"Hill_L{layer}_{kind}_{slot.x}_{slot.z}",
            root,
            matchStraightMeshBottom: true);
    }

    void SpawnHillConnector(
        CellCoord slot,
        EdgeKind kind,
        Vector3 pos,
        float edgeYaw,
        int dirToHill,
        Transform root,
        int layer)
    {
        if (hillTileSet == null)
            return;

        hillBuildLayer = layer;

        GameObject prefab;
        float yaw;
        float yOffset = hillTileSet.connectorHeightOffset;
        bool flipScaleX = false;

        switch (kind)
        {
            case EdgeKind.OuterCorner:
            case EdgeKind.InnerCorner:
                ResolveCornerConnector(
                    slot,
                    kind,
                    dirToHill,
                    edgeYaw,
                    out prefab,
                    out yaw,
                    out flipScaleX);
                break;

            default:
                ResolveStraightConnector(dirToHill, edgeYaw, out prefab, out yaw, out flipScaleX);
                break;
        }

        pos.y = GetEdgeSurfaceHeight(layer);
        Spawn(
            prefab,
            pos,
            yaw,
            yOffset,
            $"HillConn_L{layer}_{slot.x}_{slot.z}_{kind}",
            root,
            matchStraightMeshBottom: true,
            flipScaleX: flipScaleX);
    }

    // 코너 슬롯 연결:
    // - 직선 Hill만 이어지고, walk 시 W 또는 WxW In/Out이 먼저면 → HXW
    // - walk 중 Hill이 먼저거나, Hill 러닝이면 → HXH
    // - Hill 자체가 코너면 → HXH
    void ResolveCornerConnector(
        CellCoord slot,
        EdgeKind cornerKind,
        int dirToHill,
        float edgeYaw,
        out GameObject prefab,
        out float yaw,
        out bool flipScaleX)
    {
        flipScaleX = false;
        dirToHill = NormalizeDir(dirToHill);
        CellCoord hillCell = Neighbor(slot, dirToHill);
        EdgeKind hillKind = ClassifyEdgeKind(hillCell);
        bool hillOnStraightWall = hillKind == EdgeKind.Straight;

        // 옆이 W / WxW In·Out이면 HXW 우선 (Hill 위치·다른 방향 Hill보다 앞)
        bool useHxh;
        if (HasImmediateWallOrWxwNeighbor(slot, dirToHill))
            useHxh = false;
        else if (!hillOnStraightWall)
            useHxh = true;
        else
            useHxh = ShouldUseHxhInsteadOfHxw(slot, hillCell, dirToHill);

        if (!useHxh)
        {
            // 코너 ↔ W / WxW → HXW
            bool outer = cornerKind == EdgeKind.OuterCorner;
            if (outer)
            {
                prefab = hillTileSet.hxwOuter != null
                    ? hillTileSet.hxwOuter
                    : (hillTileSet.wxh != null ? hillTileSet.wxh : hillTileSet.toOuterCorner);
            }
            else
            {
                prefab = hillTileSet.hxwInner != null
                    ? hillTileSet.hxwInner
                    : (hillTileSet.wxh != null ? hillTileSet.wxh : hillTileSet.toInnerCorner);
            }

            // 코너↔코너: Hill 벽 바깥을 로컬 N으로 둔 상대 8방위 (설치 방향과 무관)
            // 코너↔직선벽(W): 기존 좌/우 방향표
            if (HasCornerNeighbor8(slot, dirToHill))
            {
                if (!TryGetStraightOutwardYaw(hillCell, out float hillWallYaw))
                    hillWallYaw = edgeYaw;

                if (TryGetLocalDir8FromHill(slot, hillCell, hillWallYaw, out HillTileSet.Dir8 localDir))
                {
                    hillTileSet.TryGetHxwCornerToCornerPose(outer, localDir, out float yawOffset, out flipScaleX);
                    yaw = edgeYaw + yawOffset;
                    return;
                }
            }

            if (!TryGetStraightOutwardYaw(hillCell, out float wallYaw))
                wallYaw = edgeYaw;

            int outward = NormalizeDir(Mathf.RoundToInt(wallYaw / 90f));
            bool hillOnRight = ResolveHillOnRightForHxw(slot, dirToHill, outward);

            if (hillTileSet.invertHxwLeftRight)
                hillOnRight = !hillOnRight;

            hillTileSet.TryGetHxwPose(outer, outward, hillOnRight, out float tableYaw, out flipScaleX);
            yaw = edgeYaw + (tableYaw - wallYaw);
            return;
        }

        // HXH In/Out
        ApplyHxhCornerPrefab(cornerKind, edgeYaw, out prefab, out yaw);
    }

    // hillBuildLayer 기준 조회 (Hill 커넥터 오토타일용)
    bool BuildHasTop(CellCoord cell) => HasTopLayer(cell, hillBuildLayer);
    bool BuildHasHill(CellCoord cell) => HasHillOnLayer(cell, hillBuildLayer);
    bool BuildIsEdgeSlot(CellCoord cell) => IsEdgeSlot(cell, hillBuildLayer);

    // 3x3 8방위 오프셋: N,NE,E,SE,S,SW,W,NW (로컬/월드 공통)
    static readonly int[] Dir8Dx = { 0, 1, 1, 1, 0, -1, -1, -1 };
    static readonly int[] Dir8Dz = { 1, 1, 0, -1, -1, -1, 0, 1 };

    // Hill 쪽을 제외한 8방위에 WxW 코너가 있으면 true
    bool HasCornerNeighbor8(CellCoord cornerSlot, int dirToHill)
    {
        dirToHill = NormalizeDir(dirToHill);
        int hillDx = Dir4Dx(dirToHill);
        int hillDz = Dir4Dz(dirToHill);

        for (int i = 0; i < 8; i++)
        {
            int dx = Dir8Dx[i];
            int dz = Dir8Dz[i];
            if (dx == hillDx && dz == hillDz)
                continue;

            CellCoord n = new CellCoord(cornerSlot.x + dx, cornerSlot.z + dz);
            if (BuildHasHill(n) || BuildHasTop(n) || !BuildIsEdgeSlot(n))
                continue;

            EdgeKind kind = ClassifyEdgeKind(n);
            if (kind == EdgeKind.OuterCorner || kind == EdgeKind.InnerCorner)
                return true;
        }

        return false;
    }

    // Hill 벽 바깥(outward)을 로컬 N(+Z)으로 둔 뒤, 커넥터 상대 방위.
    // 예: 어느 쪽 벽이든 Hill 오른쪽 커넥터 → 항상 로컬 E
    bool TryGetLocalDir8FromHill(
        CellCoord slot,
        CellCoord hillCell,
        float hillWallYaw,
        out HillTileSet.Dir8 dir8)
    {
        int dx = slot.x - hillCell.x;
        int dz = slot.z - hillCell.z;

        if (dx < -1 || dx > 1 || dz < -1 || dz > 1 || (dx == 0 && dz == 0))
        {
            dir8 = HillTileSet.Dir8.N;
            return false;
        }

        int outward = NormalizeDir(Mathf.RoundToInt(hillWallYaw / 90f));

        // 월드 → 로컬: outward가 N이 되도록 CCW로 outward회 회전
        int lx = dx;
        int lz = dz;
        for (int i = 0; i < outward; i++)
        {
            int nx = -lz;
            int nz = lx;
            lx = nx;
            lz = nz;
        }

        for (int i = 0; i < 8; i++)
        {
            if (Dir8Dx[i] == lx && Dir8Dz[i] == lz)
            {
                dir8 = (HillTileSet.Dir8)i;
                return true;
            }
        }

        dir8 = HillTileSet.Dir8.N;
        return false;
    }

    static int Dir4Dx(int dir)
    {
        return NormalizeDir(dir) switch
        {
            0 => 0,
            1 => 1,
            2 => 0,
            _ => -1,
        };
    }

    static int Dir4Dz(int dir)
    {
        return NormalizeDir(dir) switch
        {
            0 => 1,
            1 => 0,
            2 => -1,
            _ => 0,
        };
    }

    // 벽 바깥(outward)을 볼 때, 커넥터 칸 기준으로 Hill이 오른쪽인지.
    bool ResolveHillOnRightForHxw(CellCoord connectorSlot, int dirToHill, int wallOutward)
    {
        dirToHill = NormalizeDir(dirToHill);
        wallOutward = NormalizeDir(wallOutward);
        int rightDir = (wallOutward + 1) % 4;
        int leftDir = (wallOutward + 3) % 4;

        if (dirToHill == rightDir)
            return true;
        if (dirToHill == leftDir)
            return false;

        // Hill→커넥터 방향으로 좌우 추론
        int toConnector = OppositeDir(dirToHill);
        if (toConnector == rightDir)
            return false; // 커넥터가 Hill 오른쪽 → 커넥터에서 Hill은 왼쪽
        if (toConnector == leftDir)
            return true;

        return true;
    }

    // Hill 쪽을 제외한 직교 이웃에 W 또는 WxW In/Out이 있으면 true
    bool HasImmediateWallOrWxwNeighbor(CellCoord cornerSlot, int dirToHill)
    {
        dirToHill = NormalizeDir(dirToHill);

        for (int d = 0; d < 4; d++)
        {
            if (d == dirToHill)
                continue;

            CellCoord n = Neighbor(cornerSlot, d);
            if (BuildHasHill(n) || BuildHasTop(n) || !BuildIsEdgeSlot(n))
                continue;

            EdgeKind kind = ClassifyEdgeKind(n);
            if (kind == EdgeKind.Straight ||
                kind == EdgeKind.OuterCorner ||
                kind == EdgeKind.InnerCorner)
                return true;
        }

        return false;
    }

    // 코너 옆 HXH vs HXW:
    // - 옆/이어짐이 W 또는 WxW In·Out → HXW (우선)
    // - 그게 없고 Hill이 이어지면 → HXH
    bool ShouldUseHxhInsteadOfHxw(CellCoord cornerSlot, CellCoord hillCell, int dirToHill)
    {
        dirToHill = NormalizeDir(dirToHill);

        bool sawWallOrWxw = false;
        bool sawHill = false;

        for (int d = 0; d < 4; d++)
        {
            if (d == dirToHill)
                continue;

            CellCoord n = Neighbor(cornerSlot, d);
            if (BuildHasHill(n))
            {
                sawHill = true;
                continue;
            }

            if (BuildIsEdgeSlot(n))
            {
                EdgeKind kind = ClassifyEdgeKind(n);
                if (kind == EdgeKind.Straight ||
                    kind == EdgeKind.OuterCorner ||
                    kind == EdgeKind.InnerCorner)
                {
                    sawWallOrWxw = true;
                    continue;
                }
            }

            EdgeWalkHit hit = ClassifyEdgeWalk(cornerSlot, d, maxSteps: 3);
            if (hit == EdgeWalkHit.WallOrWxw)
                sawWallOrWxw = true;
            else if (hit == EdgeWalkHit.Hill)
                sawHill = true;
        }

        if (sawWallOrWxw)
            return false;

        for (int d = 0; d < 4; d++)
        {
            CellCoord n = Neighbor(hillCell, d);
            if (n.Equals(cornerSlot))
                continue;
            if (BuildHasHill(n))
                return true;
        }

        return sawHill;
    }

    enum EdgeWalkHit
    {
        None,
        Hill,
        WallOrWxw,
    }

    // from에서 travelDir로 스캔. Hill / W·WxW 중 먼저 나오는 것.
    EdgeWalkHit ClassifyEdgeWalk(CellCoord from, int travelDir, int maxSteps)
    {
        CellCoord current = from;
        int dir = NormalizeDir(travelDir);

        for (int step = 0; step < maxSteps; step++)
        {
            CellCoord next = Neighbor(current, dir);

            if (BuildHasHill(next))
                return EdgeWalkHit.Hill;

            if (BuildHasTop(next))
                return EdgeWalkHit.None;

            if (!BuildIsEdgeSlot(next))
            {
                if (!TryPickNextEdgeDir(current, OppositeDir(dir), out dir))
                    return EdgeWalkHit.None;

                next = Neighbor(current, dir);
                if (BuildHasHill(next))
                    return EdgeWalkHit.Hill;
                if (BuildHasTop(next) || !BuildIsEdgeSlot(next))
                    return EdgeWalkHit.None;
            }

            EdgeKind kind = ClassifyEdgeKind(next);

            if (kind == EdgeKind.Straight ||
                kind == EdgeKind.OuterCorner ||
                kind == EdgeKind.InnerCorner)
                return EdgeWalkHit.WallOrWxw;
        }

        return EdgeWalkHit.None;
    }

    // excludeFromDir(들어온 방향)을 제외하고, Hill > 코너 > 직선 벽 순으로 다음 진행 방향 선택
    bool TryPickNextEdgeDir(CellCoord cell, int excludeFromDir, out int nextDir)
    {
        excludeFromDir = NormalizeDir(excludeFromDir);

        for (int d = 0; d < 4; d++)
        {
            if (d == excludeFromDir)
                continue;
            if (BuildHasHill(Neighbor(cell, d)))
            {
                nextDir = d;
                return true;
            }
        }

        int cornerDir = -1;
        int straightDir = -1;

        for (int d = 0; d < 4; d++)
        {
            if (d == excludeFromDir)
                continue;

            CellCoord n = Neighbor(cell, d);
            if (BuildHasTop(n) || !BuildIsEdgeSlot(n))
                continue;

            EdgeKind kind = ClassifyEdgeKind(n);
            if (kind == EdgeKind.OuterCorner || kind == EdgeKind.InnerCorner)
            {
                if (cornerDir < 0)
                    cornerDir = d;
            }
            else if (straightDir < 0)
            {
                straightDir = d;
            }
        }

        if (cornerDir >= 0)
        {
            nextDir = cornerDir;
            return true;
        }

        if (straightDir >= 0)
        {
            nextDir = straightDir;
            return true;
        }

        nextDir = 0;
        return false;
    }

    void ApplyHxhCornerPrefab(EdgeKind cornerKind, float edgeYaw, out GameObject prefab, out float yaw)
    {
        if (cornerKind == EdgeKind.OuterCorner)
        {
            prefab = hillTileSet.toOuterCorner != null
                ? hillTileSet.toOuterCorner
                : hillTileSet.wxh;
            yaw = edgeYaw + (hillTileSet.toOuterCorner != null
                ? hillTileSet.toOuterCornerYawOffset
                : hillTileSet.wxhYawOffset);
        }
        else
        {
            prefab = hillTileSet.toInnerCorner != null
                ? hillTileSet.toInnerCorner
                : hillTileSet.wxh;
            yaw = edgeYaw + (hillTileSet.toInnerCorner != null
                ? hillTileSet.toInnerCornerYawOffset
                : hillTileSet.wxhYawOffset);
        }
    }

    // 직선 벽 슬롯이 Top에서 어느 쪽으로 나와 있는지 → 벽 바깥 yaw (0/90/180/270)
    bool TryGetStraightOutwardYaw(CellCoord edgeCell, out float wallYaw)
    {
        wallYaw = 0f;
        int found = -1;
        int layer = hillBuildLayer;

        for (int dir = 0; dir < 4; dir++)
        {
            if (!HasTopLayer(Neighbor(edgeCell, dir), layer))
                continue;

            int outward = OppositeDir(dir);
            if (found >= 0 && found != outward)
                return false;

            found = outward;
        }

        if (found < 0)
            return false;

        wallYaw = found * 90f;
        return true;
    }

    EdgeKind ClassifyEdgeKind(CellCoord cell)
    {
        int layer = hillBuildLayer;
        if (IsInnerCornerCell(cell, layer))
            return EdgeKind.InnerCorner;
        if (IsOuterCornerSlot(cell, layer))
            return EdgeKind.OuterCorner;
        return EdgeKind.Straight;
    }

    // 직선 연결: 회전은 항상 벽의 바깥 방향(edgeYaw).
    // Hill이 벽을 바라볼 때 오른쪽에 있으면 localScale.x만 반전 (HxW).
    void ResolveStraightConnector(
        int dirToHill,
        float edgeYaw,
        out GameObject prefab,
        out float yaw,
        out bool flipScaleX)
    {
        dirToHill = NormalizeDir(dirToHill);
        prefab = hillTileSet.wxh;
        yaw = edgeYaw + hillTileSet.wxhYawOffset;
        flipScaleX = false;
        ApplyWxhMirror(dirToHill, edgeYaw, ref flipScaleX);
    }

    void ApplyWxhMirror(int dirToHill, float edgeYaw, ref bool flipScaleX)
    {
        int outward = NormalizeDir(Mathf.RoundToInt(edgeYaw / 90f));
        int right = (outward + 1) % 4;
        int left = (outward + 3) % 4;

        if (dirToHill == right)
            flipScaleX = true;
        else if (dirToHill == left)
            flipScaleX = false;
        else
            flipScaleX = false;

        if (hillTileSet.flipWxhWhenHillOnLeft)
            flipScaleX = !flipScaleX;
    }

    bool TryGetDirTowardHill(CellCoord slot, out int dir) =>
        TryGetDirTowardHill(slot, hillBuildLayer, out dir);

    bool TryGetDirTowardHill(CellCoord slot, int layer, out int dir)
    {
        for (int d = 0; d < 4; d++)
        {
            if (HasHillOnLayer(Neighbor(slot, d), layer))
            {
                dir = d;
                return true;
            }
        }

        dir = 0;
        return false;
    }

    bool IsOuterCornerSlot(CellCoord empty) => IsOuterCornerSlot(empty, 0);

    bool IsOuterCornerSlot(CellCoord empty, int layer)
    {
        if (HasTopLayer(empty, layer))
            return false;

        if (HasTopLayer(new CellCoord(empty.x - 1, empty.z - 1), layer) &&
            !HasTopLayer(new CellCoord(empty.x - 1, empty.z), layer) &&
            !HasTopLayer(new CellCoord(empty.x, empty.z - 1), layer))
            return true;

        if (HasTopLayer(new CellCoord(empty.x + 1, empty.z - 1), layer) &&
            !HasTopLayer(new CellCoord(empty.x + 1, empty.z), layer) &&
            !HasTopLayer(new CellCoord(empty.x, empty.z - 1), layer))
            return true;

        if (HasTopLayer(new CellCoord(empty.x - 1, empty.z + 1), layer) &&
            !HasTopLayer(new CellCoord(empty.x - 1, empty.z), layer) &&
            !HasTopLayer(new CellCoord(empty.x, empty.z + 1), layer))
            return true;

        if (HasTopLayer(new CellCoord(empty.x + 1, empty.z + 1), layer) &&
            !HasTopLayer(new CellCoord(empty.x + 1, empty.z), layer) &&
            !HasTopLayer(new CellCoord(empty.x, empty.z + 1), layer))
            return true;

        return false;
    }

    bool IsInnerCornerCell(CellCoord empty) => IsInnerCornerCell(empty, 0);

    bool IsInnerCornerCell(CellCoord empty, int layer)
    {
        if (HasTopLayer(empty, layer))
            return false;

        bool n = HasTopLayer(Neighbor(empty, 0), layer);
        bool e = HasTopLayer(Neighbor(empty, 1), layer);
        bool s = HasTopLayer(Neighbor(empty, 2), layer);
        bool w = HasTopLayer(Neighbor(empty, 3), layer);

        if (n && e && HasTopLayer(new CellCoord(empty.x + 1, empty.z + 1), layer))
            return true;
        if (e && s && HasTopLayer(new CellCoord(empty.x + 1, empty.z - 1), layer))
            return true;
        if (s && w && HasTopLayer(new CellCoord(empty.x - 1, empty.z - 1), layer))
            return true;
        if (w && n && HasTopLayer(new CellCoord(empty.x - 1, empty.z + 1), layer))
            return true;

        return false;
    }

    void TryPlaceInnerAtVertex(
        int vx,
        int vz,
        int layer,
        HashSet<CellCoord> visited,
        Transform root,
        HashSet<CellCoord> spawnedSlots)
    {
        CellCoord key = new CellCoord(vx, vz);

        if (!visited.Add(key))
            return;

        bool sw = HasTopLayer(new CellCoord(vx - 1, vz - 1), layer);
        bool se = HasTopLayer(new CellCoord(vx, vz - 1), layer);
        bool nw = HasTopLayer(new CellCoord(vx - 1, vz), layer);
        bool ne = HasTopLayer(new CellCoord(vx, vz), layer);

        int count = (sw ? 1 : 0) + (se ? 1 : 0) + (nw ? 1 : 0) + (ne ? 1 : 0);

        if (count != 3)
            return;

        float half = TileSize * 0.5f;
        Vector3 pos = EdgeVertexWorld(vx, vz, layer);

        float yaw;
        CellCoord emptyCell;

        if (!ne)
        {
            pos += new Vector3(half, 0f, half);
            yaw = 0f;
            emptyCell = new CellCoord(vx, vz);
        }
        else if (!nw)
        {
            pos += new Vector3(-half, 0f, half);
            yaw = 270f;
            emptyCell = new CellCoord(vx - 1, vz);
        }
        else if (!se)
        {
            pos += new Vector3(half, 0f, -half);
            yaw = 90f;
            emptyCell = new CellCoord(vx, vz - 1);
        }
        else
        {
            pos += new Vector3(-half, 0f, -half);
            yaw = 180f;
            emptyCell = new CellCoord(vx - 1, vz - 1);
        }

        SpawnEdgeSlot(
            emptyCell,
            EdgeKind.InnerCorner,
            layer,
            pos,
            yaw,
            $"Inner_L{layer}_{vx}_{vz}",
            root,
            spawnedSlots);
    }

    void SpawnRamps(Transform root)
    {
        if (tileSet.ramp == null || ramps == null)
            return;

        for (int i = 0; i < ramps.Count; i++)
        {
            RampEntry ramp = ramps[i];

            if (!HasTop(ramp.cell))
                continue;

            int dir = NormalizeDir(ramp.direction);
            CellCoord rampSlot = Neighbor(ramp.cell, dir);
            if (!HasGround(rampSlot) && !HasHill(rampSlot))
                continue;

            Vector3 center = CellCenterWorld(ramp.cell);
            center.y = EdgeSurfaceHeight;
            float size = TileSize;

            Vector3 offset = dir switch
            {
                0 => new Vector3(0f, 0f, size),
                1 => new Vector3(size, 0f, 0f),
                2 => new Vector3(0f, 0f, -size),
                _ => new Vector3(-size, 0f, 0f),
            };

            float yaw = dir * 90f;
            Spawn(
                tileSet.ramp,
                center + offset,
                yaw + tileSet.rampYawOffset,
                tileSet.edgeHeightOffset,
                $"Ramp_{ramp.cell.x}_{ramp.cell.z}_{dir}",
                root,
                matchStraightMeshBottom: true);
        }
    }

    void SpawnEdge(EdgeKind kind, Vector3 pos, float yaw, string name, Transform root)
    {
        // pos.y 는 호출 측에서 층 높이를 이미 넣습니다.
        GameObject prefab = null;
        float yawOffset = 0f;

        switch (kind)
        {
            case EdgeKind.Straight:
                prefab = tileSet.straight;
                yawOffset = tileSet.straightYawOffset;
                break;
            case EdgeKind.OuterCorner:
                prefab = tileSet.outerCorner;
                yawOffset = tileSet.outerCornerYawOffset;
                break;
            case EdgeKind.InnerCorner:
                prefab = tileSet.innerCorner;
                yawOffset = tileSet.innerCornerYawOffset;
                break;
        }

        Spawn(prefab, pos, yaw + yawOffset, tileSet.edgeHeightOffset, name, root, matchStraightMeshBottom: true);
    }

    void Spawn(
        GameObject prefab,
        Vector3 position,
        float yaw,
        float yOffset,
        string name,
        Transform root,
        bool matchStraightMeshBottom = false,
        bool flipScaleX = false)
    {
        if (prefab == null)
            return;

        position.y += yOffset;

#if UNITY_EDITOR
        GameObject instance = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefab, root);
#else
        GameObject instance = Instantiate(prefab, root);
#endif
        if (instance == null)
            return;

        instance.name = name;
        instance.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
        MarkGeneratedStatic(instance);

        if (flipScaleX)
        {
            Vector3 scale = instance.transform.localScale;
            scale.x = -scale.x;
            instance.transform.localScale = scale;
        }

        if (matchStraightMeshBottom)
            MatchMeshBottomToStraight(instance, prefab, flipScaleX);
    }

    static void MarkGeneratedStatic(GameObject instance)
    {
        if (instance == null)
            return;

#if UNITY_EDITOR
        const UnityEditor.StaticEditorFlags flags =
            UnityEditor.StaticEditorFlags.BatchingStatic |
            UnityEditor.StaticEditorFlags.OccludeeStatic |
            UnityEditor.StaticEditorFlags.OccluderStatic |
            UnityEditor.StaticEditorFlags.ContributeGI;

        UnityEditor.GameObjectUtility.SetStaticEditorFlags(instance, flags);

        Transform[] children = instance.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i] != null && children[i].gameObject != instance)
                UnityEditor.GameObjectUtility.SetStaticEditorFlags(children[i].gameObject, flags);
        }
#else
        instance.isStatic = true;
#endif
    }

    void MatchMeshBottomToStraight(GameObject instance, GameObject sourcePrefab, bool flipScaleX)
    {
        if (!TryGetCachedPrefabBottomOffset(sourcePrefab, flipScaleX, out float instanceBottomOffset))
        {
            if (!TryGetMeshBottomOffset(instance, out instanceBottomOffset))
                return;
        }

        float targetBottomOffset = 0f;

        if (tileSet != null && tileSet.straight != null)
        {
            if (!TryGetCachedPrefabBottomOffset(
                    tileSet.straight,
                    flipScaleX: false,
                    out float straightBottomOffset))
                return;

            targetBottomOffset = straightBottomOffset;
        }

        float delta = targetBottomOffset - instanceBottomOffset;

        if (Mathf.Abs(delta) > 0.0001f)
            instance.transform.position += new Vector3(0f, delta, 0f);
    }

    bool TryGetCachedPrefabBottomOffset(GameObject prefab, bool flipScaleX, out float bottomOffset)
    {
        bottomOffset = 0f;
        if (prefab == null)
            return false;

        // flip 여부에 따라 bounds가 달라질 수 있어 키를 분리
        int key = prefab.GetInstanceID() ^ (flipScaleX ? 1 : 0);
        if (prefabBottomOffsetCache.TryGetValue(key, out bottomOffset))
            return true;

        if (!TryGetPrefabMeshBottomOffset(prefab, flipScaleX, out bottomOffset))
            return false;

        prefabBottomOffsetCache[key] = bottomOffset;
        return true;
    }

    static bool TryGetPrefabMeshBottomOffset(GameObject prefab, bool flipScaleX, out float bottomOffset)
    {
        bottomOffset = 0f;

        if (prefab == null)
            return false;

#if UNITY_EDITOR
        GameObject temp = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefab);
#else
        GameObject temp = Instantiate(prefab);
#endif
        if (temp == null)
            return false;

        temp.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        if (flipScaleX)
        {
            Vector3 scale = temp.transform.localScale;
            scale.x = -scale.x;
            temp.transform.localScale = scale;
        }

        bool ok = TryGetMeshBottomOffset(temp, out bottomOffset);

#if UNITY_EDITOR
        DestroyImmediate(temp);
#else
        Destroy(temp);
#endif
        return ok;
    }

    static bool TryGetMeshBottomOffset(GameObject instance, out float bottomOffset)
    {
        bottomOffset = 0f;
        Renderer[] renderers = instance.GetComponentsInChildren<Renderer>();

        if (renderers == null || renderers.Length == 0)
            return false;

        Bounds bounds = renderers[0].bounds;

        for (int i = 1; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                bounds.Encapsulate(renderers[i].bounds);
        }

        bottomOffset = bounds.min.y - instance.transform.position.y;
        return true;
    }

    void OnValidate()
    {
        lookupDirty = true;
    }

    void OnDrawGizmosSelected()
    {
        if (!drawGizmos)
            return;

        EnsureLookup();
        float size = TileSize;

        if (tops != null)
        {
            Gizmos.color = topGizmoColor;

            for (int i = 0; i < tops.Count; i++)
            {
                TopEntry entry = tops[i];
                for (int layer = 0; layer <= entry.maxLayer; layer++)
                {
                    Vector3 center = CellCenterWorldAtTop(entry.cell, layer);
                    center.y += 0.05f;
                    Gizmos.DrawCube(center, new Vector3(size * 0.95f, 0.1f, size * 0.95f));
                }
            }
        }

        if (hills != null)
        {
            Gizmos.color = hillGizmoColor;

            for (int i = 0; i < hills.Count; i++)
                DrawHillGizmoCell(hills[i].cell, hills[i].layer, size);
        }

        if (groundCells != null)
        {
            Gizmos.color = groundGizmoColor;

            for (int i = 0; i < groundCells.Count; i++)
            {
                Vector3 center = CellCenterWorld(groundCells[i]);
                center.y = EdgeSurfaceHeight + 0.02f;
                Gizmos.DrawCube(center, new Vector3(size * 0.92f, 0.05f, size * 0.92f));
            }
        }

        Gizmos.color = rampGizmoColor;

        if (ramps == null)
            return;

        for (int i = 0; i < ramps.Count; i++)
        {
            RampEntry ramp = ramps[i];
            Vector3 center = CellCenterWorld(ramp.cell) + Vector3.up * 0.15f;
            Gizmos.DrawSphere(center, size * 0.15f);
        }
    }

    void DrawHillGizmoCell(CellCoord cell, int layer, float size)
    {
        Vector3 center = CellCenterWorld(cell);
        center.y = GetEdgeSurfaceHeight(layer) + 0.08f;
        Gizmos.DrawCube(center, new Vector3(size * 0.9f, 0.15f, size * 0.9f));
    }
}
