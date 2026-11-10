using System;
using System.Collections.Generic;
using UnityEngine;

public partial class MinimapBlipManager
{
    public enum IconShape
    {
        [InspectorName("기본 사각형")] DefaultBlip,
        [InspectorName("본부 모양")] Headquarters,
        [InspectorName("스포너 모양")] Spawner,
        [InspectorName("원형 링")] Ring
    }

    public enum IconColorMode
    {
        [InspectorName("소속 색 (아군/적)")] Team,
        [InspectorName("지정 색")] Custom,
        [InspectorName("이미지 원본 색")] Original
    }

    [Serializable]
    public class IconSetting
    {
        [Label("사용")]
        [Tooltip("끄면 이 아이콘 설정을 쓰지 않고 일반 블립으로 그립니다.")]
        public bool enabled = true;
        [Label("아이콘 이미지")]
        [Tooltip("미니맵에 그릴 스프라이트입니다. 비워두면 '이미지 없을 때 모양'으로 그립니다.")]
        public Sprite sprite;
        [Label("이미지 없을 때 모양")]
        public IconShape fallbackShape = IconShape.DefaultBlip;
        [Label("아이콘 크기(픽셀)")]
        [Tooltip("미니맵에서의 가로·세로 크기입니다. 0이면 일반 블립 크기(건물은 칸 수 기준)를 씁니다. 선택하면 '선택 시 크기 배율'만큼 커집니다.")]
        [Min(0f)] public float size = 14f;
        [Label("건물 크기보다 작게 그리지 않기")]
        [Tooltip("켜면 큰 건물은 칸 수 기준 크기와 아이콘 크기 중 큰 쪽으로 그립니다.")]
        public bool atLeastFootprint = true;
        [Label("색 방식")]
        [Tooltip("어느 방식이든 '발견했지만 시야 밖' 건물은 그 투명도를 따릅니다.")]
        public IconColorMode colorMode = IconColorMode.Team;
        [Label("지정 색")]
        [Tooltip("색 방식이 '지정 색'일 때 씁니다.")]
        public Color customColor = Color.white;
        [Label("항상 위에 그리기")]
        [Tooltip("켜면 다른 블립에 가려지지 않게 위에 그립니다.")]
        public bool drawOnTop = true;

        public IconSetting() { }
        public IconSetting(IconShape shape) { fallbackShape = shape; }

        public bool DrawsShape => sprite != null || fallbackShape != IconShape.DefaultBlip;

        public Color ResolveColor(Color teamColor)
        {
            Color color;
            switch (colorMode)
            {
                case IconColorMode.Custom: color = customColor; break;
                case IconColorMode.Original: color = Color.white; break;
                default: return teamColor;
            }
            color.a *= teamColor.a;
            return color;
        }
    }

    [Serializable]
    public class HighlightSetting
    {
        [Label("사용")]
        public bool enabled = true;
        [Label("강조 아이콘 이미지")]
        [Tooltip("강조 표시에 쓸 스프라이트입니다. 비워두면 '이미지 없을 때 모양'으로 그립니다.")]
        public Sprite sprite;
        [Label("이미지 없을 때 모양")]
        [Tooltip("'기본 사각형'을 고르면 채워진 사각형으로 그립니다.")]
        public IconShape fallbackShape = IconShape.Ring;
        [Label("색")]
        public Color color = new Color(1f, .85f, .2f, 1f);
        [Label("시작 크기(픽셀)")]
        [Tooltip("처음 나타날 때의 크기입니다. 여기서 끝 크기까지 알 쪽으로 좁혀집니다.")]
        [Min(1f)] public float startSize = 72f;
        [Label("끝 크기(픽셀)")]
        [Min(0f)] public float endSize = 12f;
        [Label("한 번 좁혀지는 시간(초)")]
        [Min(.05f)] public float duration = 1f;
        [Label("반복 횟수")]
        [Tooltip("좁혀지는 애니메이션을 몇 번 반복할지 정합니다.")]
        [Min(1)] public int repeatCount = 2;
        [Label("사라지기 시작 지점")]
        [Tooltip("한 번 좁혀지는 동안 이 비율이 지나면 서서히 투명해집니다. 1이면 끝까지 선명하다가 바로 사라집니다.")]
        [Range(0f, 1f)] public float fadeStart = .6f;
    }

