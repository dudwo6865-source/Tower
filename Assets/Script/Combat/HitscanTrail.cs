using UnityEngine;

// 히트스캔 공격의 순간 빛줄기(트레일)입니다. 위치(시작~끝점)는 매번 공격 지점에 맞춰
// 갱신하지만, 색상·두께는 프리팹이 지정된 경우 프리팹에 미리 만들어둔 그라디언트/
// 두께 커브(Width Curve)를 그대로 존중합니다(덮어쓰지 않음). 프리팹이 없을 때만
// UnitAttacker의 색상·두께 값으로 기본 라인을 만들고, 짧게 페이드하며 사라집니다.
[RequireComponent(typeof(LineRenderer))]
public class HitscanTrail : MonoBehaviour
{
    static readonly int GradationId = Shader.PropertyToID("_Gradation");
    static readonly int DissolveId = Shader.PropertyToID("_Dissolve");

    private LineRenderer line;
    private float duration;
    private float elapsed;
    private Color baseColor;
    private bool ownsColorAndWidth;
    private MaterialPropertyBlock propertyBlock;

    void Awake()
    {
        propertyBlock = new MaterialPropertyBlock();
    }

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

        // Trail 셰이더의 Gradation 값을 라이프타임에 맞춰 1->0으로 내려, 노이즈 패턴이
        // 점점 걷혀 사라지는 디졸브 연출이 재생되게 합니다(프리팹 유무와 무관하게 항상 적용).
        // 피어싱 빔(Plasma_Trail) 셰이더는 대신 _Dissolve 값을 0(완전히 보임)에서
        // -1(완전히 디졸브되어 사라짐)로 보간해 같은 연출을 냅니다. 프로퍼티가 없는
        // 셰이더에서는 SetFloat이 조용히 무시되므로 두 값을 함께 설정해도 안전합니다.
        line.GetPropertyBlock(propertyBlock);
        propertyBlock.SetFloat(GradationId, 1f - t);
        propertyBlock.SetFloat(DissolveId, -t);
        line.SetPropertyBlock(propertyBlock);

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
