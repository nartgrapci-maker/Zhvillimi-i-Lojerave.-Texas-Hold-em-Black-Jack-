using System.Collections.Generic;
using UnityEngine;

/// <summary>Soft puffs of smoke and steam: a few translucent spheres that drift up, grow and fade.</summary>
public static class Smoke
{
    static readonly List<GameObject> Live = new List<GameObject>();

    /// <summary>One puff at a world position. size is the starting diameter, life in seconds, grey 0..1 (steam is lighter).</summary>
    public static void Puff(Vector3 pos, Vector3 velocity, float size, float life, float grey = 0.85f)
    {
        for (int i = Live.Count - 1; i >= 0; i--) if (Live[i] == null) Live.RemoveAt(i);
        if (Live.Count > 90) return;                       // never flood the scene
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Collider col = go.GetComponent<Collider>();
        if (col != null) Object.Destroy(col);
        go.name = "Smoke";
        go.transform.position = pos;
        go.transform.localScale = Vector3.one * size;
        MeshKit.Smooth(go, PrimitiveType.Sphere);
        Renderer r = go.GetComponent<Renderer>();
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        SmokePuff sp = go.AddComponent<SmokePuff>();
        sp.Velocity = velocity;
        sp.Size = size;
        sp.Life = Mathf.Max(0.3f, life);
        sp.Grey = grey;
        Live.Add(go);
    }

    public static void Clear()
    {
        for (int i = 0; i < Live.Count; i++) if (Live[i] != null) Object.Destroy(Live[i]);
        Live.Clear();
    }
}

public class SmokePuff : MonoBehaviour
{
    public Vector3 Velocity;
    public float Size, Life, Grey;
    float _age;

    void Update()
    {
        _age += Time.deltaTime;
        float p = _age / Life;
        if (p >= 1f) { Destroy(gameObject); return; }
        Velocity.y += 0.06f * Time.deltaTime;                     // warm air rises
        Velocity *= Mathf.Clamp01(1f - 0.7f * Time.deltaTime);    // and slows down
        transform.position += Velocity * Time.deltaTime;
        transform.localScale = Vector3.one * Size * (0.6f + 1.9f * p);
        float fade = p < 0.15f ? p / 0.15f : 1f - (p - 0.15f) / 0.85f;
        int step = Mathf.Clamp(Mathf.RoundToInt(fade * 7f), 0, 7);
        float a = step / 7f * 0.34f;
        GetComponent<Renderer>().sharedMaterial = Mats.Unlit(new Color(Grey, Grey, Grey * 0.97f, a));
    }
}