    [Serializable]
    public class TypeIconSetting
    {
        [Label("타입 ID")]
        [Tooltip("SelectableEntity의 '타입 ID'(entityTypeId, 보통 프리팹 이름)와 같으면 이 아이콘을 씁니다.")]
        public string entityTypeId;
        [Label("아이콘")]
        public IconSetting icon = new IconSetting();
    }

    [Header("미니맵 아이콘")]
    [Label("본부 아이콘")]
    [Tooltip("Headquarters가 붙은 건물의 아이콘입니다.")]
    public IconSetting headquartersIcon = new IconSetting(IconShape.Headquarters);
    [Label("적 스포너 아이콘")]
    [Tooltip("EnemySpawner가 붙은 건물의 아이콘입니다.")]
    public IconSetting spawnerIcon = new IconSetting(IconShape.Spawner);
    [Label("알 아이콘")]
    [Tooltip("MonsterEgg(알 파괴 미션의 알)의 아이콘입니다. 이미지를 넣으면 그 이미지로, 비워두고 모양이 '기본 사각형'이면 일반 블립으로 그립니다.")]
    public IconSetting eggIcon = new IconSetting(IconShape.DefaultBlip);
    [Label("보스 아이콘")]
    [Tooltip("BossCombatAI가 붙은 보스의 아이콘입니다. 이미지를 넣으면 그 이미지로, 비워두고 모양이 '기본 사각형'이면 일반 블립으로 그립니다.")]
    public IconSetting bossIcon = new IconSetting(IconShape.DefaultBlip);
    [Label("알 출현 강조 표시")]
    [Tooltip("새 알이 맵에 놓이면 미니맵의 알 위치에 크게 나타났다가 알 쪽으로 좁혀지며 사라지는 강조 아이콘을 그립니다. 시야와 상관없이 표시합니다.")]
    public HighlightSetting eggSpawnHighlight = new HighlightSetting();
    [Label("타입별 아이콘")]
    [Tooltip("타입 ID로 특정 유닛·건물의 아이콘을 지정합니다. 위의 본부·스포너·알·보스 설정보다 우선합니다.")]
    public List<TypeIconSetting> typeIcons = new List<TypeIconSetting>();

    // 구성 요소 판별은 블립을 잡을 때 한 번만 하고, 설정은 매 프레임 고른다.
    // 그래서 플레이 중에 인스펙터에서 '사용'이나 목록을 바꿔도 바로 반영된다.
    IconSetting ResolveIconSetting(BlipView blip)
    {
        if (!string.IsNullOrEmpty(blip.typeId) && typeIcons != null)
            foreach (TypeIconSetting entry in typeIcons)
                if (entry != null && entry.icon != null && entry.icon.enabled && entry.entityTypeId == blip.typeId)
                    return entry.icon;
        if (blip.egg && eggIcon.enabled) return eggIcon;
        if (blip.boss && bossIcon.enabled) return bossIcon;
        if (blip.headquarters && headquartersIcon.enabled) return headquartersIcon;
        if (blip.spawner && spawnerIcon.enabled) return spawnerIcon;
        return null;
    }

    [Header("본부 피격 알림")]
    [Label("본부 피격 링 표시")]
    public bool showHeadquartersDamageAlert = true;
    [Label("피격 링 색")]
    public Color damageAlertColor = new Color(1f, .15f, .08f, 1f);
    [Label("피격 알림 지속 시간")]
    [Min(.1f)] public float damageAlertDuration = 4f;
    [Label("피격 링 크기")]
    [Min(8f)] public float damageAlertSize = 32f;
    [Label("피격 링 맥동 주기")]
    [Min(.1f)] public float damageAlertPulsePeriod = .8f;

    sealed class HeadquartersWatch
    {
        public EntityHealth health;
        public Action<float, SelectableEntity> handler;
        public float lastDamage = float.NegativeInfinity;
    }
    readonly Dictionary<SelectableEntity, HeadquartersWatch> headquartersWatches = new Dictionary<SelectableEntity, HeadquartersWatch>();

