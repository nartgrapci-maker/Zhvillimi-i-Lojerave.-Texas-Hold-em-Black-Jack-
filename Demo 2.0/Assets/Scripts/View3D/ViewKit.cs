using System.Collections.Generic;
using UnityEngine;
using PokerDemo.Core;

/// <summary>Materials that work in both the Built-in and the Universal render pipeline (the shader is picked at runtime).</summary>
public static class Mats
{
    /// <summary>Multiplier for all world-space text. If text looks too big or too small in your Unity version, change this one number.</summary>
    public static float TextScale = 1f;

    static readonly Dictionary<long, Material> _lit = new Dictionary<long, Material>();
    static readonly Dictionary<long, Material> _unlit = new Dictionary<long, Material>();
    static Shader _litShader, _unlitShader;
    static Font _font;

    static Shader FindFirst(params string[] names)
    {
        for (int i = 0; i < names.Length; i++)
        {
            Shader s = Shader.Find(names[i]);
            if (s != null) return s;
        }
        return null;
    }

    static Shader LitShader
    {
        get
        {
            if (_litShader == null)
                _litShader = FindFirst("Universal Render Pipeline/Lit", "Standard", "HDRP/Lit", "Universal Render Pipeline/Simple Lit", "Sprites/Default");
            return _litShader;
        }
    }

    static Shader UnlitShader
    {
        get
        {
            if (_unlitShader == null) _unlitShader = FindFirst("Sprites/Default", "Unlit/Color", "UI/Default");
            return _unlitShader;
        }
    }

    // our own ids, because Object.GetInstanceID() is obsolete (an error) in newer Unity versions
    static readonly Dictionary<Texture, int> _texIds = new Dictionary<Texture, int>();

    static long TextureId(Texture tex)
    {
        int id;
        if (!_texIds.TryGetValue(tex, out id))
        {
            id = _texIds.Count + 1;
            _texIds[tex] = id;
        }
        return id;
    }

    static long Key(Color c, long extra)
    {
        Color32 k = c;
        long packed = ((long)k.r << 24) | ((long)k.g << 16) | ((long)k.b << 8) | k.a;
        return (packed << 20) ^ extra;
    }

    static void SetColor(Material m, Color c)
    {
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
    }

    public static Material Lit(Color c, float smoothness = 0.25f, float metallic = 0f)
    {
        long key = Key(c, (long)(smoothness * 100) * 1000 + (long)(metallic * 100));
        Material m;
        if (_lit.TryGetValue(key, out m) && m != null) return m;
        m = new Material(LitShader);
        SetColor(m, c);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
        _lit[key] = m;
        return m;
    }

    public static Material Unlit(Color c, Texture tex = null)
    {
        long key = Key(c, tex == null ? 0 : TextureId(tex));
        Material m;
        if (_unlit.TryGetValue(key, out m) && m != null) return m;
        m = new Material(UnlitShader);
        SetColor(m, c);
        if (tex != null) m.mainTexture = tex;
        _unlit[key] = m;
        return m;
    }

    static readonly Dictionary<string, Material> _litTex = new Dictionary<string, Material>();

    /// <summary>A lit material with a texture that repeats tileX x tileY times over each face (shared when the numbers match).</summary>
    public static Material LitTex(Color tint, Texture tex, float tileX, float tileY, float smoothness = 0.15f)
    {
        tileX = Mathf.Max(0.25f, Mathf.Round(tileX * 4f) / 4f);
        tileY = Mathf.Max(0.25f, Mathf.Round(tileY * 4f) / 4f);
        Color32 k = tint;
        string key = TextureId(tex) + ":" + k.r + "," + k.g + "," + k.b + ":" + tileX + "x" + tileY + ":" + (int)(smoothness * 100);
        Material m;
        if (_litTex.TryGetValue(key, out m) && m != null) return m;
        m = new Material(LitShader);
        SetColor(m, tint);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
        m.mainTexture = tex;
        m.mainTextureScale = new Vector2(tileX, tileY);
        _litTex[key] = m;
        return m;
    }

    /// <summary>An unlit material of its own (not shared) whose texture repeats tileX x tileY times over the quad.</summary>
    public static Material UnlitTiled(Color c, Texture tex, float tileX, float tileY)
    {
        Material m = new Material(UnlitShader);
        SetColor(m, c);
        m.mainTexture = tex;
        m.mainTextureScale = new Vector2(tileX, tileY);
        return m;
    }

    public static Font Font
    {
        get
        {
            if (_font == null)
            {
#if UNITY_2022_2_OR_NEWER
                _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#else
                _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
#endif
                if (_font == null) _font = Font.CreateDynamicFontFromOSFont("Arial", 16);
            }
            return _font;
        }
    }

