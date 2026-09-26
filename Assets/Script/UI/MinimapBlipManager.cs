using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RTSMinimap))]
[DefaultExecutionOrder(100)]
public class MinimapBlipManager : MonoBehaviour
{
    class BlipView
    {
        public RectTransform rect;
        public Image image;
    }

    [Header("플레이어")]
    [Label("로컬 플레이어 ID")]
    [Tooltip("비워두면(-1) FogOfWarManager 또는 UnitSelectionManager의 로컬 플레이어 ID를 사용합니다.")]
    public int localPlayerOwnerId = -1;

    [Header("아군 색")]
    [Label("아군 유닛 색")]
    [Tooltip("아군 유닛 블립 색입니다.")]
    public Color allyUnitColor = new Color(0.25f, 0.95f, 0.35f, 1f);

    [Label("아군 건물 색")]
    [Tooltip("아군 건물 블립 색입니다.")]
    public Color allyBuildingColor = new Color(0.25f, 0.75f, 1f, 1f);

    [Header("적 색")]
    [Label("적 유닛 색")]
    [Tooltip("현재 시야 안에 있는 적 유닛 블립 색입니다.")]
    public Color enemyUnitColor = new Color(1f, 0.25f, 0.25f, 1f);

    [Label("적 건물 색")]
    [Tooltip("현재 시야 안에 있는 적 건물 블립 색입니다.")]
    public Color enemyBuildingColor = new Color(1f, 0.35f, 0.35f, 1f);

    [Label("발견한 적 건물 색")]
    [Tooltip("한번 본 적 건물이 현재 시야 밖일 때 표시 색입니다.")]
    public Color seenEnemyBuildingColor = new Color(0.85f, 0.35f, 0.35f, 0.75f);

    [Header("크기")]
    [Label("아군 유닛 크기")]
    [Tooltip("아군 유닛 블립 크기(픽셀)입니다.")]
    public float allyUnitSize = 6f;

    [Label("적 유닛 크기")]
    [Tooltip("적 유닛 블립 크기(픽셀)입니다.")]
    public float enemyUnitSize = 6f;

    [Label("건물 칸당 픽셀")]
    [Tooltip("건물 블립 크기(픽셀). GridFootprint 칸 수 × 이 값으로 가로·세로가 결정됩니다.")]
    public float buildingBlipPixelsPerCell = 5f;

    [Label("선택 시 크기 배율")]
    [Tooltip("선택된 유닛·건물 블립에 곱할 크기 배율입니다.")]
    public float selectedScale = 1.35f;

    [Header("풀")]
    [Label("초기 풀 크기")]
    [Tooltip("미리 만들어 둘 블립 UI 개수입니다. 유닛/건물 수보다 크게 두면 런타임 생성이 줄어듭니다.")]
    public int initialPoolSize = 32;

    private RTSMinimap minimap;
    private FogOfWarManager fogManager;
    private RectTransform blipsRoot;

    private readonly Dictionary<SelectableEntity, BlipView> activeBlips =
        new Dictionary<SelectableEntity, BlipView>();

    private readonly Dictionary<SelectableEntity, bool> enemyFogVisibility =
        new Dictionary<SelectableEntity, bool>();

    private readonly HashSet<SelectableEntity> seenEnemyBuildings =
        new HashSet<SelectableEntity>();

    private readonly Dictionary<SelectableEntity, FogOfWarVisibility> fogVisibilityCache =
        new Dictionary<SelectableEntity, FogOfWarVisibility>();

    private readonly Stack<BlipView> blipPool = new Stack<BlipView>();

    // 매 프레임 새로 만들면 그대로 GC 부담이 된다. 한 번 만들어 두고 비워 쓴다.
    private readonly HashSet<SelectableEntity> stillActive =
        new HashSet<SelectableEntity>();

    private readonly List<SelectableEntity> toRemove = new List<SelectableEntity>();

    private bool initialized;

    void Awake()
    {
        minimap = GetComponent<RTSMinimap>();
    }

    void Start()
    {
        fogManager = FogOfWarManager.Instance;

        if (fogManager == null)
            fogManager = FindObjectOfType<FogOfWarManager>();

        if (localPlayerOwnerId < 0)
        {
            if (fogManager != null)
                localPlayerOwnerId = fogManager.LocalPlayerOwnerId;
            else if (UnitSelectionManager.Instance != null)
                localPlayerOwnerId = UnitSelectionManager.Instance.localPlayerOwnerId;
            else
                localPlayerOwnerId = 1;
        }
    }

    void LateUpdate()
    {
        if (minimap == null || !minimap.IsReady)
            return;

        if (!initialized)
        {
            EnsureBlipsRoot();

            for (int i = 0; i < initialPoolSize; i++)
                blipPool.Push(CreateBlipView());

            initialized = true;
        }

        RefreshBlips();
    }

    void EnsureBlipsRoot()
    {
        if (blipsRoot != null || minimap == null || !minimap.IsReady)
            return;

        GameObject rootObject = new GameObject(
            "BlipsContainer",
            typeof(RectTransform));

        rootObject.transform.SetParent(minimap.MinimapRect, false);

        blipsRoot = rootObject.GetComponent<RectTransform>();
        blipsRoot.anchorMin = Vector2.zero;
        blipsRoot.anchorMax = Vector2.one;
        blipsRoot.offsetMin = Vector2.zero;
        blipsRoot.offsetMax = Vector2.zero;
    }