    void ApplyTacticalBlip(BlipView blip, SelectableEntity entity, Color tint)
    {
        IconSetting icon = ResolveIconSetting(blip);
        bool special = icon != null && icon.DrawsShape;
        Sprite sprite = icon != null ? icon.sprite : null;
        MinimapTacticalGlyph.Shape shape = icon == null ? MinimapTacticalGlyph.Shape.Ring
            : icon.fallbackShape == IconShape.Headquarters ? MinimapTacticalGlyph.Shape.Headquarters
            : icon.fallbackShape == IconShape.Spawner ? MinimapTacticalGlyph.Shape.Spawner
            : MinimapTacticalGlyph.Shape.Ring;
        Color color = icon != null ? icon.ResolveColor(tint) : tint;
        if (special && icon.size > 0f)
        {
            // 일반 블립 크기에는 이미 선택 배율이 들어 있으므로 아이콘 크기에만 따로 곱한다.
            float size = icon.size * (entity.IsSelected ? selectedScale : 1f);
            if (icon.atLeastFootprint) size = Mathf.Max(size, Mathf.Max(blip.rect.sizeDelta.x, blip.rect.sizeDelta.y));
            blip.rect.sizeDelta = Vector2.one * size;
        }
        blip.image.sprite = sprite;
        blip.image.preserveAspect = special;
        blip.image.enabled = !special || sprite != null;
        blip.image.color = color;
        if (special && sprite == null)
        {
            if (blip.glyph == null) blip.glyph = CreateGlyph(blip.rect, "주요 건물 아이콘");
            blip.glyph.SetShape(shape);
            blip.glyph.color = color;
        }
        if (blip.glyph != null) blip.glyph.gameObject.SetActive(special && sprite == null);

        bool ownHeadquarters = blip.headquarters && entity.ownerId == localPlayerOwnerId;
        if (ownHeadquarters && !headquartersWatches.ContainsKey(entity) && entity.CachedHealth != null)
        {
            var watch = new HeadquartersWatch { health = entity.CachedHealth };
            watch.handler = (damage, attacker) => { if (damage > 0f) watch.lastDamage = Time.unscaledTime; };
            watch.health.OnDamaged += watch.handler;
            headquartersWatches.Add(entity, watch);
        }
        if (!ownHeadquarters) StopWatchingHeadquarters(entity);
        bool alerting = ownHeadquarters && showHeadquartersDamageAlert && headquartersWatches.TryGetValue(entity, out var active)
            && Time.unscaledTime - active.lastDamage < Mathf.Max(.1f, damageAlertDuration);
        if (alerting)
        {
            if (blip.alert == null)
            {
                blip.alert = CreateGlyph(blip.rect, "본부 피격 알림");
                blip.alert.SetShape(MinimapTacticalGlyph.Shape.Ring);
            }
            float phase = Mathf.Repeat(Time.unscaledTime / Mathf.Max(.1f, damageAlertPulsePeriod), 1f);
            blip.alert.rectTransform.anchorMin = blip.alert.rectTransform.anchorMax = new Vector2(.5f, .5f);
            blip.alert.rectTransform.sizeDelta = Vector2.one * Mathf.Max(damageAlertSize, headquartersIcon.size * 1.5f) * Mathf.Lerp(.75f, 1.2f, phase);
            Color alertColor = damageAlertColor;
            alertColor.a *= 1f - phase * .8f;
            blip.alert.color = alertColor;
            blip.rect.SetAsLastSibling();
        }
        if (blip.alert != null) blip.alert.gameObject.SetActive(alerting);
    }

