using UnityEngine;

// 인스펙터에 보이는 필드 이름만 바꾼다. 코드상 변수 이름과 직렬화 데이터는 그대로다.
// 사용 예: [Label("공격력")] public float attackDamage;
// 그리는 쪽은 Editor/LabelAttributeDrawer.cs 에 있다.
[System.AttributeUsage(System.AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
public class LabelAttribute : PropertyAttribute
{
    public readonly string text;

    public LabelAttribute(string text)
    {
        this.text = text;
    }
}
