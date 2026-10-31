using TMPro;
using UnityEngine;

// 게임 중 승리 조건과 진행 상황을 텍스트로 표시합니다.
// 조건 값(버틸 밤 수, 조건 사용 여부)은 GameResultManager에서 읽으므로
// MapLoader가 스테이지(MapConfig) 값으로 덮어쓴 경우에도 그 값이 그대로 표시됩니다.
[DisallowMultipleComponent]
public class VictoryConditionHUD : MonoBehaviour
{
    [Header("참조")]
    [Label("결과 매니저")]
    [Tooltip("비워두면 씬에서 GameResultManager를 자동으로 찾습니다.")]
    public GameResultManager resultManager;

    [Label("낮밤 사이클")]
    [Tooltip("비워두면 씬에서 DayNightCycle을 자동으로 찾습니다.")]
    public DayNightCycle dayNightCycle;

    [Header("조건 목록")]
    [Label("기준 텍스트")]
    [Tooltip("이 텍스트를 복제해 활성 승리 조건을 표시합니다. 씬의 원본은 숨깁니다. HUD 컴포넌트는 별도 오브젝트에 붙이세요.")]
    [UnityEngine.Serialization.FormerlySerializedAs("survivalText")]
    public TMP_Text conditionTemplate;

    [Label("조건 목록 부모")]
    [Tooltip("생성한 텍스트를 넣을 부모입니다. 비워두면 기준 텍스트의 부모를 사용합니다. 배치는 부모의 Layout Group에서 설정하세요.")]
    public Transform conditionContainer;

    // 이전 씬의 개별 텍스트가 중복 표시되지 않도록 참조만 보존한다.
    [SerializeField, HideInInspector] TMP_Text spawnerText;
    [SerializeField, HideInInspector] TMP_Text captureText;
    [SerializeField, HideInInspector] GameObject survivalRoot;
    [SerializeField, HideInInspector] GameObject spawnerRoot;
    [SerializeField, HideInInspector] GameObject captureRoot;

    [Label("모든 조건이 꺼졌을 때 숨길 오브젝트")]
    [Tooltip("승리 조건이 하나도 없을 때 숨길 오브젝트입니다(패널 전체 등). 비워두면 아무것도 숨기지 않습니다. 이 컴포넌트가 붙은 오브젝트는 넣지 마세요(꺼지면 다시 켜지지 않습니다).")]
    public GameObject panelRoot;

    [Header("문구")]
    [Label("생존 조건 형식")]
    [Tooltip("{0} = 버텨야 할 밤 수, {1} = 지금까지 버틴 밤 수, {2} = 남은 밤 수")]
    public string survivalFormat = "{0}번째 밤까지 생존 ({1}/{0})";

    [Label("스포너 조건 형식")]
    [Tooltip("{0} = 파괴한 스포너 수, {1} = 전체 스포너 수, {2} = 남은 스포너 수")]
    public string spawnerFormat = "적 스포너 모두 파괴 ({0}/{1})";

    [Header("갱신")]
    [Label("갱신 간격(초)")]
    [Tooltip("텍스트를 다시 계산하는 간격(실제 시간)입니다. 0이면 매 프레임 갱신합니다.")]
    public float refreshInterval = 0.25f;

    float nextRefreshTime;
    readonly TMP_Text[] rows = new TMP_Text[4];
    bool templateWasActive;
    bool initialized;
    bool layoutWarningShown;

    void Start()
    {
        if (resultManager == null)
            resultManager = GameResultManager.Instance;

        if (resultManager == null)
            resultManager = FindObjectOfType<GameResultManager>();

        if (dayNightCycle == null)
            dayNightCycle = DayNightCycle.Instance;

        if (dayNightCycle == null)
            dayNightCycle = FindObjectOfType<DayNightCycle>();

        if (resultManager == null)
            Debug.LogWarning("VictoryConditionHUD: 씬에서 GameResultManager를 찾지 못했습니다.");

        InitializeTemplate();
        Refresh();
    }

    void Update()
    {
        if (Time.unscaledTime < nextRefreshTime)
            return;

        nextRefreshTime = Time.unscaledTime + Mathf.Max(0f, refreshInterval);
        Refresh();
    }

    void InitializeTemplate()
    {
        if (conditionTemplate == null)
            conditionTemplate = spawnerText != null ? spawnerText : captureText;
        if (conditionTemplate == null)
        {
            Debug.LogWarning("VictoryConditionHUD: 기준 텍스트 하나를 연결하세요.", this);
            return;
        }
        if (conditionContainer == null) conditionContainer = conditionTemplate.transform.parent;
        if (conditionContainer == null || conditionContainer.IsChildOf(conditionTemplate.transform) ||
            conditionTemplate.GetComponentInChildren<VictoryConditionHUD>(true) != null)
        {
            Debug.LogError("VictoryConditionHUD: 목록 부모와 HUD는 기준 텍스트 밖에 배치해야 합니다. 프리팹 텍스트는 목록 부모도 연결하세요.", this);
            return;
        }

        templateWasActive = conditionTemplate.gameObject.activeSelf;
        // 에셋 원본은 수정하지 않고 씬에 있는 기준 텍스트만 숨긴다.
        if (conditionTemplate.gameObject.scene.IsValid()) conditionTemplate.gameObject.SetActive(false);
        HideLegacy(spawnerText != null ? spawnerText.gameObject : null);
        HideLegacy(captureText != null ? captureText.gameObject : null);
        HideLegacy(survivalRoot);
        HideLegacy(spawnerRoot);
        HideLegacy(captureRoot);
        initialized = true;
    }

