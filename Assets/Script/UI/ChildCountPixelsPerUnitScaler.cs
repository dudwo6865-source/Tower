using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 켜져 있는 자식 수에 따라 이 오브젝트 Image의 Pixels Per Unit Multiplier를
/// 표에 직접 지정한 값으로 바꿉니다.
/// 표에 없는 자식 수는 그보다 작은 것 중 가장 가까운 자식 수의 값을 씁니다.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(Image))]
public class ChildCountPixelsPerUnitScaler : MonoBehaviour
{
    [System.Serializable]
    public struct Entry
    {
        [Label("자식 수")]
        [Tooltip("켜져 있는 자식이 이 수일 때 아래 배율을 씁니다.")]
        public int childCount;

        [Label("Pixels Per Unit 배율")]
        [Tooltip("이 자식 수일 때 넣을 Pixels Per Unit Multiplier 값입니다.")]
        public float multiplier;
    }

    [Label("자식 수별 배율")]
    [Tooltip("자식 수마다 쓸 Pixels Per Unit Multiplier 값입니다. 표에 없는 자식 수는 그보다 작은 것 중 가장 가까운 값을 씁니다.")]
    [SerializeField] private List<Entry> entries = new List<Entry>
    {
        new Entry { childCount = 1, multiplier = 1f },
    };

    private Image image;
    private int lastCount = -1;

    private void OnEnable()
    {
        image = GetComponent<Image>();
        lastCount = -1;
        Refresh();
    }

    private void Update()
    {
        // 자식을 켜고 끄는 변화는 따로 알림이 없으므로 매 프레임 자식 수를 확인한다.
        Refresh();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // 표를 고치면 바로 반영한다. OnValidate 안에서 다른 컴포넌트를 바꾸지 않도록 다음 틱으로 미룬다.
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this == null || !isActiveAndEnabled)
                return;
            lastCount = -1;
            Refresh();
        };
    }
#endif

    private void Refresh()
    {
        if (image == null)
            image = GetComponent<Image>();
        if (image == null)
            return;

        int count = CountActiveChildren();
        if (count == lastCount)
            return;
        lastCount = count;

        if (!TryFindMultiplier(count, out float multiplier))
            return;
        if (Mathf.Approximately(image.pixelsPerUnitMultiplier, multiplier))
            return;

        // 0 이하는 이미지가 깨지므로 최소값으로 막는다.
        image.pixelsPerUnitMultiplier = Mathf.Max(0.01f, multiplier);

#if UNITY_EDITOR
        // 에디터에서 바뀐 값이 씬/프리팹에 저장되도록 표시한다.
        if (!Application.isPlaying)
        {
            UnityEditor.EditorUtility.SetDirty(image);
            UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(image);
        }
#endif
    }

    private int CountActiveChildren()
    {
        int count = 0;
        for (int i = 0; i < transform.childCount; i++)
        {
            if (transform.GetChild(i).gameObject.activeSelf)
                count++;
        }
        return count;
    }

    // 자식 수가 같은 항목이 있으면 그 값을, 없으면 더 작은 자식 수 중 가장 가까운 항목의 값을 찾는다.
    // 그것도 없으면 가장 작은 자식 수의 값을 쓴다.
    private bool TryFindMultiplier(int count, out float multiplier)
    {
        multiplier = 0f;
        if (entries == null || entries.Count == 0)
            return false;

        int bestBelow = int.MinValue;
        int smallest = int.MaxValue;
        float smallestValue = 0f;
        bool found = false;

        foreach (Entry entry in entries)
        {
            if (entry.childCount <= count && entry.childCount > bestBelow)
            {
                bestBelow = entry.childCount;
                multiplier = entry.multiplier;
                found = true;
            }
            if (entry.childCount < smallest)
            {
                smallest = entry.childCount;
                smallestValue = entry.multiplier;
            }
        }

        if (!found)
            multiplier = smallestValue;
        return true;
    }
}
