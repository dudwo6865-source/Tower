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
    public Color allyUnitColor = new Color(0.25f, 0.95f, 0.35f, 1f);
    public Color allyBuildingColor = new Color(0.25f, 0.75f, 1f, 1f);

    [Header("Enemy Colors")]
    public Color enemyUnitColor = new Color(1f, 0.25f, 0.25f, 1f);
    public Color enemyBuildingColor = new Color(1f, 0.35f, 0.35f, 1f);
    [Tooltip("한번 본 적 건물이 현재 시야 밖일 때 표시 색입니다.")]
    public Color seenEnemyBuildingColor = new Color(0.85f, 0.35f, 0.35f, 0.75f);

    [Header("Sizes")]
    public float allyUnitSize = 6f;
    public float allyBuildingSize = 10f;
    public float enemyUnitSize = 6f;
    public float enemyBuildingSize = 10f;
    public float selectedScale = 1.35f;

    [Header("Pool")]
    public int initialPoolSize = 32;

    private RTSMinimap minimap;
    private FogOfWarManager fogManager;
    private RectTransform blipsRoot;

    private readonly Dictionary<SelectableEntity, BlipView> activeBlips =
        new Dictionary<SelectableEntity, BlipView>();

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
            ReleaseBlip(entity);

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
        public float size;
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
            return false;
        }

        bool isAlly = entity.ownerId == localPlayerOwnerId;
        Vector3 groundPosition = GetGroundPosition(entity.transform.position);
        bool currentlyVisible =
            fogManager == null || fogManager.IsVisible(groundPosition);

        if (isAlly)
        {
            display.isAlly = true;
            display.currentlyVisible = true;
            display.color = entity.entityType == SelectableEntityType.Building
                ? allyBuildingColor
                : allyUnitColor;
            display.size = entity.entityType == SelectableEntityType.Building
                ? allyBuildingSize
                : allyUnitSize;
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
            display.size = enemyUnitSize;
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
        display.size = enemyBuildingSize;
        return true;
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
        float size = display.size;

        if (entity.IsSelected)
            size *= selectedScale;

        blip.rect.anchoredPosition =
            minimap.WorldToMinimapLocal(entity.transform.position);

        blip.rect.sizeDelta = new Vector2(size, size);
        blip.image.color = display.color;
    }

    static Vector3 GetGroundPosition(Vector3 position)
    {
        Terrain activeTerrain = Terrain.activeTerrain;

        if (activeTerrain == null)
            return position;

        position.y = activeTerrain.SampleHeight(position) +
                     activeTerrain.transform.position.y;

        return position;
    }
}
