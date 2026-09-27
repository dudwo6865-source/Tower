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

    [Header("텍스트 연결")]
    [Label("생존 조건 텍스트")]
    [Tooltip("'N번째 밤까지 생존' 조건을 표시할 텍스트입니다. 비워두면 표시하지 않습니다.")]
    public TMP_Text survivalText;

    [Label("스포너 조건 텍스트")]
    [Tooltip("'적 스포너 모두 파괴' 조건을 표시할 텍스트입니다. 비워두면 표시하지 않습니다.")]
    public TMP_Text spawnerText;

    [Header("숨기기")]
    [Label("꺼진 조건 숨기기")]
    [Tooltip("켜면 사용하지 않는 승리 조건의 텍스트(또는 아래 묶음 오브젝트)를 숨깁니다.")]
    public bool hideDisabledConditions = true;

    [Label("생존 조건 묶음")]
    [Tooltip("생존 조건이 꺼져 있을 때 함께 숨길 오브젝트입니다(아이콘·배경 등을 묶은 부모). 비워두면 텍스트만 숨깁니다.")]
    public GameObject survivalRoot;

    [Label("스포너 조건 묶음")]
    [Tooltip("스포너 조건이 꺼져 있을 때 함께 숨길 오브젝트입니다(아이콘·배경 등을 묶은 부모). 비워두면 텍스트만 숨깁니다.")]
    public GameObject spawnerRoot;

    [Label("둘 다 꺼졌을 때 숨길 오브젝트")]
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

        Refresh();
    }

    void Update()
    {
        if (Time.unscaledTime < nextRefreshTime)
            return;

        nextRefreshTime = Time.unscaledTime + Mathf.Max(0f, refreshInterval);
        Refresh();
    }

    // 승리 조건 텍스트를 즉시 다시 그립니다.
    public void Refresh()
    {
        if (resultManager == null)
            return;

        // 게임이 끝난 뒤에는 마지막 표시를 그대로 둔다.
        if (resultManager.IsGameOver)
            return;

        bool survivalOn = resultManager.winBySurvivingNights;
        bool spawnerOn = resultManager.winWhenAllSpawnersDestroyed;

        if (hideDisabledConditions)
        {
            SetVisible(survivalRoot, survivalText, survivalOn);
            SetVisible(spawnerRoot, spawnerText, spawnerOn);

            if (panelRoot != null && panelRoot.activeSelf != (survivalOn || spawnerOn))
                panelRoot.SetActive(survivalOn || spawnerOn);
        }

        if (survivalOn && survivalText != null)
        {
            int target = resultManager.survivalNightsToWin;
            int survived = dayNightCycle != null ? Mathf.Min(dayNightCycle.CycleCount, target) : 0;
            survivalText.text = string.Format(survivalFormat, target, survived, Mathf.Max(0, target - survived));
        }

        if (spawnerOn && spawnerText != null)
        {
            int alive = GameResultManager.CountAliveSpawners();
            int total = Mathf.Max(resultManager.MaxSpawnersSeen, alive);
            spawnerText.text = string.Format(spawnerFormat, total - alive, total, alive);
        }
    }

    static void SetVisible(GameObject root, TMP_Text text, bool visible)
    {
        GameObject target = root != null ? root : (text != null ? text.gameObject : null);

        if (target != null && target.activeSelf != visible)
            target.SetActive(visible);
    }
}
