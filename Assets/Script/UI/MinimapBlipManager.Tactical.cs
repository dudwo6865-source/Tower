using System;
using System.Collections.Generic;
using UnityEngine;

public partial class MinimapBlipManager
{
    [Header("주요 건물 아이콘")]
    [Label("주요 건물 전용 아이콘")]
    public bool useSpecialBuildingIcons = true;
    [Label("본부 아이콘")]
    [Tooltip("비워두면 기본 본부 모양을 그립니다.")]
    public Sprite headquartersIcon;
    [Label("스포너 아이콘")]
    [Tooltip("비워두면 기본 마름모 스포너 모양을 그립니다.")]
    public Sprite spawnerIcon;
    [Label("주요 건물 최소 크기")]
    [Min(4f)] public float specialIconSize = 14f;

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
        bool special = useSpecialBuildingIcons && (blip.headquarters || blip.spawner);
        Sprite sprite = special ? (blip.headquarters ? headquartersIcon : spawnerIcon) : null;
        blip.image.sprite = sprite;
        blip.image.preserveAspect = special;
        blip.image.enabled = !special || sprite != null;
        if (special)
        {
            float size = Mathf.Max(specialIconSize, Mathf.Max(blip.rect.sizeDelta.x, blip.rect.sizeDelta.y));
            if (entity.IsSelected) size = Mathf.Max(size, specialIconSize * selectedScale);
            blip.rect.sizeDelta = Vector2.one * size;
            if (sprite == null)
            {
                if (blip.glyph == null) blip.glyph = CreateGlyph(blip.rect, "주요 건물 아이콘");
                blip.glyph.SetShape(blip.headquarters ? MinimapTacticalGlyph.Shape.Headquarters : MinimapTacticalGlyph.Shape.Spawner);
                blip.glyph.color = tint;
            }
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
            blip.alert.rectTransform.sizeDelta = Vector2.one * Mathf.Max(damageAlertSize, specialIconSize * 1.5f) * Mathf.Lerp(.75f, 1.2f, phase);
            Color color = damageAlertColor;
            color.a *= 1f - phase * .8f;
            blip.alert.color = color;
            blip.rect.SetAsLastSibling();
        }
        if (blip.alert != null) blip.alert.gameObject.SetActive(alerting);
    }

    static MinimapTacticalGlyph CreateGlyph(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(MinimapTacticalGlyph));
        go.transform.SetParent(parent, false);
        var glyph = go.GetComponent<MinimapTacticalGlyph>();
        glyph.raycastTarget = false;
        glyph.rectTransform.anchorMin = Vector2.zero;
        glyph.rectTransform.anchorMax = Vector2.one;
        glyph.rectTransform.offsetMin = glyph.rectTransform.offsetMax = Vector2.zero;
        return glyph;
    }

    void StopWatchingHeadquarters(SelectableEntity entity)
    {
        if (!headquartersWatches.TryGetValue(entity, out var watch)) return;
        if (watch.health != null) watch.health.OnDamaged -= watch.handler;
        headquartersWatches.Remove(entity);
    }

    void OnDisable()
    {
        foreach (var watch in headquartersWatches.Values)
            if (watch.health != null) watch.health.OnDamaged -= watch.handler;
        headquartersWatches.Clear();
        foreach (var blip in activeBlips.Values)
            if (blip.alert != null) blip.alert.gameObject.SetActive(false);
    }
}
