using UnityEngine;

[DisallowMultipleComponent]
public class ProducedUnitMarker : MonoBehaviour
{
    ProductionBuilding source;
    bool released;

    public ProductionBuilding Source => source;

    public void Initialize(ProductionBuilding producer)
    {
        source = producer;
    }

    public void Release()
    {
        if (released)
            return;

        released = true;

        if (source != null)
            source.NotifyUnitReleased(this);

        source = null;
    }

    void OnDestroy()
    {
        Release();
    }
}
