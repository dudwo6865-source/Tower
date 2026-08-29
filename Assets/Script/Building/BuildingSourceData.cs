using UnityEngine;

/// <summary>
/// 이 건물을 만든 Buildable 데이터를 인스턴스에 기록합니다.
/// 설치된 건물에서 원본 에셋(업그레이드 단계 등)을 되찾을 때 씁니다.
/// </summary>
[DisallowMultipleComponent]
public class BuildingSourceData : MonoBehaviour
{
    [Tooltip("런타임 배치 시 자동으로 채워집니다. 씬에 미리 놓아둔 건물만 직접 지정하세요.")]
    public ScriptableObject sourceData;

    public IBuildablePlacementData Data => sourceData as IBuildablePlacementData;

    public BuildableTowerData TowerData => sourceData as BuildableTowerData;

    public static void Assign(GameObject target, IBuildablePlacementData data)
    {
        if (target == null || !(data is ScriptableObject asset))
            return;

        BuildingSourceData source = target.GetComponent<BuildingSourceData>();

        if (source == null)
            source = target.AddComponent<BuildingSourceData>();

        source.sourceData = asset;
    }

    public static BuildableTowerData ResolveTowerData(Component target)
    {
        if (target == null)
            return null;

        BuildingSourceData source = target.GetComponent<BuildingSourceData>();
        return source != null ? source.TowerData : null;
    }
}
