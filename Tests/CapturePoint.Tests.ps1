# 실제 CapturePoint 코드를 최소 대역과 함께 실행한다. 유니티 씬 검증을 대체하지 않는다.
$ErrorActionPreference = 'Stop'
$captureSource = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot '../Assets/Script/Mission/CapturePoint.cs')
$testSupport = @'
namespace UnityEngine {
    public class MonoBehaviour { public bool isActiveAndEnabled = true; public Transform transform = new Transform(); }
    public class Transform { public Vector3 position; }
    public struct Vector3 {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x=x; this.y=y; this.z=z; }
        public static Vector3 operator -(Vector3 a, Vector3 b) { return new Vector3(a.x-b.x,a.y-b.y,a.z-b.z); }
    }
    public class DisallowMultipleComponent : System.Attribute {}
    public class TooltipAttribute : System.Attribute { public TooltipAttribute(string value) {} }
    public static class Mathf { public static float Min(float a, float b) { return System.Math.Min(a,b); } }
}
public class LabelAttribute : System.Attribute { public LabelAttribute(string value) {} }
public enum SelectableEntityType { Unit, Building }
public class EntityHealth { public bool IsAlive = true; }
public class SelectableEntity : UnityEngine.MonoBehaviour {
    public int ownerId = 1;
    public SelectableEntityType entityType;
    public EntityHealth CachedHealth = new EntityHealth();
}
public static class SelectableRegistry {
    public static System.Collections.Generic.List<SelectableEntity> Entities = new System.Collections.Generic.List<SelectableEntity>();
}
public static class CapturePointTests {
    static int count;
    static void Check(bool condition, string description) {
        if (!condition) throw new System.Exception(description);
        count++;
    }
    public static int Run() {
        var point = new CapturePoint();
        var ally = new SelectableEntity();
        SelectableRegistry.Entities.Clear();
        point.Tick(1,5,2);
        Check(point.Elapsed == 0, "빈 거점은 진행하지 않는다");
        SelectableRegistry.Entities.Add(ally);
        point.Tick(1,5,2);
        Check(point.Elapsed == 2, "아군 진입 후 진행한다");
        var enemy = new SelectableEntity { ownerId = 2 };
        SelectableRegistry.Entities.Add(enemy);
        point.Tick(1,5,1);
        Check(point.Elapsed == 2 && point.State == CapturePoint.CaptureState.Contested, "적 진입 시 진행을 보존한다");
        enemy.CachedHealth.IsAlive = false;
        point.Tick(1,5,1);
        Check(point.Elapsed == 3, "적 사망 후 진행을 재개한다");
        ally.isActiveAndEnabled = false;
        point.Tick(1,5,1);
        Check(point.Elapsed == 0 && point.WasReset, "마지막 아군 비활성화 시 초기화한다");
        ally.isActiveAndEnabled = true;
        ally.entityType = SelectableEntityType.Building;
        point.Tick(1,5,1);
        Check(point.Elapsed == 0, "아군 건물만으로 점령하지 않는다");
        ally.entityType = SelectableEntityType.Unit;
        ally.transform.position = new UnityEngine.Vector3(8,100,0);
        point.Tick(1,5,1);
        Check(point.Elapsed == 1, "높이와 무관하게 경계를 포함한다");
        ally.transform.position = new UnityEngine.Vector3(8.01f,0,0);
        point.Tick(1,5,1);
        Check(point.Elapsed == 0, "경계 밖으로 나가면 초기화한다");
        ally.transform.position = new UnityEngine.Vector3(0,0,0);
        enemy.CachedHealth.IsAlive = true;
        enemy.entityType = SelectableEntityType.Building;
        point.Tick(1,5,1);
        Check(point.State == CapturePoint.CaptureState.Contested, "적 건물도 점령을 방해한다");
        enemy.isActiveAndEnabled = false;
        SelectableRegistry.Entities.Add(new SelectableEntity());
        point.Tick(1,5,1);
        Check(point.Elapsed == 1, "여러 아군이 있어도 속도는 같다");
        point.Tick(1,5,0);
        Check(point.Elapsed == 1, "일시 정지 시 시간이 흐르지 않는다");
        point.Tick(1,5,10);
        Check(point.Elapsed == 5 && point.State == CapturePoint.CaptureState.Complete, "완료 시 목표 시간에 고정한다");
        SelectableRegistry.Entities.Clear();
        point.Tick(1,5,1);
        Check(point.State == CapturePoint.CaptureState.Complete, "완료는 다시 초기화되지 않는다");
        point.ResetProgress();
        Check(point.Elapsed == 0 && !point.WasReset && point.State == CapturePoint.CaptureState.Waiting, "재시작은 상태를 모두 초기화한다");
        SelectableRegistry.Entities.Add(ally);
        point.Tick(1,0,1);
        point.Tick(1,float.NaN,1);
        point.Tick(1,float.PositiveInfinity,1);
        Check(point.Elapsed == 0, "잘못된 시간으로 진행하지 않는다");
        point.radius = -1;
        point.Tick(1,5,1);
        Check(point.Elapsed == 0 && !point.HasValidRadius, "잘못된 반경은 판정하지 않는다");
        point.radius = 8;
        point.Tick(1,5,2);
        ally.CachedHealth.IsAlive = false;
        enemy.isActiveAndEnabled = true;
        SelectableRegistry.Entities.Add(enemy);
        point.Tick(1,5,1);
        Check(point.Elapsed == 0 && point.State == CapturePoint.CaptureState.Waiting, "아군 사망은 적 존재보다 초기화를 우선한다");
        return count;
    }
}
'@
Add-Type -TypeDefinition ($captureSource + "`n" + $testSupport)
$passed = [CapturePointTests]::Run()
Write-Output "점령 판정 검증 $passed 개 통과"
