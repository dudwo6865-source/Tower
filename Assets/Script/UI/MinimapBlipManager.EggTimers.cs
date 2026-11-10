using System.Collections.Generic;
using TMPro;
using UnityEngine;

public partial class MinimapBlipManager
{
    [Header("알 부화 시간")]
    [Label("알 부화 시간 표시")]
    public bool showEggHatchTimers = true;

    [Label("알 부화 시간 텍스트 프리팹")]
    [Tooltip("알 아이콘마다 재사용할 TextMeshProUGUI 프리팹입니다. 앵커를 위쪽 중앙으로 두고 위치·크기·폰트·색상을 프리팹에서 설정합니다. 비워두면 시간을 표시하지 않습니다.")]
    public TextMeshProUGUI eggHatchTimerPrefab;

    // 블립 풀과 수명을 맞춰 텍스트를 재사용한다. 외형과 배치는 프리팹 설정을 따른다.
    readonly Dictionary<BlipView, TextMeshProUGUI> eggTimerViews =
        new Dictionary<BlipView, TextMeshProUGUI>();

    void UpdateEggTimer(BlipView blip, SelectableEntity entity)
    {
        eggTimerViews.TryGetValue(blip, out TextMeshProUGUI text);
        MonsterEgg egg = entity.GetComponent<MonsterEgg>();
        bool visible = showEggHatchTimers && eggHatchTimerPrefab != null &&
            egg != null && egg.isActiveAndEnabled && !egg.Resolved;
        string label = null;
        if (visible)
        {
            // 미션의 실제 남은 시간을 사용하므로 일시 정지와 게임 속도를 그대로 따른다.
            EggMissionController mission = egg.GetComponentInParent<EggMissionController>();
            if (mission != null && mission.TryGetMinimapHatchTime(egg, out float remaining, out bool hatching))
            {
                int seconds = Mathf.Max(0, Mathf.CeilToInt(remaining));
                label = hatching ? "부화 중" : $"{seconds / 60:00}:{seconds % 60:00}";
            }
        }

        if (label == null)
        {
            if (text != null) text.gameObject.SetActive(false);
            return;
        }

        if (text == null)
        {
            text = Instantiate(eggHatchTimerPrefab, blip.rect, false);
            text.raycastTarget = false;
            eggTimerViews[blip] = text;
        }
        if (text.text != label) text.text = label;
        text.gameObject.SetActive(true);
        text.transform.SetAsLastSibling();
    }

    void HideEggTimers()
    {
        foreach (TextMeshProUGUI text in eggTimerViews.Values)
            if (text != null) text.gameObject.SetActive(false);
    }
}
