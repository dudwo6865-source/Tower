using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 건물 설치 직후 기능 잠금과 Place 애니메이션을 따로 제어합니다.
/// - 기능 잠금: 설정한 시간 동안 명령/생산/공격만 막고, 선택은 가능합니다.
/// - 애니는 placeAnimationTrigger만 쏘며, 잠금 시간과 무관합니다.
/// </summary>
[DisallowMultipleComponent]
public class BuildingConstructionGate : MonoBehaviour
{
    [Header("Feature Lock")]
    [Tooltip("설치 후 명령/생산/공격이 막히는 시간(초)입니다. 0이면 잠금 없음.")]
    public float featureLockDuration = 2f;

    [Header("Place Animation")]
    [Tooltip("비워두면 자식 포함 Animator를 자동으로 찾습니다.")]
    public Animator animator;

    [Tooltip("설치 시 재생할 Animator 트리거 이름입니다. 비우면 애니는 생략합니다.")]
    public string placeAnimationTrigger = "Place";

    [Header("Place Shader FX")]
    [Tooltip("비워두면 자식에서 찾거나 없으면 자동으로 추가합니다.")]
    public BuildingPlacementDissolveFX dissolveFX;

    public bool IsFeatureLocked { get; private set; }

    public event Action OnFeatureUnlocked;

    Coroutine lockRoutine;

    void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);

        if (dissolveFX == null)
            dissolveFX = GetComponentInChildren<BuildingPlacementDissolveFX>(true);

        if (dissolveFX == null)
            dissolveFX = gameObject.AddComponent<BuildingPlacementDissolveFX>();
    }

    void OnDestroy()
    {
        if (lockRoutine != null)
            StopCoroutine(lockRoutine);
    }

    public void BeginAfterPlacement()
    {
        PlayPlaceAnimation();
        dissolveFX?.Play();
        BeginFeatureLock();
    }

    public void PlayPlaceAnimation()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);

        if (animator == null || string.IsNullOrEmpty(placeAnimationTrigger))
            return;

        animator.SetTrigger(placeAnimationTrigger);
    }

    public void BeginFeatureLock()
    {
        if (lockRoutine != null)
            StopCoroutine(lockRoutine);

        if (featureLockDuration <= 0f)
        {
            if (IsFeatureLocked)
                UnlockFeatures();
            return;
        }

        IsFeatureLocked = true;
        lockRoutine = StartCoroutine(FeatureLockRoutine());
    }

    IEnumerator FeatureLockRoutine()
    {
        yield return new WaitForSeconds(featureLockDuration);
        UnlockFeatures();
        lockRoutine = null;
    }

    void UnlockFeatures()
    {
        if (!IsFeatureLocked)
            return;

        IsFeatureLocked = false;
        OnFeatureUnlocked?.Invoke();
    }

    public static bool IsFeatureLockedOn(Component source)
    {
        if (source == null)
            return false;

        return IsFeatureLockedOn(source.gameObject);
    }

    public static bool IsFeatureLockedOn(GameObject source)
    {
        if (source == null)
            return false;

        BuildingConstructionGate gate = source.GetComponent<BuildingConstructionGate>();
        return gate != null && gate.IsFeatureLocked;
    }
}
