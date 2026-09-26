# Tower — 프로젝트 가이드

Unity **2022.3.26f1** 타워 디펜스 RTS. 낮/밤이 한 사이클(= 웨이브 1회)이고,
밤에 적이 본부로 진군합니다. 플레이어는 타워를 짓고 유닛을 생산해 막습니다.

---

## 작업 규칙

- **브랜치는 `Main`.** 작업 후 커밋하고 `git push -u origin Main`.
- PR은 **명시적으로 요청받았을 때만** 만듭니다. 평소엔 `Main`에 직접 푸시.
- 원격이 앞서 있으면 `git fetch origin Main` 후 머지하고 푸시합니다.
- **커밋 메시지는 한국어로 씁니다.** 첫 줄에 무엇을 했는지 요약하고, 필요하면
  빈 줄 뒤에 왜 그렇게 했는지를 적습니다. (맨 끝의 Co-Authored-By 등
  서명 줄은 원래 형식 그대로 둡니다.)
- 주석·`[Tooltip]`·`[Header]`·에디터 UI 문구는 **전부 한국어**로 씁니다.
  기존 파일의 톤(설명은 "~합니다", 코드 내 주석은 "~한다")을 따릅니다.
- 인스펙터에 노출되는 필드에는 **`[Label("한글 이름")]`을 붙여** 인스펙터 라벨을
  한글로 표시합니다. (`Assets/Script/Attributes/LabelAttribute.cs`)
  `[InspectorName]`은 enum 값에만 동작하므로 필드에 쓰지 않습니다.
  - 순서는 `[Header]` → `[Label]` → `[Tooltip]` → 필드입니다.
  - 배열/리스트 제목은 커스텀 에디터가 **없는** 스크립트에서만 한글로 바뀝니다.
    커스텀 에디터가 있으면 그 에디터에서 `new GUIContent("한글", property.tooltip)`로
    직접 라벨을 넘깁니다.
- 사용자는 코딩보다 **유니티 에디터 사용에 익숙합니다.** 인스펙터 설정으로
  풀 수 있는 문제는 코드를 고치기 전에 에디터 해결법을 먼저 안내합니다.
- 새 수치는 되도록 `[SerializeField]` / `public` 필드로 빼서 **인스펙터에서
  조절 가능하게** 합니다. 하드코딩된 매직 넘버를 남기지 않습니다.

### 검증

이 레포에는 유니티 CI가 없고, 원격 세션 컨테이너에도 유니티가 없습니다
(프록시가 `download.unity3d.com` / `license.unity3d.com`을 차단, docker 데몬 없음).
**컴파일 검증은 사용자가 로컬 유니티에서 직접 합니다.**
"유니티가 없어 검증하지 못했다"는 안내는 **매번 반복하지 않습니다.**
오류가 나면 사용자가 콘솔 메시지를 붙여넣어 줍니다.

더 나은 구현 방법이나 작업 방식이 보이면 **적극적으로 먼저 제안합니다.**

---

## 코드 지도

| 폴더 | 내용 |
|---|---|
| `Assets/Script/AI/` | 전투 AI. `CombatAIBase` → `MobileCombatAI` → `UnitCombatAI`(플레이어) / `EnemyCombatAI`(적), `TowerAI`, `TargetFinder` |
| `Assets/Script/Enemy/` | `WaveManager`, `WaveTuning`/`WavePlan`, `EnemySpawner`, `EnemySpawnUtility` |
| `Assets/Script/Building/` | 건물, 배치(`TowerPlacementController`), 생산, 본부, 업그레이드 |
| `Assets/Script/Grid/` | `MapGrid`(격자), `GridOccupancy`(점유), `GridFootprint`(건물 칸), `GridMovement`(목적지 스냅) |
| `Assets/Script/Map/` | `MapConfig`(스테이지 데이터 에셋), `MapLoader`, `MapRoot`, 절벽 페인터 |
| `Assets/Script/FogOfWar/` | 시야/안개. `FogOfWarVisibility`가 **렌더러만** 끕니다 |
| `Assets/Script/Editor/` | 에디터 툴. `StageEditorWindow`(스테이지 통합 편집), `EnemySpawnerEditor` |
| `Assets/Script/` 루트 | 선택/명령 입력(`UnitSelectionManager`, `UnitCommandController`, `UnitCommandHandler`, `BuildingCommandHandler`)과 각종 인디케이터 |

### 자주 쓰는 패턴

- 매니저는 `public static X Instance` 싱글톤. 중복 인스턴스는 `Destroy(this)`로
  **컴포넌트만** 지웁니다 — `Destroy(gameObject)`는 같은 오브젝트의 다른 매니저까지
  날려버립니다.
- 인디케이터(`CommandCursorIndicator`, `MoveDestinationIndicator`,
  `AttackTargetIndicator`)는 씬에 없으면 `EnsureInstance()`가 런타임에 만듭니다.
  그래서 **씬에서 인스펙터 값을 조절하려면 직접 오브젝트에 붙여야** 합니다.

---

## 시스템별 주의사항

### 웨이브 / 스포너

- **웨이브 = 낮+밤 한 주기.** 첫 낮이 웨이브 1, 밤이 끝나면 다음 웨이브.
- `WaveManager`는 스포너를 **새로 만들지 않습니다.** 맵/씬에 미리 배치된
  `EnemySpawner`들의 수치를 웨이브마다 조정할 뿐입니다.
