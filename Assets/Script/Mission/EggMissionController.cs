using System;
using System.Collections.Generic;
using UnityEngine;

// 알 파괴 미션: 시간이 지나면 맵에 무작위로 알을 놓고, 알마다 따로 부화 시간을 센다.
// 정해진 수의 알을 파괴하면 승리한다. 맵 루트에 미리 놓은 자식 알도 함께 사용한다.
[DisallowMultipleComponent]
[AddComponentMenu("게임/미션/EggMissionController")]
public class EggMissionController : MonoBehaviour
{
    // 미니맵에는 부화 대기 또는 연출 중인 알의 실제 타이머만 전달한다.
    public bool TryGetMinimapHatchTime(MonsterEgg egg, out float remaining, out bool hatching)
    {
        MonsterEgg.HatchStatus status = GetEggStatus(egg, out remaining, out _, out _);
        hatching = status == MonsterEgg.HatchStatus.Hatching;
        return hatching || status == MonsterEgg.HatchStatus.NextHatch;
    }

    public EggMissionRules Rules { get; private set; }
    public string Error { get; private set; }
    public bool Complete => Error == null && Rules != null && Rules.Complete;
    public EggMissionSettings Settings { get; private set; }
    public int SpawnedEggs { get; private set; }
    // 시간이 지나 새 알이 맵에 놓였을 때 알린다(미리 놓은 알은 제외). 미니맵 강조 표시 등에 쓴다.
    public static event Action<MonsterEgg> EggSpawned;
    // 다음 알이 나오기까지 남은 시간. 더 나오지 않으면 -1이다.
    public float NextSpawnRemaining => !CanSpawnMore ? -1f : Mathf.Max(0f, nextSpawnTime - elapsed);

    readonly List<MonsterEgg> eggs = new List<MonsterEgg>();
    readonly List<int> expired = new List<int>();
    readonly List<Vector3> hqPositions = new List<Vector3>();
    GameResultManager owner;
    float elapsed;
    float nextSpawnTime;
    int retrySpawns;
    bool placementWarned;

    bool CanSpawnMore => Settings != null && Settings.eggPrefab != null && Rules != null && !Rules.Complete &&
        (Settings.maxTotalSpawns <= 0 || SpawnedEggs < Settings.maxTotalSpawns);

    public void Initialize(GameResultManager result, EggMissionSettings settings)
    {
        if (Rules != null || Error != null) return;
        owner = result;
        if (settings == null) { Fail("알 미션 설정이 없습니다."); return; }
        Settings = settings.Clone();
        MonsterEgg[] placed = GetComponentsInChildren<MonsterEgg>();
        string error = Settings.Validate(placed.Length);
        if (error != null) { Fail(error); return; }
        foreach (MonsterEgg egg in placed)
        {
            error = ValidateEgg(egg);
            if (error != null) { Fail(egg.name + ": " + error); return; }
        }
        Rules = new EggMissionRules(Settings.targetDestroyCount, Settings.hatchSeconds);
        nextSpawnTime = Settings.firstSpawnDelay;
        foreach (MonsterEgg egg in placed) Register(egg);
    }

    string ValidateEgg(MonsterEgg egg)
    {
        string error = egg.ValidateSetup();
        if (error == null && (!egg.isActiveAndEnabled || egg.Health == null || !egg.Health.IsAlive))
            error = "알의 컴포넌트를 활성화하고 체력을 확인하세요.";
        if (error == null && egg.GetComponent<SelectableEntity>().ownerId == owner.playerOwnerId)
            error = "알의 소유자 ID를 적 진영으로 설정하세요.";
        return error;
    }

    void Register(MonsterEgg egg)
    {
        Rules.AddEgg();
        eggs.Add(egg);
        egg.DestroyedByDamage += HandleEggDestroyed;
        egg.SetNextHatch(true);
    }

    void HandleEggDestroyed(MonsterEgg egg)
    {
        if (owner == null || owner.IsGameOver || Error != null) return;
        int index = eggs.IndexOf(egg);
        int previousCount = Settings.GetEggsPerSpawn(Rules.DestroyedEggs);
        if (Rules.ResolveEgg(index, false))
        {
            egg.SetNextHatch(false);
            int nextCount = Settings.GetEggsPerSpawn(Rules.DestroyedEggs);
            // 초기값이나 출현 수가 같은 구간에서는 알리지 않고, 다음 출현 수가 바뀔 때만 알린다.
            if (CanSpawnMore && previousCount != nextCount)
                TutorialController.Instance?.Notify(TutorialEvent.EggSpawnCountChanged, eggSpawnCount: nextCount);
        }
    }

