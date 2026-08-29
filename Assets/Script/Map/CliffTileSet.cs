using UnityEngine;

// 절벽(고지대) 모듈 프리팹 세트입니다.
// 언덕은 별도 HillTileSet을 사용합니다.
[CreateAssetMenu(fileName = "CliffTileSet", menuName = "Tank/Cliff Tile Set")]
public class CliffTileSet : ScriptableObject
{
    [Header("Center")]
    [Tooltip("평평한 고지대 상판입니다. (Paint Top)")]
    public GameObject top;

    [Header("Ground")]
    [Tooltip("바닥(저지대) 타일 프리팹입니다. (Paint Ground)")]
    public GameObject ground;

    [Tooltip("바닥 타일 추가 Y 오프셋입니다.")]
    public float groundHeightOffset = 0f;

    [Tooltip("바닥 타일 Y 회전 오프셋입니다.")]
    public float groundYawOffset = 0f;

    [Header("Edge Prefabs")]
    [Tooltip("직선 절벽면입니다. 기본 방향: 로컬 +Z(북쪽).")]
    public GameObject straight;

    [Tooltip("볼록 모서리(바깥 코너)입니다.")]
    public GameObject outerCorner;

    [Tooltip("오목 모서리(안쪽 코너)입니다.")]
    public GameObject innerCorner;

    [Header("Ramp")]
    [Tooltip("램프입니다. 기본 방향: 로컬 +Z(북쪽)로 올라감.")]
    public GameObject ramp;

    [Header("Grid")]
    [Tooltip("한 타일의 월드 크기(미터)입니다. 기존 맵 모듈(스케일 2)은 보통 8입니다.")]
    public float tileSize = 8f;

    [Tooltip("고지대(Top)와 가장자리 모듈 사이의 높이 차이입니다.")]
    public float cliffHeight = 4.8f;

    [Tooltip("윗층마다 추가로 올릴 높이입니다. 0이고 cliffHeight도 0이면 |topOffset-edgeOffset|을 씁니다.")]
    public float layerStepHeight = 0f;

    [Tooltip("Top 상판의 추가 Y 오프셋입니다.")]
    public float topHeightOffset = 0f;

    [Tooltip("가장자리 모듈의 추가 Y 오프셋입니다.")]
    public float edgeHeightOffset = 0f;

    [Header("Rotation Offsets (degrees Y)")]
    public float straightYawOffset;
    public float outerCornerYawOffset;
    public float innerCornerYawOffset;
    public float rampYawOffset;
}
