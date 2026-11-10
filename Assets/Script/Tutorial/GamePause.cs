using System.Collections.Generic;
using UnityEngine;

// 각 화면이 자신의 정지만 해제하며 마지막 화면이 닫힐 때 원래 배속을 복원한다.
public static class GamePause
{
    static readonly HashSet<object> owners = new HashSet<object>();
    static float previousScale = 1f;
    public static bool IsPaused => owners.Count > 0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Initialize() { owners.Clear(); previousScale = 1f; }

    public static void Acquire(object owner)
    {
        if (owner == null || owners.Contains(owner)) return;
        if (owners.Count == 0) previousScale = Time.timeScale;
        owners.Add(owner);
        Time.timeScale = 0f;
    }

    public static void Release(object owner)
    {
        if (owner == null || !owners.Remove(owner)) return;
        if (owners.Count == 0) Time.timeScale = previousScale;
    }

    public static void ResetForScene()
    {
        owners.Clear();
        previousScale = 1f;
        Time.timeScale = 1f;
    }
}
