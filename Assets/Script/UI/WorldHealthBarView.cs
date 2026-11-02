using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 공용 체력바 프리팹의 연결 정보를 한곳에 보관한다.
[DisallowMultipleComponent]
public class WorldHealthBarView : MonoBehaviour
{
    [Header("공용 체력바 연결")]
    [Label("채움 이미지")]
    [Tooltip("Filled 방식의 체력 채움 Image를 연결합니다.")]
    public Image fillImage;

    [Label("표시 그룹")]
    [Tooltip("배경·채움·테두리·숫자를 감싸는 CanvasGroup을 연결합니다.")]
    public CanvasGroup displayGroup;

    [Label("체력 숫자")]
    [Tooltip("선택 항목입니다. 보스용 현재 체력 / 최대 체력 텍스트를 연결합니다.")]
    public TMP_Text healthText;

    [Label("위치 추적 기준점")]
    [Tooltip("콜라이더 아래 자동 배치에서는 연결할 필요가 없습니다. 자동 배치를 끄고 수동 발밑 위치 추적을 사용할 때만 연결합니다.")]
    public Transform followAnchor;

    [Header("회전")]
    [Label("유닛 회전 무시")]
    [Tooltip("켜면 프리팹에 설정한 로컬 회전을 월드 기준 각도로 유지합니다. 유닛이 돌아도 체력바 방향은 바뀌지 않습니다. 끄면 부모의 회전을 따릅니다.")]
    public bool ignoreUnitRotation = true;

    [Header("크기")]
    [Label("유닛 크기 무시")]
    [Tooltip("켜면 부모 유닛의 스케일을 보정하여 체력바 프리팹에 설정한 크기를 유지합니다. 체력바 크기는 공용 프리팹 루트의 Scale과 각 UI의 크기로 조절합니다.")]
    public bool ignoreUnitScale = true;

    private Quaternion authoredRotation;
    private Vector3 authoredScale;
    private bool rotationLocked;
    private bool scaleLocked;
    private Transform originalParent;
    private WorldHealthBar owner;
    private Vector3 authoredPosition;
    private Vector2 authoredSizeDelta;
    private bool detached;
    private bool parentWasActive = true;

    public bool IsDetached => detached;

    void Awake()
    {
        // 스폰 당시 유닛의 방향을 포함하지 않고 프리팹에 지정한 각도만 기억한다.
        authoredRotation = transform.localRotation;
        authoredScale = transform.localScale;
        authoredPosition = transform.localPosition;
        originalParent = transform.parent;
        owner = GetComponentInParent<WorldHealthBar>(true);
    }

    void Start()
    {
        UpdateTransform();
        if (owner != null)
            owner.UpdateAutomaticPosition(transform);
    }

    void OnEnable()
    {
        if (!detached)
        {
            UpdateRotation();
            UpdateScale();
        }
    }

    void LateUpdate()
    {
        UpdateTransform();
        if (owner != null)
            owner.UpdateAutomaticPosition(transform);
    }

    void OnDisable()
    {
        if (detached)
            return;
        RestoreLocalRotation();
        RestoreLocalScale();
    }

    void UpdateTransform()
    {
        if (detached && (owner == null || originalParent == null))
        {
            Destroy(gameObject);
            return;
        }

        if (ignoreUnitRotation && ignoreUnitScale && owner != null && originalParent != null)
        {
            if (!detached && owner.RegisterDetachedView(this))
            {
                // 기존 UI만 분리한다. 유닛 회전·스케일이 렌더링 직전에 바뀌어도 상속되지 않는다.
                RectTransform rect = transform as RectTransform;
                Vector2 size = rect != null ? rect.rect.size : Vector2.zero;
                if (rect != null)
                    authoredSizeDelta = rect.sizeDelta;
                transform.SetParent(null, true);
                if (rect != null)
                    rect.sizeDelta = size;
                transform.rotation = authoredRotation;
                transform.localScale = authoredScale;
                rotationLocked = false;
                scaleLocked = false;
                detached = true;
            }
            if (detached)
            {
                // 위치 오프셋도 유닛이 회전할 때 함께 돌지 않도록 월드 기준으로 유지한다.
                if (!owner.autoPositionBelowCollider && owner.followAnchor == null)
                    transform.position = originalParent.position + authoredPosition;
                bool parentActive = originalParent.gameObject.activeInHierarchy;
                if (!parentActive && displayGroup != null)
                    displayGroup.alpha = 0f;
                else if (!parentWasActive)
                    owner.RefreshVisibility();
                parentWasActive = parentActive;
                return;
            }
        }
        if (detached)
        {
            // 옵션을 끄면 원래 계층과 에디터 배치값을 복구한다.
            transform.SetParent(originalParent, false);
            if (transform is RectTransform rect)
                rect.sizeDelta = authoredSizeDelta;
            transform.localPosition = authoredPosition;
            transform.localRotation = authoredRotation;
            transform.localScale = authoredScale;
            detached = false;
        }
        UpdateRotation();
        UpdateScale();
    }