    /// <summary>World-space text (TextMesh). height is the approximate letter height in world units.</summary>
    public static TextMesh Text(Transform parent, string text, float height, Color color, Vector3 localPos,
                                TextAnchor anchor = TextAnchor.MiddleCenter, FontStyle style = FontStyle.Bold)
    {
        var go = new GameObject("Text");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        var tm = go.AddComponent<TextMesh>();
        tm.font = Font;
        tm.text = text;
        tm.fontSize = 128;                                   // a large font texture keeps the letters sharp up close
        tm.fontStyle = style;
        tm.anchor = anchor;
        tm.alignment = TextAlignment.Center;
        tm.color = color;
        tm.characterSize = height * TextScale / 12.8f;
        go.GetComponent<MeshRenderer>().sharedMaterial = Font.material;
        return tm;
    }
}

public static class Prim
{
    public static GameObject Make(PrimitiveType type, Transform parent, Vector3 localPos, Vector3 localScale, Material mat, string name = null)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        Collider col = go.GetComponent<Collider>();
        if (col != null) Object.Destroy(col);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = localScale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        MeshKit.Smooth(go, type);
        if (name != null) go.name = name;
        return go;
    }

    /// <summary>A cone (apex up): radius 0.5 and height 2 at scale one, like the cylinder.</summary>
    public static GameObject Cone(Transform parent, Vector3 localPos, Vector3 localScale, Material mat, string name = "Cone")
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Collider col = go.GetComponent<Collider>();
        if (col != null) Object.Destroy(col);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = localScale;
        go.GetComponent<MeshFilter>().sharedMesh = MeshKit.Cone;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        go.name = name;
        return go;
    }

    /// <summary>A ring (donut) lying flat, built from a smooth lathe mesh.</summary>
    public static GameObject Torus(Transform parent, Vector3 localPos, float radius, float tube, Material mat, string name = "Torus")
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Collider col = go.GetComponent<Collider>();
        if (col != null) Object.Destroy(col);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.GetComponent<MeshFilter>().sharedMesh = MeshKit.Torus(radius, tube);
        go.GetComponent<Renderer>().sharedMaterial = mat;
        go.name = name;
        return go;
    }

    /// <summary>A flat, unlit quad lying on the table (up = true) or facing down (up = false).</summary>
    public static GameObject FlatQuad(Transform parent, Vector3 localPos, float sizeX, float sizeZ, Color color, Texture tex, bool up)
    {
        GameObject q = Make(PrimitiveType.Quad, parent, localPos, new Vector3(sizeX, sizeZ, 1f), Mats.Unlit(color, tex), "Face");
        q.transform.localRotation = Quaternion.Euler(up ? 90f : -90f, 0f, 0f);
        Renderer r = q.GetComponent<Renderer>();
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return q;
    }
}

/// <summary>Suit symbols drawn from math formulas into small textures, so no image files are needed.</summary>
public static class SuitTextures
{
    static readonly Texture2D[] _cache = new Texture2D[4];

    static bool Heart(float u, float v)
    {
        float x = u * 1.15f, y = v * 1.15f + 0.15f;
        float a = x * x + y * y - 1f;
        return a * a * a - x * x * y * y * y <= 0f;
    }

    static bool Inside(CardSuit suit, float u, float v)
    {
        switch (suit)
        {
            case CardSuit.Clubs:
                if (u * u + (v - 0.38f) * (v - 0.38f) <= 0.09f) return true;
                if ((u + 0.36f) * (u + 0.36f) + (v + 0.10f) * (v + 0.10f) <= 0.09f) return true;
                if ((u - 0.36f) * (u - 0.36f) + (v + 0.10f) * (v + 0.10f) <= 0.09f) return true;
                return v < 0.20f && v > -0.92f && Mathf.Abs(u) <= 0.07f + Mathf.Max(0f, -0.30f - v) * 0.40f;
            case CardSuit.Diamonds:
                return Mathf.Abs(u) / 0.72f + Mathf.Abs(v) / 0.98f <= 1f;
            case CardSuit.Hearts:
                return Heart(u, v);
            default:
                if (Heart(u * 1.08f, -(v - 0.22f) * 1.12f)) return true;
                return v < -0.25f && v > -0.92f && Mathf.Abs(u) <= 0.07f + Mathf.Max(0f, -0.40f - v) * 0.42f;
        }
    }

