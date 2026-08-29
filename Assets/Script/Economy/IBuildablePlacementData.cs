using UnityEngine;

public interface IBuildablePlacementData
{
    string BuildAssetName { get; }
    string DisplayName { get; }
    GameObject Prefab { get; }
    int WattCost { get; }
    int OwnerId { get; }
    // 빌드 버튼에 표시할 아이콘. 지정되지 않았으면 프리팹 SelectableEntity.portrait로 대체됩니다.
    Sprite Icon { get; }
    string GetEntityTypeId();
    Vector2Int GetFootprintCells();
}
