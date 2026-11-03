using UnityEngine;

/// <summary>A piece of confetti in the colours of Albania (red, black, gold and white) that flies up, spins and falls.</summary>
public class Confetti : MonoBehaviour
{
    Vector3 _v, _spin, _euler;
    float _life = 2.8f;

    static readonly Color[] Colors = { Theme.Red, Theme.Gold, Color.white, Theme.Black, Theme.Red, Theme.Gold };

    public static void Burst(Transform parent, Vector3 pos, int count, System.Random rng)
    {
        for (int i = 0; i < count; i++)
        {
            GameObject c = Prim.Make(PrimitiveType.Cube, parent, Vector3.zero, new Vector3(0.10f, 0.015f, 0.065f), Mats.Unlit(Colors[i % Colors.Length]), "Confetti");
            c.transform.position = pos;
            Renderer r = c.GetComponent<Renderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            var cf = c.AddComponent<Confetti>();
            cf._v = new Vector3(R(rng, -1.8f, 1.8f), R(rng, 2.6f, 5.0f), R(rng, -1.4f, 1.4f));
            cf._spin = new Vector3(R(rng, -400f, 400f), R(rng, -400f, 400f), R(rng, -400f, 400f));
            cf._life = 2.2f + (float)rng.NextDouble() * 1.2f;
        }
    }

    static float R(System.Random rng, float a, float b) { return a + (float)rng.NextDouble() * (b - a); }

    void Update()
    {
        float dt = Time.deltaTime;
        _life -= dt;
        _v = _v + Vector3.up * (-9f * dt);
        transform.position = transform.position + _v * dt;
        _euler = _euler + _spin * dt;
        transform.rotation = Quaternion.Euler(_euler.x, _euler.y, _euler.z);
        if (_life <= 0f) Destroy(gameObject);
    }
}
