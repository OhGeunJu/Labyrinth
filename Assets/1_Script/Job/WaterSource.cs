using System.Collections.Generic;
using UnityEngine;

public class WaterSource : MonoBehaviour
{
    public static readonly List<WaterSource> All = new();

    void OnEnable()  => All.Add(this);
    void OnDisable() => All.Remove(this);

    public void Init(BuildingData data) { }

    public static WaterSource FindNearest(Vector3 position)
    {
        WaterSource nearest = null;
        float minDist = float.MaxValue;
        foreach (var ws in All)
        {
            float d = Vector3.Distance(position, ws.transform.position);
            if (d < minDist) { minDist = d; nearest = ws; }
        }
        return nearest;
    }
}
