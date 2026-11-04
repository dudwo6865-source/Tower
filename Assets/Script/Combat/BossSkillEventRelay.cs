using UnityEngine;

[DisallowMultipleComponent]
public class BossSkillEventRelay : MonoBehaviour
{
    [Label("보스 스킬")]
    public BossSkillController target;

    public void OnBossSkillHit(AnimationEvent animationEvent)
    {
        if (target == null) target = GetComponentInParent<BossSkillController>();
        if (target != null) target.OnSkillHit(animationEvent);
    }

    public void 보스스킬타격(AnimationEvent animationEvent)
    {
        OnBossSkillHit(animationEvent);
    }
}