    void HideLegacy(GameObject target)
    {
        if (target == null || conditionContainer.IsChildOf(target.transform) || transform.IsChildOf(target.transform)) return;
        if (target.scene.IsValid()) target.SetActive(false);
    }

    TMP_Text GetRow(int index, bool enabledCondition, string title)
    {
        if (enabledCondition && rows[index] == null)
        {
            rows[index] = Instantiate(conditionTemplate, conditionContainer, false);
            rows[index].name = title;
            // 늦게 켜진 조건도 생존 → 스포너 → 점령 순서를 유지한다.
            for (int i = 0; i < rows.Length; i++)
                if (rows[i] != null) rows[i].transform.SetAsLastSibling();
        }
        if (rows[index] != null) rows[index].gameObject.SetActive(enabledCondition);
        return rows[index];
    }

    void OnDestroy()
    {
        foreach (TMP_Text row in rows)
            if (row != null) Destroy(row.gameObject);
        if (initialized && conditionTemplate != null && conditionTemplate.gameObject.scene.IsValid())
            conditionTemplate.gameObject.SetActive(templateWasActive);
    }

    // 활성 조건만 생성하고 이미 만든 항목은 재사용한다.
    public void Refresh()
    {
        if (resultManager == null || !initialized) return;

        bool survivalOn = resultManager.winBySurvivingNights;
        bool spawnerOn = resultManager.winWhenAllSpawnersDestroyed;
        bool captureOn = resultManager.winByCapturingPoint;
        bool bossOn = resultManager.winByDefeatingBoss;
        TMP_Text survivalText = GetRow(0, survivalOn, "생존 조건");
        TMP_Text spawnerText = GetRow(1, spawnerOn, "스포너 조건");
        TMP_Text captureText = GetRow(2, captureOn, "점령 조건");
        TMP_Text bossText = GetRow(3, bossOn, "보스 처치 조건");
        if (bossText != null && bossOn)
        {
            BossNest nest = resultManager.bossNest;
            bossText.text = !string.IsNullOrEmpty(resultManager.BossError) || nest == null || !string.IsNullOrEmpty(nest.Error)
                ? "보스 목표 설정을 확인하세요."
                : nest.Defeated ? $"{nest.displayName} 처치 완료"
                : nest.Boss == null ? $"{nest.displayName} · 보스 준비 중"
                : $"{nest.displayName} 처치 · {nest.Boss.StateLabel}";
        }

        bool any = survivalOn || spawnerOn || captureOn || bossOn;
        // 자기 자신을 꺼서 조건 변경 후 다시 켜지지 않는 상황을 막는다.
        if (panelRoot != null && !transform.IsChildOf(panelRoot.transform))
            panelRoot.SetActive(any);

        int activeCount = (survivalOn ? 1 : 0) + (spawnerOn ? 1 : 0) + (captureOn ? 1 : 0) + (bossOn ? 1 : 0);
        if (activeCount > 1 && !layoutWarningShown && conditionContainer.GetComponent<UnityEngine.UI.LayoutGroup>() == null)
        {
            layoutWarningShown = true;
            Debug.LogWarning("VictoryConditionHUD: 조건이 겹치지 않도록 목록 부모에 Vertical Layout Group 등을 설정하세요.", this);
        }

        if (resultManager.IsGameOver)
        {
            if (captureText != null && captureOn && resultManager.CurrentResult == GameResultManager.Result.Victory &&
                resultManager.capturePoint != null && resultManager.capturePoint.State == CapturePoint.CaptureState.Complete)
                captureText.text = $"{resultManager.capturePoint.displayName} 점령 완료";
            return;
        }

        if (survivalOn && survivalText != null)
        {
            int target = resultManager.survivalNightsToWin;
            int survived = dayNightCycle != null ? Mathf.Min(dayNightCycle.CycleCount, target) : 0;
            survivalText.text = string.Format(survivalFormat, target, survived, Mathf.Max(0, target - survived));
        }

        if (spawnerOn && spawnerText != null)
        {
            // 아직 건설 전인 스포너 마커도 남은 수에 넣는다.
            int alive = GameResultManager.CountAliveSpawners();
            int remaining = alive + StageBuildingSpawnPoint.PendingSpawnerCount;
            int total = Mathf.Max(
                resultManager.MaxSpawnersSeen,
                resultManager.DestroyedSpawnerCount + remaining);
            spawnerText.text = string.Format(spawnerFormat, Mathf.Max(0, total - remaining), total, remaining);
        }

        if (captureOn && captureText != null)
        {
            CapturePoint point = resultManager.capturePoint;
            if (!string.IsNullOrEmpty(resultManager.CaptureError) || point == null)
                captureText.text = "점령 목표 설정을 확인하세요.";
            else
            {
                string progress = $"{point.Elapsed:0.0} / {resultManager.captureDuration:0.0}초";
                switch (point.State)
                {
                    case CapturePoint.CaptureState.Capturing:
                        captureText.text = $"{point.displayName} 점령 중 · {progress}";
                        break;
                    case CapturePoint.CaptureState.Contested:
                        captureText.text = $"{point.displayName} · 적 때문에 점령 중단 ({progress})";
                        break;
                    case CapturePoint.CaptureState.Complete:
                        captureText.text = $"{point.displayName} 점령 완료";
                        break;
                    default:
                        captureText.text = point.WasReset
                            ? $"{point.displayName} · 아군이 없어 점령 진행 초기화"
                            : $"{point.displayName} 점령 · 유닛을 구역 안으로 이동하세요.";
                        break;
                }
            }
        }
    }

}
