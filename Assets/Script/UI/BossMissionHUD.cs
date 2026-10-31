using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[AddComponentMenu("게임/UI/BossMissionHUD")]
public class BossMissionHUD : MonoBehaviour
{
    [Label("결과 매니저")]
    public GameResultManager resultManager;
    [Label("보스 패널")]
    [Tooltip("직접 만든 패널을 연결합니다. 이 컴포넌트가 붙은 오브젝트의 부모는 연결하지 마세요.")]
    public GameObject panel;
    [Label("보스 이름 텍스트")]
    public TMP_Text nameText;
    [Label("행동 상태 텍스트")]
    public TMP_Text stateText;
    [Label("체력 텍스트")]
    public TMP_Text healthText;
    [Label("체력 채움 이미지")]
    [Tooltip("에디터에서 Image Type을 Filled로 설정한 이미지를 연결합니다.")]
    public Image healthFill;
    [Label("둥지 버프 표시")]
    public GameObject buffIndicator;

    BossNest observedNest;
    bool discovered;
    float lastHealth;
    float lastMax;

    void LateUpdate()
    {
        if (resultManager == null) resultManager = GameResultManager.Instance;
        bool active = resultManager != null && resultManager.winByDefeatingBoss;
        if (panel != null && !transform.IsChildOf(panel.transform)) panel.SetActive(active);
        if (!active) return;
        BossNest nest = resultManager.bossNest;
        if (observedNest != nest) { observedNest = nest; discovered = false; }
        if (nameText != null) nameText.text = nest != null ? nest.displayName : "보스";
        BossCombatAI boss = nest != null ? nest.Boss : null;
        FogOfWarVisibility fog = boss != null ? boss.GetComponent<FogOfWarVisibility>() : null;
        bool visible = boss != null && (fog == null || fog.IsCurrentlyVisible);
        if (visible)
        {
            discovered = true;
            lastHealth = boss.Health.CurrentHealth;
            lastMax = boss.Health.MaxHealth;
        }
        bool defeated = nest != null && nest.Defeated;
        if (defeated) lastHealth = 0f;
        if (healthText != null)
            healthText.text = defeated ? "처치 완료" : !discovered ? "보스를 발견하면 체력이 표시됩니다."
                : $"{lastHealth:0} / {lastMax:0}" + (visible ? "" : " · 마지막 확인");
        if (healthFill != null) healthFill.fillAmount = discovered && lastMax > 0f ? lastHealth / lastMax : 0f;
        if (buffIndicator != null) buffIndicator.SetActive(visible && !defeated && boss.IncomingDamageMultiplier < 1f);
        if (stateText != null)
            stateText.text = !string.IsNullOrEmpty(resultManager.BossError) || nest == null || !string.IsNullOrEmpty(nest.Error)
                ? "보스 둥지 설정을 확인하세요." : defeated ? "보스 처치 완료"
                : boss != null ? boss.StateLabel + $" · 첫 출격 {nest.firstAssaultNight}번째 밤"
                : "보스 준비 중";
    }
}
