using UnityEngine;

// 빌드 데이터가 아이콘을 별도로 지정하지 않았을 때
// 프리팹의 SelectableEntity.portrait에서 아이콘을 찾아주는 공용 헬퍼입니다.
public static class BuildableIconResolver
{
    public static Sprite ResolvePrefabPortrait(GameObject prefab)
    {
        if (prefab == null)
            return null;

        SelectableEntity selectable = prefab.GetComponent<SelectableEntity>();

        return selectable != null ? selectable.portrait : null;
    }
}
