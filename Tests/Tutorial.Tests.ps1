$ErrorActionPreference = 'Stop'
# 유니티 화면 대신 최소 대역을 사용하여 실제 진행 코드와 정지 소유권을 검증한다.
$stubs = @'
using System;
using System.Collections;
namespace UnityEngine {
 public class Object { public static void Destroy(object o) {} }
 public class ScriptableObject : Object {}
 public class GameObject { public bool activeInHierarchy = true; }
 public class MonoBehaviour : Object {
  public bool isActiveAndEnabled = true;
  public void StopAllCoroutines() {}
  public object StartCoroutine(IEnumerator routine) { return null; }
 }
 public class Sprite {}
 public static class Time { public static float timeScale = 1, unscaledTime; public static int frameCount; }
 public static class Debug { public static void LogWarning(string text, object context) {} }
 public enum RuntimeInitializeLoadType { SubsystemRegistration }
 public class RuntimeInitializeOnLoadMethodAttribute : Attribute { public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType t) {} }
 public class DefaultExecutionOrder : Attribute { public DefaultExecutionOrder(int i) {} }
 public class AddComponentMenu : Attribute { public AddComponentMenu(string s) {} }
 public class CreateAssetMenuAttribute : Attribute { public string menuName, fileName; }
 public class InspectorNameAttribute : Attribute { public InspectorNameAttribute(string s) {} }
 public class TooltipAttribute : Attribute { public TooltipAttribute(string s) {} }
 public class MinAttribute : Attribute { public MinAttribute(float f) {} }
 public class TextAreaAttribute : Attribute { public TextAreaAttribute(int a, int b) {} }
}
public class LabelAttribute : Attribute { public LabelAttribute(string s) {} }
public enum DayNightPhase { Day, Night }
public class DayNightCycle {
 public static DayNightCycle Instance;
 public bool autoStart = true, IsDay = true;
 public int CycleCount;
 public DayNightPhase CurrentPhase;
}
public class GameResultManager { public static GameResultManager Instance; public bool IsGameOver; }
public class StageIntroController { public static bool IsPlaying; }
public class MapConfig { public TutorialBinding[] tutorials; }
public class MapLoader { public static MapLoader Instance; public bool IsMapReady; public MapConfig LoadedConfig; }
public class BuildingConstructionGate { public static bool IsFeatureLockedOn(UnityEngine.GameObject o) { return false; } }
public class TutorialPanel {
 public bool isActiveAndEnabled = true;
 public UnityEngine.GameObject panelRoot = new UnityEngine.GameObject();
 public event Action NextRequested;
 public int shown;
 public bool CanShow(TutorialSequence s) { return s.pages != null && s.pages.Length > 0; }
 public void Show(TutorialPage p, bool block, bool advance) { shown++; panelRoot.activeInHierarchy = true; }
 public void Hide() { panelRoot.activeInHierarchy = false; }
 public void Click() { if (NextRequested != null) NextRequested(); }
}
'@
$source = $stubs
foreach ($name in @('GamePause', 'TutorialSequence', 'TutorialController')) {
    $code = Get-Content -Raw -Encoding UTF8 "$PSScriptRoot/../Assets/Script/Tutorial/$name.cs"
    # 파일별 using을 별도 네임스페이스 내부에 배치한다.
    $source += "`nnamespace TutorialTestCode {`n$code`n}`n"
}
$source = "using TutorialTestCode;`n" + $source
Add-Type -TypeDefinition $source
$script:checks = 0
function Check($condition, $message) { if (-not $condition) { throw $message }; $script:checks++ }
function Invoke-Hidden($instance, $method) {
    $instance.GetType().GetMethod($method, [Reflection.BindingFlags]'Instance,NonPublic').Invoke($instance, @()) | Out-Null
}
function Tick($controller) {
    [UnityEngine.Time]::frameCount += 2
    [UnityEngine.Time]::unscaledTime += 1
    Invoke-Hidden $controller 'Update'
}
function New-Sequence {
    $s = [TutorialTestCode.TutorialSequence]::new()
    $s.pages = @([TutorialTestCode.TutorialPage]::new())
    return $s
}
$a = [object]::new(); $b = [object]::new()
[UnityEngine.Time]::timeScale = 2
[TutorialTestCode.GamePause]::Acquire($a)
[TutorialTestCode.GamePause]::Acquire($a)
[TutorialTestCode.GamePause]::Acquire($b)
[TutorialTestCode.GamePause]::Release($a)
Check ([UnityEngine.Time]::timeScale -eq 0) '다른 화면이 남으면 정지를 유지한다'
[TutorialTestCode.GamePause]::Release($a)
Check ([UnityEngine.Time]::timeScale -eq 0) '중복 해제가 다른 화면의 정지를 풀지 않는다'
[TutorialTestCode.GamePause]::Release($b)
Check ([UnityEngine.Time]::timeScale -eq 2) '마지막 화면이 닫히면 이전 배속을 복원한다'
[UnityEngine.Time]::timeScale = 0
[TutorialTestCode.GamePause]::Acquire($a); [TutorialTestCode.GamePause]::Release($a)
Check ([UnityEngine.Time]::timeScale -eq 0) '원래 정지한 상태를 유지한다'
[TutorialTestCode.GamePause]::ResetForScene()
[TutorialTestCode.GamePause]::Release($a)
Check ([UnityEngine.Time]::timeScale -eq 1) '씬 전환 후 이전 화면 해제는 무시한다'
$c = [TutorialTestCode.TutorialController]::new()
$c.panel = [TutorialPanel]::new()
Invoke-Hidden $c 'Awake'; Invoke-Hidden $c 'OnEnable'
$first = New-Sequence; $second = New-Sequence
$binding = [TutorialTestCode.TutorialBinding]::new(); $binding.sequence = $first
$c.Configure(@($binding))
[StageIntroController]::IsPlaying = $true
Tick $c
Check ($c.panel.shown -eq 0) '시작 연출 중에는 안내를 열지 않는다'
[StageIntroController]::IsPlaying = $false
[TutorialTestCode.GamePause]::Acquire($b)
Tick $c
Check ($c.panel.shown -eq 0) '다른 정지 화면이 닫힐 때까지 기다린다'
[TutorialTestCode.GamePause]::Release($b)
Tick $c
Check ($c.panel.shown -eq 1 -and [UnityEngine.Time]::timeScale -eq 0) '스테이지 시작 안내를 표시하고 정지한다'
$c.NextPage()
Check ($c.panel.panelRoot.activeInHierarchy) '열린 프레임의 클릭으로 닫히지 않는다'
$c.Play($first); $c.Play($second); $c.Play($second)
Tick $c; $c.NextPage()
Check (-not $c.panel.panelRoot.activeInHierarchy) '마지막 클릭으로 닫힌다'
Check ([TutorialTestCode.TutorialController]::BlocksGameplay) '닫은 클릭이 게임 입력으로 전달되지 않는다'
Tick $c
Check ($c.panel.shown -eq 2) '예약된 안내를 순서대로 표시한다'
Tick $c; $c.NextPage(); Tick $c
$c.Play($first); Tick $c
Check ($c.panel.shown -eq 2) '완료된 일회성 안내와 중복 예약은 다시 표시하지 않는다'
$practice = New-Sequence; $practice.pauseGame = $false; $practice.blockGameplay = $false; $practice.waitForEvent = $true
$follow = New-Sequence
$binding = [TutorialTestCode.TutorialBinding]::new()
$binding.trigger = [TutorialTestCode.TutorialEvent]::BuildingCompleted
$binding.prerequisite = $practice; $binding.sequence = $follow
$c.Configure(@($binding)); $c.Play($practice); Tick $c
Tick $c; $c.NextPage()
Check ($c.panel.panelRoot.activeInHierarchy -and [UnityEngine.Time]::timeScale -eq 1) '실습은 시간을 흐르게 하고 클릭으로 닫히지 않는다'
$c.Notify([TutorialTestCode.TutorialEvent]::UnitProduced, $null, 0)
Check ($c.panel.panelRoot.activeInHierarchy) '다른 이벤트는 실습을 완료하지 않는다'
$c.Notify([TutorialTestCode.TutorialEvent]::BuildingCompleted, $null, 0)
Check (-not $c.panel.panelRoot.activeInHierarchy) '지정 이벤트로 실습을 완료한다'
Tick $c
Check ([UnityEngine.Time]::timeScale -eq 0) '실습 완료 이벤트로 후속 안내를 연결한다'
[TutorialTestCode.GamePause]::Acquire($b)
$c.CancelAll()
Check ([UnityEngine.Time]::timeScale -eq 0) '안내 취소가 다른 화면의 정지를 풀지 않는다'
[TutorialTestCode.GamePause]::Release($b)
Check ([UnityEngine.Time]::timeScale -eq 1) '모든 화면이 닫히면 재개한다'
$c.Configure(@()); $c.Play($first); Tick $c
Check ($c.panel.panelRoot.activeInHierarchy) '스테이지 재설정 시 표시 이력을 초기화한다'
Invoke-Hidden $c 'OnDisable'
Check ([UnityEngine.Time]::timeScale -eq 1 -and -not $c.panel.panelRoot.activeInHierarchy) '비활성화 시 패널과 시간 정지를 정리한다'
Write-Output "튜토리얼 진행·일시정지 $script:checks 개 검증 통과"
