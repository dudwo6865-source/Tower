using UnityEngine;

// 히트스캔 공격의 순간 빛줄기(트레일)입니다. 위치(시작~끝점)는 매번 공격 지점에 맞춰
// 갱신하지만, 색상·두께는 프리팹이 지정된 경우 프리팹에 미리 만들어둔 그라디언트/
// 두께 커브(Width Curve)를 그대로 존중합니다(덮어쓰지 않음). 프리팹이 없을 때만
// UnitAttacker의 색상·두께 값으로 기본 라인을 만들고, 짧게 페이드하며 사라집니다.
[RequireComponent(typeof(LineRenderer))]
public class HitscanTrail : MonoBehaviour
{
    private LineRenderer line;
    private float duration;
    private float elapsed;
    private Color baseColor;
    private bool ownsColorAndWidth;

    public void Play(Vector3 start, Vector3 end, float lifeTime, Color color, float width, bool overrideColorAndWidth)
    {
        line = GetComponent<LineRenderer>();
        duration = Mathf.Max(0.01f, lifeTime);
        elapsed = 0f;
        ownsColorAndWidth = overrideColorAndWidth;

        line.positionCount = 2;
        line.SetPosition(0, start);
        line.SetPosition(1, end);

        if (!ownsColorAndWidth)
            return;

        baseColor = color;
        line.startWidth = width;
        line.endWidth = width;
        line.startColor = color;
        line.endColor = color;
    }

    void Update()
    {
        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / duration);

        // 프리팹이 직접 만든 그라디언트/두께 커브는 건드리지 않고, 지속 시간만 지키다 사라집니다.
        if (ownsColorAndWidth)
        {
            Color faded = baseColor;
            faded.a = Mathf.Lerp(baseColor.a, 0f, t);
            line.startColor = faded;
            line.endColor = faded;
        }

        if (t >= 1f)
            Destroy(gameObject);
    }
}
