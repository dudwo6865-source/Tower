using System;
using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("게임/미션/EggMissionController")]
public class EggMissionController : MonoBehaviour
{
    // 미니맵에는 부화 대기 또는 연출 중인 알의 실제 타이머만 전달한다.
    public bool TryGetMinimapHatchTime(MonsterEgg egg, out float remaining, out bool hatching)
    {
        remaining = 0f;
        hatching = false;
        if (egg == null || Rules == null || Error != null || eggs == null) return false;
        int index = Array.IndexOf(eggs, egg);
        if (index < 0) return false;
        EggMissionRules.EggState state = Rules.GetState(index);
        hatching = state == EggMissionRules.EggState.Hatching;
        if (hatching) return true;
        if (state != EggMissionRules.EggState.Waiting || egg != NextEgg) return false;
        remaining = Rules.RemainingSeconds;
        return true;
    }

    public EggMissionRules Rules { get; private set; }
    public string Error { get; private set; }
    public MonsterEgg NextEgg { get; private set; }
    public bool Complete => Error == null && Rules != null && Rules.Complete;
    MonsterEgg[] eggs;
    EntityHealth[] monsters;
    Action[] monsterDeaths;
    GameResultManager owner;
    int transitionFrame = -1;

    public void Initialize(GameResultManager result, float[] durations)
    {
        if (Rules != null || Error != null) return;
        owner = result;
        eggs = GetComponentsInChildren<MonsterEgg>();
        string error = EggMissionRules.ValidateDurations(durations, eggs.Length);
        if (error != null) { Fail(error); return; }
        foreach (MonsterEgg egg in eggs)
        {
            error = egg.ValidateSetup();
            if (error == null && (!egg.isActiveAndEnabled || egg.Health == null || !egg.Health.IsAlive))
                error = "모든 알의 컴포넌트를 활성화하고 체력을 확인하세요.";
            if (error == null && egg.GetComponent<SelectableEntity>().ownerId == result.playerOwnerId)
                error = "알의 소유자 ID를 적 진영으로 설정하세요.";
            if (error != null) { Fail(egg.name + ": " + error); return; }
        }
        Rules = new EggMissionRules(eggs.Length, durations);
        monsters = new EntityHealth[eggs.Length];
        monsterDeaths = new Action[eggs.Length];
        foreach (MonsterEgg egg in eggs) egg.DestroyedByDamage += HandleEggDestroyed;
        UpdateNextEgg();
    }

    void HandleEggDestroyed(MonsterEgg egg)
    {
        if (owner == null || owner.IsGameOver || Error != null) return;
        int index = Array.IndexOf(eggs, egg);
        if (!Rules.ResolveEgg(index, false)) return;
        transitionFrame = Time.frameCount;
        UpdateNextEgg();
    }

    void UpdateNextEgg()
    {
        NextEgg = null;
        for (int i = 0; i < eggs.Length; i++)
            if (Rules.GetState(i) == EggMissionRules.EggState.Waiting && eggs[i] != null &&
                (NextEgg == null || eggs[i].hatchPriority < NextEgg.hatchPriority)) NextEgg = eggs[i];
        foreach (MonsterEgg egg in eggs)
            if (egg != null) egg.SetNextHatch(egg == NextEgg && Error == null);
    }

    public void Tick(float deltaTime)
    {
        if (Rules == null || Error != null || owner == null || owner.IsGameOver || StageIntroController.IsPlaying || deltaTime <= 0f) return;
        for (int i = 0; i < eggs.Length; i++)
        {
            if ((Rules.GetState(i) == EggMissionRules.EggState.Waiting || Rules.GetState(i) == EggMissionRules.EggState.Hatching) &&
                (eggs[i] == null || !eggs[i].isActiveAndEnabled || eggs[i].Health == null || !eggs[i].Health.IsAlive))
            { Fail("알이 정상적인 파괴 처리 없이 제거되거나 비활성화되었습니다."); return; }
            if (Rules.GetState(i) == EggMissionRules.EggState.Hatching && eggs[i].HatchPlaybackMissing)
            { Fail("부화 중인 알의 애니메이션 컴포넌트 또는 Animator가 제거되거나 비활성화되었습니다."); return; }
            if (Rules.GetState(i) == EggMissionRules.EggState.Hatched &&
                (monsters[i] == null || !monsters[i].isActiveAndEnabled))
            { Fail("부화 몬스터가 처치되지 않고 제거되거나 비활성화되었습니다."); return; }
        }
        float urgency = Rules.StageSeconds > 0f ? 1f - Rules.RemainingSeconds / Rules.StageSeconds : 0f;
        foreach (MonsterEgg egg in eggs)
            if (egg != null) egg.SetUrgency(egg == NextEgg ? urgency : 0f);
        // 같은 프레임의 여러 알 파괴는 각각 단계에 반영하되 새 타이머를 즉시 깎지 않는다.
        if (transitionFrame == Time.frameCount || !Rules.Tick(deltaTime, false)) return;
        if (NextEgg == null) { Fail("다음 부화 대상이 없습니다."); return; }
        int index = Array.IndexOf(eggs, NextEgg);
        MonsterEgg hatchingEgg = NextEgg;
        if (!Rules.BeginHatch(index)) return;
        transitionFrame = Time.frameCount;
        UpdateNextEgg();
        hatchingEgg.BeginHatch(health =>
        {
            if (!Rules.ResolveEgg(index, true)) return;
            monsters[index] = health;
            monsterDeaths[index] = () => Rules.DefeatMonster(index);
            health.OnDied += monsterDeaths[index];
        }, Fail, () => this != null && isActiveAndEnabled && Error == null && owner != null &&
            !owner.IsGameOver && !StageIntroController.IsPlaying && Time.timeScale > 0f);
    }

    void Fail(string message)
    {
        Error = message;
        Debug.LogError("알 미션: " + message, this);
        if (eggs != null)
            foreach (MonsterEgg egg in eggs) if (egg != null) egg.SetNextHatch(false);
    }

    void OnDisable()
    {
        if (eggs != null)
            foreach (MonsterEgg egg in eggs) if (egg != null) egg.SetNextHatch(false);
    }

    void OnDestroy()
    {
        if (eggs != null)
            foreach (MonsterEgg egg in eggs) if (egg != null) egg.DestroyedByDamage -= HandleEggDestroyed;
        if (monsters != null)
            for (int i = 0; i < monsters.Length; i++)
                if (monsters[i] != null) monsters[i].OnDied -= monsterDeaths[i];
    }

    public string ProgressText
    {
        get
        {
            if (Error != null) return "알 미션 설정을 확인하세요.";
            if (Rules == null) return "알 미션 준비 중";
            if (Complete) return "알과 부화 몬스터 제거 완료";
            if (Rules.RemainingEggs == 0) return $"부화 중 {Rules.HatchingEggs}개 · 남은 부화 몬스터 처치 {Rules.LivingMonsters}마리";
            int seconds = Mathf.CeilToInt(Rules.RemainingSeconds);
            string target = NextEgg != null ? NextEgg.displayName : "몬스터 알";
            return $"남은 알 {Rules.RemainingEggs}/{Rules.TotalEggs} · 다음 부화 {seconds / 60:00}:{seconds % 60:00}\n" +
                $"부화 대상: {target} · 부화 중 {Rules.HatchingEggs}개 · 부화 몬스터 {Rules.LivingMonsters}마리";
        }
    }
}