    void UpdateRotation()
    {
        if (ignoreUnitRotation)
        {
            transform.rotation = authoredRotation;
            rotationLocked = true;
        }
        else
            RestoreLocalRotation();
    }

    void RestoreLocalRotation()
    {
        if (!rotationLocked)
            return;
        transform.localRotation = authoredRotation;
        rotationLocked = false;
    }

    void UpdateScale()
    {
        if (!ignoreUnitScale)
        {
            RestoreLocalScale();
            return;
        }

        Transform parent = transform.parent;
        if (parent == null)
            transform.localScale = authoredScale;
        else
        {
            // 회전된 캔버스의 각 축에 실제로 적용되는 부모 배율을 구한다.
            // 부모의 축별 스케일을 그대로 나누면 바닥에 눕힌 체력바의 높이와 깊이가 뒤바뀐다.
            Quaternion localRotation = transform.localRotation;
            Vector3 inheritedScale = new Vector3(
                parent.TransformVector(localRotation * Vector3.right).magnitude,
                parent.TransformVector(localRotation * Vector3.up).magnitude,
                parent.TransformVector(localRotation * Vector3.forward).magnitude);
            transform.localScale = new Vector3(
                CompensateScale(authoredScale.x, inheritedScale.x),
                CompensateScale(authoredScale.y, inheritedScale.y),
                CompensateScale(authoredScale.z, inheritedScale.z));
        }
        scaleLocked = true;
    }

    static float CompensateScale(float authored, float inherited)
    {
        // 부모 스케일이 0인 동안은 나누지 않고 복구될 때 다시 보정한다.
        return Mathf.Approximately(inherited, 0f) ? authored : authored / inherited;
    }

    void RestoreLocalScale()
    {
        if (!scaleLocked)
            return;
        transform.localScale = authoredScale;
        scaleLocked = false;
    }

    void Reset()
    {
        // 후보가 하나뿐일 때만 연결하고 디자인 설정은 변경하지 않는다.
        Image candidate = null;
        int count = 0;
        foreach (Image image in GetComponentsInChildren<Image>(true))
        {
            if (image.type != Image.Type.Filled)
                continue;
            candidate = image;
            count++;
        }
        if (count == 1)
            fillImage = candidate;

        displayGroup = GetComponent<CanvasGroup>();
        if (displayGroup == null)
        {
            CanvasGroup[] groups = GetComponentsInChildren<CanvasGroup>(true);
            if (groups.Length == 1)
                displayGroup = groups[0];
        }

        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
        if (texts.Length == 1)
            healthText = texts[0];
    }

    public bool TryValidate(out string reason)
    {
        reason = null;
        if (fillImage == null || displayGroup == null)
            reason = "공용 체력바의 채움 이미지와 표시 그룹을 연결해 주세요.";
        else if (!fillImage.transform.IsChildOf(transform)
            || !displayGroup.transform.IsChildOf(transform)
            || (healthText != null && !healthText.transform.IsChildOf(transform))
            || (followAnchor != null && !followAnchor.IsChildOf(transform)))
            reason = "연결 대상은 모두 공용 체력바 프리팹 안에 있어야 합니다.";
        else if (!fillImage.transform.IsChildOf(displayGroup.transform)
            || (healthText != null && !healthText.transform.IsChildOf(displayGroup.transform)))
            reason = "채움 이미지와 체력 숫자를 표시 그룹 아래에 배치해 주세요.";
        else if (fillImage.type != Image.Type.Filled
            || fillImage.fillMethod != Image.FillMethod.Horizontal
            || fillImage.fillOrigin != (int)Image.OriginHorizontal.Left)
            reason = "채움 Image를 Filled / Horizontal / Left로 설정해 주세요.";
        else if (displayGroup.alpha <= 0f)
            reason = "표시 그룹의 Alpha를 0보다 크게 설정해 주세요. 보통 1을 사용합니다.";
        else
        {
            Canvas canvas = fillImage.GetComponentInParent<Canvas>(true);
            if (canvas == null || !canvas.transform.IsChildOf(transform)
                || canvas.rootCanvas.renderMode != RenderMode.WorldSpace)
                reason = "공용 체력바 안에 World Space Canvas가 필요합니다.";
        }
        return reason == null;
    }
}
