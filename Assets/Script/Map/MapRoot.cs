using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;

// 하나의 맵(지형 + 정적 오브젝트 + 사전 배치물)을 담는 루트 컴포넌트입니다.
// 맵 루트 프리팹의 최상위 GameObject에 붙이고, 그 아래에 지형/오브젝트를 둡니다.
// MapLoader가 이 프리팹을 인스턴스화한 뒤 BuildNavMesh()로 NavMesh를 굽고
// MapGrid를 갱신합니다.
[DisallowMultipleComponent]
public class MapRoot : MonoBehaviour
{
    [Tooltip("이 맵의 NavMesh를 굽는 NavMeshSurface입니다. 비워두면 자식에서 자동으로 찾습니다.")]
    public NavMeshSurface navMeshSurface;

    [Tooltip("로드 직후 런타임에 NavMesh를 다시 굽습니다.\n" +
        "런타임 굽기는 소스 메쉬의 'Read/Write Enabled'가 필요하고 빌드에서 비용이 큽니다.\n" +
        "고정 맵은 이 옵션을 끄고 에디터에서 미리 Bake해 두는 것을 권장합니다.")]
    public bool bakeNavMeshOnStart = false;

    void Reset()
    {
        navMeshSurface = GetComponentInChildren<NavMeshSurface>(true);
    }

    void Awake()
    {
        if (navMeshSurface == null)
            navMeshSurface = GetComponentInChildren<NavMeshSurface>(true);
    }

    // NavMesh를 (재)굽고 MapGrid 경계를 갱신한다. 로드 시 MapLoader가 호출한다.
    public bool BuildNavMesh()
    {
        if (navMeshSurface == null)
        {
            Debug.LogError(
                "MapRoot: NavMeshSurface가 없습니다. 맵 루트(또는 자식)에 NavMeshSurface를 추가하세요.",
                this);
            return false;
        }

        navMeshSurface.BuildNavMesh();
        RefreshMapGrid();
        return true;
    }

    // 이미 구운 NavMesh 데이터를 그대로 쓰는 경우(런타임 재굽기 없이) MapGrid만 갱신한다.
    public void RefreshMapGrid()
    {
        if (MapGrid.Instance != null)
            MapGrid.Instance.Refresh();
    }
}
