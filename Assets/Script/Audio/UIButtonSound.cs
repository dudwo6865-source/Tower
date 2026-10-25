using UnityEngine;

/// <summary>
/// 이 버튼만 UISoundManager의 기본 클릭음 대신 다른 소리를 내거나 소리를 끕니다.
/// Button/Toggle 컴포넌트와 같은 오브젝트에 붙입니다. 붙이지 않은 버튼은 기본 클릭음을 냅니다.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("UI/UI Button Sound")]
public class UIButtonSound : MonoBehaviour
{
    [Label("소리 끄기")]
    [Tooltip("켜면 이 버튼의 클릭음과 하이라이트음을 모두 끕니다.")]
    public bool mute;

    [Label("클릭음")]
    [Tooltip("이 버튼을 클릭했을 때 낼 소리입니다. 비워두면 UISoundManager의 기본 클릭음을 씁니다.")]
    public AudioClip clickClip;

    [Label("하이라이트음")]
    [Tooltip("비워두면 UISoundManager의 기본 버튼 하이라이트음을 사용합니다.")]
    public AudioClip highlightClip;

    [Label("하이라이트음 끄기")]
    [Tooltip("클릭음은 유지하고 이 버튼의 하이라이트음만 끕니다.")]
    public bool muteHighlight;

    [Label("잠긴 상태 클릭음")]
    [Tooltip("누를 수 없는 상태에서 클릭했을 때 낼 소리입니다. 비워두면 UISoundManager의 잠긴 버튼 클릭음을 씁니다.")]
    public AudioClip disabledClip;

    [Label("볼륨 배율")]
    [Tooltip("기본 볼륨에 곱하는 값입니다.")]
    [Range(0f, 2f)]
    public float volumeScale = 1f;
}
