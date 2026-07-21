using UnityEngine;

public class WorldHealthBarRoot : MonoBehaviour
{
    static WorldHealthBarRoot instance;

    public static Transform Transform
    {
        get
        {
            EnsureRoot();
            return instance.transform;
        }
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
    }

    void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    static void EnsureRoot()
    {
        if (instance != null)
            return;

        GameObject rootObject = new GameObject("WorldHealthBarRoot");
        rootObject.AddComponent<WorldHealthBarRoot>();
    }
}