- 스포너 인스펙터의 `enemiesPerSpawn` / `spawnInterval` / `maxAliveEnemies`는
  **기준값**입니다. 실제 값은 `Effective*` 프로퍼티(비직렬화)에 들어가므로
  **플레이 중 인스펙터에는 반영되지 않습니다.** 실효값은 `EnemySpawnerEditor`가
  그려주는 "웨이브 적용 현황" 패널에서 확인합니다.
- **근접·피격·파괴 시 스폰은 웨이브 배율(스폰 수·간격)과 생존 상한을 받지 않습니다.**
  인스펙터 값 그대로 스폰하고, 적 스탯 가중치(체력·공격력·이동 속도)만 적용됩니다.
  웨이브 보정은 주기 스폰에만 걸립니다.
- 웨이브 표의 **최대 생존 수는 배율이 아니라 절댓값**(`WaveTuning.maxAliveEnemies`)입니다.
  0이면 스포너 인스펙터의 `최대 생존 적 수`를 그대로 쓰고, 그 값도 0이면 무제한입니다.
  '이후 웨이브 증가율'의 최대 생존 수는 웨이브마다 **더하는** 수이고,
  밤 보정에 값이 있으면 그 값으로 **덮어씁니다.**
- 스포너의 스폰은 즉시 생성되지 않고 **`EnemySpawnScheduler` 대기열**에 예약돼
  프레임당 정해진 수(기본 4마리, 4ms 한도)만큼 나눠 생성됩니다. 예약분은
  `PendingSpawnCount`로 생존 상한 계산에 포함됩니다. 씬에 없으면 자동 생성되므로
  수치를 바꾸려면 씬 오브젝트에 직접 붙입니다. (`InitialEnemyPlacer`는 시작 시 한 번이라 즉시 생성)
- 실효값 계산식은 `EnemySpawner`의 `static GetEffective*` 헬퍼 한 곳에만 둡니다.
  런타임과 에디터 미리보기가 이 헬퍼를 공유합니다. 웨이브 보정 계산도 마찬가지로
  `WaveTuning.BuildForWave` 한 곳입니다.

- 시작 시 맵에 깔아두는 적은 `InitialEnemyPlacer`가 무리 단위로 배치합니다.
  스포너와 달리 한 번만 배치되고 다시 채워지지 않습니다.
  **적 프리팹의 `EnemyCombatAI.advanceToEnemyBuildings`는 기본이 켜짐이라**
  그냥 두면 본부로 걸어갑니다. 초기 배치 적은 배치 직후 이 값을 꺼서
  자리를 지키게 합니다. (스테이지의 '본부로 진군' 옵션)

### 스테이지 (MapConfig)

- `Tools > 맵 > 스테이지 에디터`에서 편집합니다. 웨이브 표, 낮/밤, 경제, 승리 조건.
- **`MapConfig` 값은 씬에 `MapLoader`가 있어야 적용됩니다.** `MapLoader`가 실행
  순서 -1000으로 각 매니저에 값을 주입합니다. 없으면 씬 매니저의 인스펙터 값이
  그대로 쓰입니다. (예: `Test 2.unity`에는 `MapLoader`가 없습니다.)

### 명령 입력

- **우클릭** = 기본 명령(이동/공격). 명령 모드가 켜져 있으면 우클릭은 **취소**입니다.
  `Esc`도 취소. 건물 배치도 동일하게 우클릭 취소입니다.
- 명령 모드(이동/공격/정찰/집결지) 중에는 커서에 `CommandCursorIndicator` 링이
  따라다닙니다. 대기 중엔 은은하게 맥동하고, 모드 진입/명령 확정 시 한 번 크게
  튑니다. 명령별로 색이 다릅니다.
- **아군 강제 공격은 의도된 동작입니다.** 공격 모드에서 아군을 찍으면 아군을
  공격합니다. 소속으로 대상을 거르지 마세요.
- 공격 모드에서 빈 지형을 찍으면 **공격 이동**(그 지점으로 가면서 마주치는 적과
  교전)입니다. 움직일 수 없는 타워만 선택한 경우엔 이동 없이 지면 표시만 남깁니다.

### 안개 / 시야

- `FogOfWarVisibility`는 시야 밖이면 **렌더러만 끕니다.** 오브젝트는 살아서
  계속 동작합니다. "적이 안 나온다"처럼 보이는 현상의 흔한 원인이므로,
  실제 존재 여부는 Hierarchy에서 `Monster(Clone)` 개수로 확인합니다.

### 그리드 / 이동

- 건물은 `GridFootprint`로 칸을 점유합니다. 적 스포너처럼 유닛이 나와야 하는
  건물은 `carveNavMesh`를 꺼서 NavMesh에 구멍을 뚫지 않습니다.
- 이동 목적지는 `GridMovement.SnapMoveDestination`으로 격자에 스냅하고
  NavMesh 위로 보정합니다.

---

## 디버깅 팁

- `WaveManager`가 웨이브마다 남기는 로그로 몇 개의 스포너에 수치가 적용됐는지
  확인할 수 있습니다: `WaveManager: 웨이브 1 (낮) — 스포너 3개에 적용: ...`
- `UnitCommandDebugLog`가 유닛에 내려간 명령을 기록합니다.
- 스폰 실패는 경고로 남습니다: `EnemySpawnUtility: 스폰 위치가 NavMesh에서 너무 멉니다.`