    public static Texture2D Get(CardSuit suit)
    {
        int index = (int)suit;
        if (_cache[index] != null) return _cache[index];
        const int N = 128, SS = 4;                      // 4 x 4 samples per pixel give smooth edges
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, true);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Trilinear;
        tex.anisoLevel = 8;
        var px = new Color32[N * N];
        for (int y = 0; y < N; y++)
        {
            for (int x = 0; x < N; x++)
            {
                int hit = 0;
                for (int sy = 0; sy < SS; sy++)
                    for (int sx = 0; sx < SS; sx++)
                    {
                        float u = (x + (sx + 0.5f) / SS) / N * 2f - 1f;
                        float v = (y + (sy + 0.5f) / SS) / N * 2f - 1f;
                        if (Inside(suit, u, v)) hit++;
                    }
                px[y * N + x] = new Color32(255, 255, 255, (byte)(255 * hit / (SS * SS)));
            }
        }
        tex.SetPixels32(px);
        tex.Apply(true);
        _cache[index] = tex;
        return tex;
    }
}

/// <summary>Tiny synthesized sound effects (no audio files needed).</summary>
public static class Sfx
{
    static AudioClip _card, _chip, _click, _win, _lose, _turn, _sip, _flick;
    static readonly System.Random Rnd = new System.Random(11);

    static AudioClip Make(string name, float seconds, System.Func<float, float> f)
    {
        const int rate = 44100;
        int n = (int)(seconds * rate);
        var data = new float[n];
        for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(f(i / (float)rate), -1f, 1f);
        AudioClip clip = AudioClip.Create(name, n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    static float Noise() { return (float)(Rnd.NextDouble() * 2.0 - 1.0); }

    public static AudioClip Card
    {
        get { if (_card == null) _card = Make("card", 0.09f, t => Noise() * Mathf.Exp(-t * 55f) * 0.45f); return _card; }
    }

    public static AudioClip Chip
    {
        get
        {
            if (_chip == null)
                _chip = Make("chip", 0.16f, t =>
                {
                    float a = Mathf.Sin(t * 2f * Mathf.PI * 2600f) * Mathf.Exp(-t * 90f);
                    float tt = t - 0.055f;
                    float b = tt > 0f ? Mathf.Sin(tt * 2f * Mathf.PI * 3100f) * Mathf.Exp(-tt * 90f) : 0f;
                    return (a + b) * 0.35f + Noise() * Mathf.Exp(-t * 120f) * 0.15f;
                });
            return _chip;
        }
    }

    public static AudioClip Click
    {
        get { if (_click == null) _click = Make("click", 0.06f, t => Mathf.Sin(t * 2f * Mathf.PI * 900f) * Mathf.Exp(-t * 45f) * 0.3f); return _click; }
    }

    public static AudioClip Turn
    {
        get { if (_turn == null) _turn = Make("turn", 0.5f, t => Mathf.Sin(t * 2f * Mathf.PI * 1320f) * Mathf.Exp(-t * 7f) * 0.22f); return _turn; }
    }

    public static AudioClip Win
    {
        get
        {
            if (_win == null)
            {
                float[] notes = { 523.25f, 659.25f, 783.99f, 1046.5f };
                _win = Make("win", 0.8f, t =>
                {
                    int k = Mathf.Clamp((int)(t / 0.14f), 0, notes.Length - 1);
                    float local = t - k * 0.14f;
                    return Mathf.Sin(local * 2f * Mathf.PI * notes[k]) * Mathf.Exp(-local * 4f) * 0.3f;
                });
            }
            return _win;
        }
    }

    /// <summary>A soft slurp: filtered noise that swells and fades.</summary>
    public static AudioClip Sip
    {
        get
        {
            if (_sip == null)
            {
                float lp = 0f;
                _sip = Make("sip", 0.55f, t =>
                {
                    lp += (Noise() - lp) * 0.08f;
                    float env = Mathf.Sin(Mathf.Clamp01(t / 0.55f) * Mathf.PI);
                    return lp * 3.2f * env * 0.35f + Mathf.Sin(t * 2f * Mathf.PI * (260f + 380f * t)) * 0.05f * env;
                });
            }
            return _sip;
        }
    }

    /// <summary>A lighter: a sharp click and a short hiss.</summary>
    public static AudioClip Flick
    {
        get
        {
            if (_flick == null)
                _flick = Make("flick", 0.35f, t =>
                {
                    float click = Mathf.Sin(t * 2f * Mathf.PI * 2200f) * Mathf.Exp(-t * 160f) * 0.5f;
                    float hiss = t > 0.05f ? Noise() * Mathf.Exp(-(t - 0.05f) * 9f) * 0.09f : 0f;
                    return click + hiss;
                });
            return _flick;
        }
    }

    public static AudioClip Lose
    {
        get
        {
            if (_lose == null)
            {
                float[] notes = { 392f, 329.63f, 261.63f };
                _lose = Make("lose", 0.7f, t =>
                {
                    int k = Mathf.Clamp((int)(t / 0.2f), 0, notes.Length - 1);
                    float local = t - k * 0.2f;
                    return Mathf.Sin(local * 2f * Mathf.PI * notes[k]) * Mathf.Exp(-local * 3.5f) * 0.3f;
                });
            }
            return _lose;
        }
    }
}