    static MinimapTacticalGlyph CreateGlyph(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(MinimapTacticalGlyph));
        go.transform.SetParent(parent, false);
        var glyph = go.GetComponent<MinimapTacticalGlyph>();
        glyph.raycastTarget = false;
        glyph.rectTransform.anchorMin = Vector2.zero;
        glyph.rectTransform.anchorMax = Vector2.one;
        glyph.rectTransform.offsetMin = glyph.rectTransform.offsetMax = Vector2.zero;
        return glyph;
    }

    sealed class SpawnHighlight
    {
        public Transform target;
        public Vector3 lastPosition;
        public float startTime;
        public RectTransform rect;
        public UnityEngine.UI.Image image;
        public MinimapTacticalGlyph glyph;
    }
    readonly List<SpawnHighlight> spawnHighlights = new List<SpawnHighlight>();
    readonly Stack<SpawnHighlight> highlightPool = new Stack<SpawnHighlight>();

    void OnEnable()
    {
        EggMissionController.EggSpawned += HandleEggSpawned;
    }

    void HandleEggSpawned(MonsterEgg egg)
    {
        if (egg == null || eggSpawnHighlight == null || !eggSpawnHighlight.enabled) return;
        // 미니맵이 아직 준비되지 않았어도 기록해 두고, 준비되면 그때부터 그린다.
        spawnHighlights.Add(new SpawnHighlight
        {
            target = egg.transform,
            lastPosition = egg.transform.position,
            startTime = Time.unscaledTime
        });
    }

    // 새 알 위치에 큰 강조 아이콘을 띄우고 알 중심으로 좁혀지며 사라지게 한다.
    void UpdateSpawnHighlights()
    {
        HighlightSetting setting = eggSpawnHighlight;
        float now = Time.unscaledTime;
        for (int i = spawnHighlights.Count - 1; i >= 0; i--)
        {
            SpawnHighlight h = spawnHighlights[i];
            float pulse = Mathf.Max(.05f, setting.duration);
            float elapsed = now - h.startTime;
            if (!setting.enabled || elapsed >= pulse * Mathf.Max(1, setting.repeatCount))
            {
                ReleaseHighlight(h);
                spawnHighlights.RemoveAt(i);
                continue;
            }
            if (h.rect == null) AcquireHighlightView(h);
            if (h.target != null) h.lastPosition = h.target.position;

            float phase = Mathf.Repeat(elapsed / pulse, 1f);
            float eased = 1f - (1f - phase) * (1f - phase);
            float size = Mathf.Lerp(setting.startSize, setting.endSize, eased);
            Color color = setting.color;
            if (setting.fadeStart < 1f)
                color.a *= 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(setting.fadeStart, 1f, phase));

            h.rect.anchoredPosition = minimap.WorldToMinimapLocal(h.lastPosition);
            h.rect.sizeDelta = Vector2.one * size;
            bool useGlyph = setting.sprite == null && setting.fallbackShape != IconShape.DefaultBlip;
            h.image.enabled = !useGlyph;
            h.image.sprite = setting.sprite;
            h.image.preserveAspect = setting.sprite != null;
            h.image.color = color;
            if (useGlyph)
            {
                if (h.glyph == null) h.glyph = CreateGlyph(h.rect, "강조 모양");
                h.glyph.SetShape(setting.fallbackShape == IconShape.Headquarters ? MinimapTacticalGlyph.Shape.Headquarters
                    : setting.fallbackShape == IconShape.Spawner ? MinimapTacticalGlyph.Shape.Spawner
                    : MinimapTacticalGlyph.Shape.Ring);
                h.glyph.color = color;
            }
            if (h.glyph != null) h.glyph.gameObject.SetActive(useGlyph);
            h.rect.SetAsLastSibling();
        }
    }

    void AcquireHighlightView(SpawnHighlight h)
    {
        if (highlightPool.Count > 0)
        {
            SpawnHighlight pooled = highlightPool.Pop();
            h.rect = pooled.rect;
            h.image = pooled.image;
            h.glyph = pooled.glyph;
            h.rect.gameObject.SetActive(true);
            return;
        }
        var go = new GameObject("알 출현 강조", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
        go.transform.SetParent(blipsRoot, false);
        h.rect = go.GetComponent<RectTransform>();
        h.rect.anchorMin = h.rect.anchorMax = h.rect.pivot = new Vector2(.5f, .5f);
        h.image = go.GetComponent<UnityEngine.UI.Image>();
        h.image.raycastTarget = false;
    }

    void ReleaseHighlight(SpawnHighlight h)
    {
        if (h.rect == null) return;
        h.rect.gameObject.SetActive(false);
        highlightPool.Push(new SpawnHighlight { rect = h.rect, image = h.image, glyph = h.glyph });
        h.rect = null;
    }

    void StopWatchingHeadquarters(SelectableEntity entity)
    {
        if (!headquartersWatches.TryGetValue(entity, out var watch)) return;
        if (watch.health != null) watch.health.OnDamaged -= watch.handler;
        headquartersWatches.Remove(entity);
    }

    void OnDisable()
    {
        HideEggTimers();
        EggMissionController.EggSpawned -= HandleEggSpawned;
        foreach (var h in spawnHighlights) ReleaseHighlight(h);
        spawnHighlights.Clear();
        foreach (var watch in headquartersWatches.Values)
            if (watch.health != null) watch.health.OnDamaged -= watch.handler;
        headquartersWatches.Clear();
        foreach (var blip in activeBlips.Values)
            if (blip.alert != null) blip.alert.gameObject.SetActive(false);
    }
}
