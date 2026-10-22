// 실제 EntityHealth의 피해·회복 경로를 실행하기 위한 유니티 의존성 대체 구현이다.
using System;
namespace UnityEngine
{
    public class DefaultExecutionOrder : Attribute { public DefaultExecutionOrder(int order) {} }
    public class RequireComponent : Attribute { public RequireComponent(Type component) {} }
    public class GameObject {}
    public struct Vector3 {}
    public struct Quaternion { public static Quaternion identity => new Quaternion(); }
    public class Transform { public Vector3 position; }
    public class Collider { public bool enabled; }
    public struct Bounds { public Vector3 center; }
}
namespace UnityEngine.AI
{
    public class NavMeshAgent { public bool enabled; }
    public class NavMeshObstacle { public bool enabled; }
}
public enum SelectableEntityType { Unit, Building }
public class SelectableEntity
{
    public SelectableEntityType entityType;
    public int ownerId;
    public UnityEngine.Collider SelectionCollider;
    public UnityEngine.Bounds SelectionBounds;
}
public static class BuildingRegistry
{
    public static void Register(SelectableEntity entity) {}
    public static void NotifyRemoved(SelectableEntity entity) {}
}
public class CombatAIBase
{
    public float aggroRange;
    public bool enabled;
    public void NotifyAttackedBy(SelectableEntity attacker) {}
}
public static class MobileCombatAI
{
    public static void RallyAlliesAround(UnityEngine.Vector3 position, int ownerId,
        SelectableEntity entity, SelectableEntity attacker, float range) {}
}
public static class CombatEffectSpawner
{
    public static void Spawn(UnityEngine.GameObject prefab, UnityEngine.Vector3 center, UnityEngine.Quaternion rotation) {}
}
public class UnitAttacker { public bool enabled; }
public class UnitMovement { public bool enabled; }
