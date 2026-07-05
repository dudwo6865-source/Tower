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

    [Header("Player")]
    [Tooltip("비워두면(-1) FogOfWarManager 또는 UnitSelectionManager의 로컬 플레이어 ID를 사용합니다.")]
    public int localPlayerOwnerId = -1;

    [Header("Ally Colors")]
    [Tooltip("아군 유닛 블립 색입니다.")]
    public Color allyUnitColor = new Color(0.25f, 0.95f, 0.35f, 1f);

    [Tooltip("아군 건물 블립 색입니다.")]
    public Color allyBuildingColor = new Color(0.25f, 0.75f, 1f, 1f);

    [Header("Enemy Colors")]
    [Tooltip("현재 시야 안에 있는 적 유닛 블립 색입니다.")]
    public Color enemyUnitColor = new Color(1f, 0.25f, 0.25f, 1f);

    [Tooltip("현재 시야 안에 있는 적 건물 블립 색입니다.")]
    public Color enemyBuildingColor = new Color(1f, 0.35f, 0.35f, 1f);

    [Tooltip("한번 본 적 건물이 현재 시야 밖일 때 표시 색입니다.")]
    public Color seenEnemyBuildingColor = new Color(0.85f, 0.35f, 0.35f, 0.75f);

    [Header("Sizes")]
    [Tooltip("아군 유닛 블립 크기(픽셀)입니다.")]
    public float allyUnitSize = 6f;

    [Tooltip("적 유닛 블립 크기(픽셀)입니다.")]
    public float enemyUnitSize = 6f;

    [Tooltip("건물 블립 크기(픽셀). GridFootprint 칸 수 × 이 값으로 가로·세로가 결정됩니다.")]
    public float buildingBlipPixelsPerCell = 5f;

    [Tooltip("선택된 유닛·건물 블립에 곱할 크기 배율입니다.")]
    public float selectedScale = 1.35f;

    [Header("Pool")]
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

    private readonly Stack<BlipView> blipPool = new Stack<BlipView>();

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
        var stillActive = new HashSet<SelectableEntity>();

        foreach (SelectableEntity entity in SelectableRegistry.Entities)
        {
            if (!TryGetBlipDisplay(entity, out BlipDisplay display))
                continue;

            stillActive.Add(entity);
            BlipView blip = GetOrCreateBlip(entity);
            ApplyBlip(blip, entity, display);
        }

        var toRemove = new List<SelectableEntity>();

        foreach (KeyValuePair<SelectableEntity, BlipView> pair in activeBlips)
        {
            if (!stillActive.Contains(pair.Key))
                toRemove.Add(pair.Key);
        }

        foreach (SelectableEntity entity in toRemove)
        {
            ReleaseBlip(entity);
            enemyFogVisibility.Remove(entity);
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

        EntityHealth health = entity.GetComponent<EntityHealth>();

        if (health != null && !health.IsAlive)
        {
            seenEnemyBuildings.Remove(entity);
            enemyFogVisibility.Remove(entity);
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

        FogOfWarVisibility fogVisibility = entity.GetComponent<FogOfWarVisibility>();

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
