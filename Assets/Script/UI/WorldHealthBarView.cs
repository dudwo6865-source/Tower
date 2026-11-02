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
    [Tooltip("선택 항목입니다. 기존 발밑 위치 추적을 사용할 때만 이 프리팹 안의 기준점을 연결합니다. 비워 두면 에디터에서 정한 위치를 사용합니다.")]
    public Transform followAnchor;

    [Header("회전")]
    [Label("유닛 회전 무시")]
    [Tooltip("켜면 프리팹에 설정한 로컬 회전을 월드 기준 각도로 유지합니다. 유닛이 돌아도 체력바 방향은 바뀌지 않습니다. 끄면 부모의 회전을 따릅니다.")]
    public bool ignoreUnitRotation = true;

    private Quaternion authoredRotation;
    private bool rotationLocked;

    void Awake()
    {
        // 스폰 당시 유닛의 방향을 포함하지 않고 프리팹에 지정한 각도만 기억한다.
        authoredRotation = transform.localRotation;
    }

    void OnEnable()
    {
        UpdateRotation();
    }

    void LateUpdate()
    {
        UpdateRotation();
    }

    void OnDisable()
    {
        RestoreLocalRotation();
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