    public void Tick(float deltaTime)
    {
        if (Rules == null || Error != null || owner == null || owner.IsGameOver || StageIntroController.IsPlaying || deltaTime <= 0f) return;
        for (int i = 0; i < eggs.Count; i++)
        {
            EggMissionRules.EggState state = Rules.GetState(i);
            if ((state == EggMissionRules.EggState.Waiting || state == EggMissionRules.EggState.Hatching) &&
                (eggs[i] == null || !eggs[i].isActiveAndEnabled || eggs[i].Health == null || !eggs[i].Health.IsAlive))
            { Fail("알이 정상적인 파괴 처리 없이 제거되거나 비활성화되었습니다."); return; }
            if (state == EggMissionRules.EggState.Hatching && eggs[i].HatchPlaybackMissing)
            { Fail("부화 중인 알의 애니메이션 컴포넌트 또는 Animator가 제거되거나 비활성화되었습니다."); return; }
        }
        if (Rules.Complete) return;

        // 알마다 따로 흐르는 부화 시간을 줄이고, 다 된 알만 부화시킨다.
        Rules.Tick(deltaTime, expired);
        for (int i = 0; i < eggs.Count; i++)
            if (eggs[i] != null) eggs[i].SetUrgency(Rules.GetState(i) == EggMissionRules.EggState.Waiting ? Rules.GetProgress(i) : 0f);
        foreach (int index in expired)
        {
            if (Error != null) return;
            StartHatch(index);
        }
        if (Error == null) UpdateSpawning(deltaTime);
    }

    void UpdateSpawning(float deltaTime)
    {
        if (!CanSpawnMore) { retrySpawns = 0; return; }
        elapsed += deltaTime;
        while (elapsed >= nextSpawnTime)
        {
            nextSpawnTime += Settings.spawnInterval;
            // 지금까지 파괴한 알 수에 맞는 구간의 출현 수를 쓴다(구간이 없으면 기본 '한 번에 나오는 수').
            int count = Settings.GetEggsPerSpawn(Rules.DestroyedEggs);
            // 맵에 남은 알이 상한 이상이면 이번 차례는 건너뛰고 다음 간격을 기다린다.
            if (Settings.maxActiveEggs > 0)
                count = Mathf.Min(count, Settings.maxActiveEggs - Rules.WaitingEggs - retrySpawns);
            retrySpawns += Mathf.Max(0, count);
        }
        // 자리를 못 찾은 알은 다음 프레임에 다시 시도한다.
        int failures = 0;
        while (retrySpawns > 0 && CanSpawnMore && Error == null && failures == 0)
        {
            if (TrySpawnEgg()) retrySpawns--;
            else failures++;
        }
    }

