using UnityEngine;

public static class DotsRuntimeBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (SpatialQueryWorld.Instance == null)
        {
            GameObject runtime = new GameObject("[DotsRuntime]");
            Object.DontDestroyOnLoad(runtime);
            runtime.AddComponent<SpatialQueryWorld>();
            runtime.AddComponent<ProjectileSimWorld>();
            runtime.AddComponent<AiPathBudgetSettings>();
            return;
        }

        if (AiPathBudgetSettings.Instance == null)
            SpatialQueryWorld.Instance.gameObject.AddComponent<AiPathBudgetSettings>();
    }
}
