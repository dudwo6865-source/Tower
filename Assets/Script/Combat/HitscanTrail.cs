using UnityEngine;

// 히트스캔 공격의 순간 빛줄기(트레일)입니다. 두 점을 잇고 짧게 페이드된 뒤 스스로 사라집니다.
[RequireComponent(typeof(LineRenderer))]
public class HitscanTrail : MonoBehaviour
{
    private LineRenderer line;
    private float duration;
    private float elapsed;
    private Color baseColor;

    public void Play(Vector3 start, Vector3 end, float lifeTime, Color color, float width)
    {
        line = GetComponent<LineRenderer>();
        duration = Mathf.Max(0.01f, lifeTime);
        elapsed = 0f;
        baseColor = color;

        line.positionCount = 2;
        line.SetPosition(0, start);
        line.SetPosition(1, end);
        line.startWidth = width;
        line.endWidth = width;
        line.startColor = color;
        line.endColor = color;
    }

    void Update()
    {
        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / duration);

        Color faded = baseColor;
        faded.a = Mathf.Lerp(baseColor.a, 0f, t);
        line.startColor = faded;
        line.endColor = faded;

        if (t >= 1f)
            Destroy(gameObject);
    }
}