    void RefreshBlips()
    {
        stillActive.Clear();
        toRemove.Clear();

        foreach (SelectableEntity entity in SelectableRegistry.Entities)
        {
            if (!TryGetBlipDisplay(entity, out BlipDisplay display))
                continue;

            stillActive.Add(entity);
            BlipView blip = GetOrCreateBlip(entity);
            ApplyBlip(blip, entity, display);
        }

        foreach (KeyValuePair<SelectableEntity, BlipView> pair in activeBlips)
        {
            if (!stillActive.Contains(pair.Key))
                toRemove.Add(pair.Key);
        }

        foreach (SelectableEntity entity in toRemove)
        {
            ReleaseBlip(entity);
            enemyFogVisibility.Remove(entity);
            fogVisibilityCache.Remove(entity);
        }

        foreach (SelectableEntity entity in stillActive)
        {
            if (entity == null || !entity.IsSelected)
                continue;

            if (activeBlips.TryGetValue(entity, out BlipView blip))
                blip.rect.SetAsLastSibling();
        }
    }

    struct BlipDisplay
    {
        public bool isAlly;
        public bool currentlyVisible;
        public Vector2 size;
        public Color color;
    }

    bool TryGetBlipDisplay(
        SelectableEntity entity,
        out BlipDisplay display)
    {
        display = default;

        if (entity == null)
            return false;

        EntityHealth health = entity.CachedHealth;

        if (health != null && !health.IsAlive)
        {
            seenEnemyBuildings.Remove(entity);
            enemyFogVisibility.Remove(entity);
            fogVisibilityCache.Remove(entity);
            return false;
        }

        bool isAlly = entity.ownerId == localPlayerOwnerId;
        bool currentlyVisible = IsEntityCurrentlyVisible(entity);

        if (isAlly)
        {
            display.isAlly = true;
            display.currentlyVisible = true;
            display.color = entity.entityType == SelectableEntityType.Building
                ? allyBuildingColor
                : allyUnitColor;
            display.size = entity.entityType == SelectableEntityType.Building
                ? GetBuildingBlipSize(entity)
                : new Vector2(allyUnitSize, allyUnitSize);
            return true;
        }

        if (fogManager == null)
            return false;

        if (entity.entityType == SelectableEntityType.Unit)
        {
            if (!currentlyVisible)
                return false;

            display.isAlly = false;
            display.currentlyVisible = true;
            display.color = enemyUnitColor;
            display.size = new Vector2(enemyUnitSize, enemyUnitSize);
            return true;
        }

        if (currentlyVisible)
            seenEnemyBuildings.Add(entity);

        if (!seenEnemyBuildings.Contains(entity))
            return false;

        display.isAlly = false;
        display.currentlyVisible = currentlyVisible;
        display.color = currentlyVisible
            ? enemyBuildingColor
            : seenEnemyBuildingColor;
        display.size = GetBuildingBlipSize(entity);
        return true;
    }

    Vector2 GetBuildingBlipSize(SelectableEntity entity)
    {
        Vector2Int footprintCells =
            GridFootprint.ResolveFootprintCells(entity.gameObject);

        float perCell = Mathf.Max(0.1f, buildingBlipPixelsPerCell);

        return new Vector2(
            footprintCells.x * perCell,
            footprintCells.y * perCell);
    }

    BlipView GetOrCreateBlip(SelectableEntity entity)
    {
        if (activeBlips.TryGetValue(entity, out BlipView existing))
            return existing;

        BlipView blip = blipPool.Count > 0
            ? blipPool.Pop()
            : CreateBlipView();

        blip.rect.gameObject.SetActive(true);
        activeBlips.Add(entity, blip);
        return blip;
    }

    void ReleaseBlip(SelectableEntity entity)
    {
        if (!activeBlips.TryGetValue(entity, out BlipView blip))
            return;

        activeBlips.Remove(entity);
        blip.rect.gameObject.SetActive(false);
        blipPool.Push(blip);
    }

    BlipView CreateBlipView()
    {
        GameObject blipObject = new GameObject(
            "Blip",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));

        blipObject.transform.SetParent(blipsRoot, false);

        RectTransform rect = blipObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);

        Image image = blipObject.GetComponent<Image>();
        image.raycastTarget = false;

        blipObject.SetActive(false);

        return new BlipView
        {
            rect = rect,
            image = image
        };
    }

    void ApplyBlip(
        BlipView blip,
        SelectableEntity entity,
        BlipDisplay display)
    {
        Vector2 size = display.size;

        if (entity.IsSelected)
            size *= selectedScale;

        blip.rect.anchoredPosition =
            minimap.WorldToMinimapLocal(entity.transform.position);

        blip.rect.sizeDelta = size;
        blip.image.color = display.color;
    }

    bool IsEntityCurrentlyVisible(SelectableEntity entity)
    {
        if (fogManager == null)
            return true;

        // 엔티티마다 매 프레임 GetComponent를 부르면 유닛이 많아질수록 그대로 비용이 된다.
        if (!fogVisibilityCache.TryGetValue(entity, out FogOfWarVisibility fogVisibility))
        {
            fogVisibility = entity.GetComponent<FogOfWarVisibility>();
            fogVisibilityCache[entity] = fogVisibility;
        }

        if (fogVisibility != null)
            return fogVisibility.IsCurrentlyVisible;

        if (!enemyFogVisibility.TryGetValue(entity, out bool wasVisible))
            wasVisible = false;

        wasVisible = fogManager.EvaluateEntityGameplayVisibility(
            entity.SelectionBounds,
            wasVisible);
        enemyFogVisibility[entity] = wasVisible;
        return wasVisible;
    }
}
