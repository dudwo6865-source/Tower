using UnityEngine;
using UnityEngine.UI;

// 에디터에서 연결한 슬라이더에 점령 진행을 표시한다.
[DefaultExecutionOrder(210)]
public class CaptureProgressUI : MonoBehaviour
{
    [Label("점령 진행 슬라이더")]
    [Tooltip("표시 전용 Slider를 연결합니다. 크기·색·채우기 영역은 에디터에서 설정합니다.")]
    public Slider progressSlider;
    [Label("거점 위치 따라가기")]
    [Tooltip("켜면 거점 위를 따라갑니다. 끄면 에디터에서 배치한 고정 위치에 표시합니다.")]
    public bool followCapturePoint = true;
    [Label("거점 기준 위치 차이")]
    public Vector3 worldOffset = new Vector3(0f, 3f, 0f);
    [Label("월드 카메라")]
    [Tooltip("비워두면 메인 카메라를 사용합니다.")]
    public Camera worldCamera;

    Canvas targetCanvas;

    void Start()
    {
        if (progressSlider == null)
            Debug.LogWarning("CaptureProgressUI: 점령 진행 슬라이더를 연결하세요.", this);
        else if (transform.IsChildOf(progressSlider.transform))
        {
            Debug.LogError("CaptureProgressUI: 슬라이더 자신이나 자식이 아닌 별도 오브젝트에 컴포넌트를 붙이세요.", this);
            enabled = false;
        }
    }

    void LateUpdate()
    {
        if (progressSlider == null) return;
        GameResultManager result = GameResultManager.Instance;
        CapturePoint point = result != null ? result.capturePoint : null;
        bool visible = result != null && result.winByCapturingPoint && !result.IsGameOver &&
            string.IsNullOrEmpty(result.CaptureError) && point != null && point.isActiveAndEnabled &&
            point.State != CapturePoint.CaptureState.Waiting;

        float progress = visible && result.captureDuration > 0f
            ? Mathf.Clamp01(point.Elapsed / result.captureDuration) : 0f;
        progressSlider.SetValueWithoutNotify(Mathf.Lerp(progressSlider.minValue, progressSlider.maxValue, progress));

        if (visible && followCapturePoint)
        {
            if (worldCamera == null) worldCamera = Camera.main;
            if (targetCanvas == null) targetCanvas = progressSlider.GetComponentInParent<Canvas>(true);
            RectTransform rect = progressSlider.transform as RectTransform;
            RectTransform parent = rect.parent as RectTransform;
            if (worldCamera == null || targetCanvas == null || parent == null)
                visible = false;
            else
            {
                Vector3 screen = worldCamera.WorldToScreenPoint(point.transform.position + worldOffset);
                visible = screen.z > 0f && worldCamera.pixelRect.Contains(new Vector2(screen.x, screen.y));
                Camera uiCamera = targetCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : targetCanvas.worldCamera;
                if (visible && RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen, uiCamera, out Vector2 local))
                    rect.localPosition = new Vector3(local.x, local.y, 0f);
                else visible = false;
            }
        }
        progressSlider.gameObject.SetActive(visible);
    }

    void OnDisable()
    {
        if (progressSlider != null && !transform.IsChildOf(progressSlider.transform))
            progressSlider.gameObject.SetActive(false);
    }
}