    bool TrySpawnEgg()
    {
        if (!TryFindPlacement(out Vector3 position))
        {
            if (!placementWarned)
                Debug.LogWarning("알 미션: 알을 놓을 자리를 찾지 못했습니다. 거리 조건이나 맵 가장자리 여백을 줄이세요. 다음 프레임에 다시 시도합니다.", this);
            placementWarned = true;
            return false;
        }
        Quaternion rotation = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);
        MonsterEgg egg = Instantiate(Settings.eggPrefab, position, rotation, transform);
        string error = ValidateEgg(egg);
        if (error != null)
        {
            egg.gameObject.SetActive(false);
            Destroy(egg.gameObject);
            Fail("새로 놓은 알: " + error);
            return false;
        }
        SpawnedEggs++;
        Register(egg);
        EggSpawned?.Invoke(egg);
        return true;
    }

    bool TryFindPlacement(out Vector3 position)
    {
        position = Vector3.zero;
        if (!MapPlayBounds.TryResolve(MapPlayBoundsSource.Auto, Vector3.zero, Vector2.zero, out MapPlayBoundsData bounds))
            return false;
        float margin = Settings.mapEdgeMargin;
        float minX = bounds.Origin.x + margin, maxX = bounds.Origin.x + bounds.Width - margin;
        float minZ = bounds.Origin.z + margin, maxZ = bounds.Origin.z + bounds.Length - margin;
        if (maxX <= minX || maxZ <= minZ) return false;

        CollectHeadquarters();
        bool hasFallback = false;
        Vector3 fallback = Vector3.zero;
        int attempts = Settings.placementAttempts * (Settings.preferOutsideVision ? 2 : 1);
        for (int attempt = 0; attempt < attempts; attempt++)
        {
            float x = UnityEngine.Random.Range(minX, maxX);
            float z = UnityEngine.Random.Range(minZ, maxZ);
            if (!UnitSpawnUtility.TrySampleTopmostAtXZ(x, z, out Vector3 sampled) || !IsFreeSpot(sampled)) continue;
            // 시야 조건만 못 맞춘 자리는 예비로 둔다.
            if (Settings.preferOutsideVision && EnemySpawnUtility.IsVisibleToLocalPlayer(sampled))
            {
                if (!hasFallback) { fallback = sampled; hasFallback = true; }
                continue;
            }
            position = sampled;
            return true;
        }
        position = fallback;
        return hasFallback;
    }

    bool IsFreeSpot(Vector3 point)
    {
        foreach (Vector3 hq in hqPositions)
            if (HorizontalSqr(point, hq) < Settings.minDistanceFromHq * Settings.minDistanceFromHq) return false;
        float eggSqr = Settings.minDistanceBetweenEggs * Settings.minDistanceBetweenEggs;
        for (int i = 0; i < eggs.Count; i++)
            if (eggs[i] != null && Rules.GetState(i) != EggMissionRules.EggState.Hatched &&
                Rules.GetState(i) != EggMissionRules.EggState.Destroyed && HorizontalSqr(point, eggs[i].transform.position) < eggSqr)
                return false;
        float buildingSqr = Settings.minDistanceFromBuildings * Settings.minDistanceFromBuildings;
        foreach (SelectableEntity building in BuildingRegistry.Buildings)
            if (building != null && building.isActiveAndEnabled && HorizontalSqr(point, building.transform.position) < buildingSqr)
                return false;
        if (MapGrid.Instance != null && GridOccupancy.Instance != null &&
            GridOccupancy.Instance.IsOccupied(MapGrid.Instance.WorldToCell(point)))
            return false;
        return true;
    }

    void CollectHeadquarters()
    {
        hqPositions.Clear();
        foreach (SelectableEntity building in BuildingRegistry.Buildings)
            if (building != null && building.ownerId == owner.playerOwnerId && building.GetComponent<Headquarters>() != null)
                hqPositions.Add(building.transform.position);
    }

    static float HorizontalSqr(Vector3 a, Vector3 b)
    {
        float x = a.x - b.x, z = a.z - b.z;
        return x * x + z * z;
    }

    void StartHatch(int index)
    {
        MonsterEgg hatchingEgg = eggs[index];
        if (!Rules.BeginHatch(index)) return;
        hatchingEgg.SetNextHatch(false);
        hatchingEgg.SetUrgency(0f);
        // 부화한 몬스터는 일반 적으로 남으며 승리 조건에는 들어가지 않는다.
        hatchingEgg.BeginHatch(health => Rules.ResolveEgg(index, true), Fail,
            () => this != null && isActiveAndEnabled && Error == null && owner != null &&
            !owner.IsGameOver && !StageIntroController.IsPlaying && Time.timeScale > 0f);
    }

    void Fail(string message)
    {
        Error = message;
        Debug.LogError("알 미션: " + message, this);
        foreach (MonsterEgg egg in eggs) if (egg != null) egg.SetNextHatch(false);
    }

    void OnDisable()
    {
        foreach (MonsterEgg egg in eggs) if (egg != null) egg.SetNextHatch(false);
    }

    void OnDestroy()
    {
        foreach (MonsterEgg egg in eggs) if (egg != null) egg.DestroyedByDamage -= HandleEggDestroyed;
    }

    // 선택 정보 패널 표시용: 알 하나의 부화 상태와 그 알만의 부화 진행률을 돌려준다.
    // 알마다 따로 부화하므로 order는 항상 1이다.
    public MonsterEgg.HatchStatus GetEggStatus(MonsterEgg egg, out float remainingSeconds, out float progress, out int order)
    {
        remainingSeconds = 0f;
        progress = 0f;
        order = 0;
        if (Error != null) return MonsterEgg.HatchStatus.Error;
        if (Rules == null) return MonsterEgg.HatchStatus.Preparing;
        int index = eggs.IndexOf(egg);
        if (index < 0) return MonsterEgg.HatchStatus.Preparing;
        EggMissionRules.EggState state = Rules.GetState(index);
        if (state == EggMissionRules.EggState.Hatching) { progress = 1f; return MonsterEgg.HatchStatus.Hatching; }
        if (state != EggMissionRules.EggState.Waiting) return MonsterEgg.HatchStatus.Resolved;
        order = 1;
        remainingSeconds = Rules.GetRemaining(index);
        progress = Rules.GetProgress(index);
        return MonsterEgg.HatchStatus.NextHatch;
    }

    public string ProgressText
    {
        get
        {
            if (Error != null) return "알 미션 설정을 확인하세요.";
            if (Rules == null) return "알 미션 준비 중";
            if (Complete) return $"알 {Rules.TargetDestroyCount}개 파괴 완료";
            string text = $"파괴한 알 {Rules.DestroyedEggs}/{Rules.TargetDestroyCount} · 맵의 알 {Rules.WaitingEggs}개";
            float soonest = Rules.SoonestRemaining();
            if (soonest >= 0f) text += " · 가장 빠른 부화 " + FormatTime(soonest);
            float next = NextSpawnRemaining;
            string spawn = next >= 0f ? "다음 알 출현 " + FormatTime(next) : "더 나올 알 없음";
            return text + "\n" + spawn;
        }
    }

    static string FormatTime(float seconds)
    {
        int s = Mathf.CeilToInt(seconds);
        return $"{s / 60:00}:{s % 60:00}";
    }
}
