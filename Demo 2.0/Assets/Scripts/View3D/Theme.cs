using UnityEngine;

/// <summary>Colours, textures and music of the Albanian theme (red, black and gold; the double-headed eagle; kilim patterns).</summary>
public static class Theme
{
    public static readonly Color Red = new Color(0.78f, 0.07f, 0.16f);
    public static readonly Color DeepRed = new Color(0.47f, 0.04f, 0.09f);
    public static readonly Color Black = new Color(0.07f, 0.065f, 0.08f);
    public static readonly Color Charcoal = new Color(0.16f, 0.15f, 0.18f);
    public static readonly Color Gold = new Color(0.84f, 0.66f, 0.23f);
    public static readonly Color DarkGold = new Color(0.55f, 0.40f, 0.08f);
    public static readonly Color Cream = new Color(0.93f, 0.88f, 0.77f);

    static Texture2D _eagle, _strip, _ring, _rug, _wood, _stone, _roof, _plaster;

    /// <summary>
    /// A texture with mipmaps and trilinear + anisotropic filtering: it stays smooth when the surface is far away or seen
    /// at a flat angle (the main cause of a "pixelated" look is a texture without mipmaps).
    /// </summary>
    static Texture2D FromRgba(byte[] rgba, int w, int h, TextureWrapMode wrap)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, true);
        tex.wrapMode = wrap;
        tex.filterMode = FilterMode.Trilinear;
        tex.anisoLevel = 16;
        var px = new Color32[w * h];
        for (int i = 0; i < px.Length; i++)
            px[i] = new Color32(rgba[i * 4], rgba[i * 4 + 1], rgba[i * 4 + 2], rgba[i * 4 + 3]);
        tex.SetPixels32(px);
        tex.Apply(true);
        return tex;
    }

    /// <summary>The double-headed eagle, white on transparent (tint it with the material colour).</summary>
    public static Texture2D Eagle
    {
        get { if (_eagle == null) _eagle = FromRgba(Art.EagleRGBA(512, 255, 255, 255), 512, 512, TextureWrapMode.Clamp); return _eagle; }
    }

    /// <summary>A long kilim strip for the wall.</summary>
    public static Texture2D Strip
    {
        get { if (_strip == null) _strip = FromRgba(Art.StripRGBA(4096, 128, 24), 4096, 128, TextureWrapMode.Repeat); return _strip; }
    }

    /// <summary>The kilim ring that decorates the table rim (fills a square quad that is as wide as the rim).</summary>
    public static Texture2D Ring
    {
        get { if (_ring == null) _ring = FromRgba(Art.RingRGBA(1024, 0.905f, 0.995f, 40), 1024, 1024, TextureWrapMode.Clamp); return _ring; }
    }

    /// <summary>The rug under the table.</summary>
    public static Texture2D Rug
    {
        get { if (_rug == null) _rug = FromRgba(Art.RugRGBA(1024), 1024, 1024, TextureWrapMode.Clamp); return _rug; }
    }

    public static Texture2D Wood
    {
        get { if (_wood == null) _wood = FromRgba(Art.WoodRGBA(512, 8), 512, 512, TextureWrapMode.Repeat); return _wood; }
    }

    public static Texture2D Stone
    {
        get { if (_stone == null) _stone = FromRgba(Art.StoneRGBA(512), 512, 512, TextureWrapMode.Repeat); return _stone; }
    }

    public static Texture2D RoofTile
    {
        get { if (_roof == null) _roof = FromRgba(Art.RoofTileRGBA(512), 512, 512, TextureWrapMode.Repeat); return _roof; }
    }

    public static Texture2D Plaster
    {
        get { if (_plaster == null) _plaster = FromRgba(Art.PlasterRGBA(256), 256, 256, TextureWrapMode.Repeat); return _plaster; }
    }

    /// <summary>A smooth vertical gradient (horizon at the bottom, zenith at the top) for the sky.</summary>
    public static Texture2D Sky(Color horizon, Color top)
    {
        const int H = 256;
        var tex = new Texture2D(2, H, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        var px = new Color32[2 * H];
        for (int y = 0; y < H; y++)
        {
            float t = Mathf.Pow(y / (float)(H - 1), 0.75f);
            Color c = Color.Lerp(horizon, top, t);
            px[y * 2] = c;
            px[y * 2 + 1] = c;
        }
        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }

    // ---------------------------------------------------------------------------------------
    // one look per map. Maps -1 (the oda), 0..3 (the cities of the tournament), 4 (Sarandë), 5 (Theth)
    // ---------------------------------------------------------------------------------------

    public class CityLook
    {
        public Color Felt, FeltInner, Wall, Sky, SkyTop, House, HouseDark, Window, Rug, Floor;
        public int Style;          // 0 oda, 1 Shkodër, 2 Berat, 3 Gjirokastër, 4 Tiranë, 5 Sarandë, 6 Theth
    }

    public const int MapCount = 7;                 // -1 .. 5

    public static CityLook Look(int city)
    {
        switch (city)
        {
            case 0: // Shkodër: the lake and the castle of Rozafa
                return new CityLook
                {
                    Felt = new Color(0.04f, 0.24f, 0.33f), FeltInner = new Color(0.05f, 0.28f, 0.38f),
                    Wall = new Color(0.06f, 0.10f, 0.18f), Sky = new Color(0.30f, 0.38f, 0.60f), SkyTop = new Color(0.04f, 0.07f, 0.18f),
                    House = new Color(0.78f, 0.72f, 0.64f), HouseDark = new Color(0.50f, 0.45f, 0.42f),
                    Window = new Color(1f, 0.82f, 0.40f), Rug = new Color(0.55f, 0.65f, 0.80f), Floor = new Color(0.12f, 0.10f, 0.12f), Style = 1
                };
            case 1: // Berat: white houses climbing the hill
                return new CityLook
                {
                    Felt = new Color(0.04f, 0.32f, 0.21f), FeltInner = new Color(0.05f, 0.36f, 0.24f),
                    Wall = new Color(0.10f, 0.08f, 0.14f), Sky = new Color(0.66f, 0.40f, 0.42f), SkyTop = new Color(0.10f, 0.07f, 0.22f),
                    House = new Color(0.97f, 0.95f, 0.90f), HouseDark = new Color(0.78f, 0.75f, 0.72f),
                    Window = new Color(1f, 0.78f, 0.35f), Rug = new Color(0.70f, 0.85f, 0.70f), Floor = new Color(0.12f, 0.10f, 0.12f), Style = 2
                };
            case 2: // Gjirokastër: stone houses and the castle
                return new CityLook
                {
                    Felt = new Color(0.18f, 0.21f, 0.30f), FeltInner = new Color(0.21f, 0.25f, 0.34f),
                    Wall = new Color(0.08f, 0.08f, 0.11f), Sky = new Color(0.42f, 0.38f, 0.46f), SkyTop = new Color(0.06f, 0.07f, 0.15f),
                    House = new Color(0.78f, 0.78f, 0.80f), HouseDark = new Color(0.52f, 0.52f, 0.56f),
                    Window = new Color(1f, 0.74f, 0.32f), Rug = new Color(0.80f, 0.74f, 0.64f), Floor = new Color(0.12f, 0.11f, 0.13f), Style = 3
                };
            case 3: // Tiranë: the final, red and black
                return new CityLook
                {
                    Felt = new Color(0.44f, 0.04f, 0.09f), FeltInner = new Color(0.50f, 0.05f, 0.10f),
                    Wall = new Color(0.07f, 0.05f, 0.08f), Sky = new Color(0.58f, 0.22f, 0.27f), SkyTop = new Color(0.07f, 0.04f, 0.11f),
                    House = new Color(0.62f, 0.55f, 0.52f), HouseDark = new Color(0.36f, 0.30f, 0.32f),
                    Window = new Color(1f, 0.85f, 0.45f), Rug = new Color(1f, 0.85f, 0.85f), Floor = new Color(0.12f, 0.09f, 0.11f), Style = 4
                };
            case 4: // Sarandë: the Ionian sea at sunset
                return new CityLook
                {
                    Felt = new Color(0.07f, 0.22f, 0.50f), FeltInner = new Color(0.09f, 0.27f, 0.57f),
                    Wall = new Color(0.10f, 0.14f, 0.26f), Sky = new Color(1.00f, 0.62f, 0.32f), SkyTop = new Color(0.12f, 0.22f, 0.48f),
                    House = new Color(0.98f, 0.96f, 0.90f), HouseDark = new Color(0.86f, 0.82f, 0.76f),
                    Window = new Color(1f, 0.80f, 0.40f), Rug = new Color(0.85f, 0.88f, 1f), Floor = new Color(0.17f, 0.13f, 0.12f), Style = 5
                };
            case 5: // Theth: the Albanian Alps at dawn
                return new CityLook
                {
                    Felt = new Color(0.33f, 0.20f, 0.10f), FeltInner = new Color(0.38f, 0.23f, 0.12f),
                    Wall = new Color(0.10f, 0.12f, 0.16f), Sky = new Color(0.62f, 0.72f, 0.86f), SkyTop = new Color(0.07f, 0.13f, 0.32f),
                    House = new Color(0.72f, 0.70f, 0.68f), HouseDark = new Color(0.46f, 0.44f, 0.44f),
                    Window = new Color(1f, 0.80f, 0.45f), Rug = new Color(0.90f, 0.82f, 0.70f), Floor = new Color(0.10f, 0.12f, 0.10f), Style = 6
                };
            default: // the oda e burrave
                return new CityLook
                {
                    Felt = new Color(0.44f, 0.04f, 0.09f), FeltInner = new Color(0.50f, 0.05f, 0.10f),
                    Wall = new Color(0.30f, 0.22f, 0.16f), Sky = new Color(0.12f, 0.07f, 0.10f), SkyTop = new Color(0.07f, 0.04f, 0.06f),
                    House = new Color(0.50f, 0.44f, 0.42f), HouseDark = new Color(0.30f, 0.26f, 0.26f),
                    Window = new Color(1f, 0.82f, 0.42f), Rug = new Color(1f, 0.9f, 0.9f), Floor = new Color(0.45f, 0.30f, 0.18f), Style = 0
                };
        }
    }
}

/// <summary>
/// A loop of folk-style music, synthesized (no audio files): a plucked string (Karplus-Strong, like a çiftelia) playing a
/// dance tune in 7/8 (3+2+2) on a scale with an augmented second, a two-note drone like the iso-polyphonic
/// singing of the south, and a soft frame drum (daire) on the accents.
/// </summary>
public static class Music
{
    static AudioClip _loop;
    static AudioClip _anthem;
    static bool _anthemTried;

    /// <summary>
    /// Himni i Flamurit as a real recording: put any audio file (.ogg / .mp3 / .wav) in a folder called Resources, best in
    /// Assets/Resources/Music/. The file name does not matter: a clip with "himn" or "hymn" in its name is preferred, otherwise the
    /// first clip found. Returns null if there is none (the folk loop is used then).
    /// </summary>
    public static AudioClip Anthem
    {
        get
        {
            if (!_anthemTried)
            {
                _anthemTried = true;
                try
                {
                    var all = new System.Collections.Generic.List<AudioClip>();
                    all.AddRange(Resources.LoadAll<AudioClip>("Music"));
                    all.AddRange(Resources.LoadAll<AudioClip>(""));
                    for (int i = 0; i < all.Count && _anthem == null; i++)
                    {
                        string n = all[i].name.ToLowerInvariant();
                        if (n.Contains("himn") || n.Contains("hymn") || n.Contains("flamur")) _anthem = all[i];
                    }
                    if (_anthem == null && all.Count > 0) _anthem = all[0];
                    Debug.Log(_anthem != null ? "[Muzika] Himni u gjet: " + _anthem.name
                                              : "[Muzika] Nuk u gjet asnjë skedar audio në Assets/Resources (ose Assets/Resources/Music).");
                }
                catch (System.Exception) { _anthem = null; }
            }
            return _anthem;
        }
    }

    const int Rate = 22050;

    static void Pluck(float[] buf, float start, float freq, float seconds, float gain, System.Random rng)
    {
        int n = Mathf.Max(2, (int)(Rate / freq));
        var line = new float[n];
        for (int i = 0; i < n; i++) line[i] = (float)(rng.NextDouble() * 2.0 - 1.0);
        int first = (int)(start * Rate);
        int count = (int)(seconds * Rate);
        int p = 0;
        for (int i = 0; i < count; i++)
        {
            int at = first + i;
            if (at >= buf.Length) break;
            float cur = line[p];
            float next = line[(p + 1) % n];
            line[p] = 0.5f * (cur + next) * 0.9965f;
            p = (p + 1) % n;
            float fade = i < 60 ? i / 60f : 1f;
            buf[at] += cur * gain * fade;
        }
    }

    static void Drum(float[] buf, float start, float gain, float pitch)
    {
        int first = (int)(start * Rate);
        int count = (int)(0.24f * Rate);
        for (int i = 0; i < count; i++)
        {
            int at = first + i;
            if (at >= buf.Length) break;
            float t = i / (float)Rate;
            buf[at] += Mathf.Sin(t * 2f * Mathf.PI * (pitch - t * 170f)) * Mathf.Exp(-t * 20f) * gain;
        }
    }

    static float Freq(int semitones) { return 293.66f * Mathf.Pow(2f, semitones / 12f); }   // D4 plus semitones

    public static AudioClip Loop
    {
        get
        {
            if (_loop != null) return _loop;
            const float eighth = 0.20f;
            const int R = -99;
            // D, Eb, F#, G, A, Bb, C, D: the "Phrygian dominant" scale, with the augmented second Eb-F# of Balkan tunes
            int[][] phrase =
            {
                new int[] { 7, 8, 7, 5, 4, 1, 0 },
                new int[] { 0, 1, 4, 1, 0, -5, 0 },
                new int[] { 12, 13, 12, 10, 8, 7, 5 },
                new int[] { 7, 5, 4, 1, 4, 1, 0 },
                new int[] { 4, 5, 7, 5, 4, 1, R }
            };
            int[] song = { 0, 1, 0, 3, 0, 1, 2, 3, 2, 0, 1, 4 };
            int bars = song.Length;
            float total = bars * 7 * eighth;
            var buf = new float[(int)(total * Rate)];
            var rng = new System.Random(7);

            // drone: two sustained notes a fifth apart that slowly swell (three swells per loop, so the seam is clean)
            for (int i = 0; i < buf.Length; i++)
            {
                float t = i / (float)Rate;
                float swell = 0.75f + 0.25f * Mathf.Sin(t / total * 2f * Mathf.PI * 3f);
                float vib = 1f + 0.002f * Mathf.Sin(t * 2f * Mathf.PI * 5.1f);
                float a = t * 2f * Mathf.PI * 146.83f * vib, b = t * 2f * Mathf.PI * 220f * vib;
                buf[i] += swell * (0.045f * (Mathf.Sin(a) + 0.5f * Mathf.Sin(2f * a) + 0.25f * Mathf.Sin(3f * a)) +
                                   0.030f * (Mathf.Sin(b) + 0.4f * Mathf.Sin(2f * b)));
            }

            for (int bar = 0; bar < bars; bar++)
            {
                int[] notes = phrase[song[bar]];
                for (int k = 0; k < 7; k++)
                {
                    float start = (bar * 7 + k) * eighth;
                    bool accent = k == 0 || k == 3 || k == 5;                  // 3 + 2 + 2
                    if (notes[k] != R) Pluck(buf, start, Freq(notes[k]), eighth * (accent ? 3.2f : 2.2f), accent ? 0.55f : 0.40f, rng);
                    if (accent) Drum(buf, start, k == 0 ? 0.40f : 0.22f, k == 0 ? 92f : 118f);
                    else if (k == 4 || k == 6) Drum(buf, start, 0.07f, 170f);
                }
                Pluck(buf, bar * 7 * eighth, 146.83f, eighth * 5f, 0.45f, rng);   // the low D under each bar
            }

            // keep it quiet and make the loop seam clean
            float peak = 0.001f;
            for (int i = 0; i < buf.Length; i++) if (Mathf.Abs(buf[i]) > peak) peak = Mathf.Abs(buf[i]);
            int fadeN = (int)(0.04f * Rate);
            for (int i = 0; i < buf.Length; i++)
            {
                float g = 0.8f / peak;
                if (i < fadeN) g *= i / (float)fadeN;
                if (i > buf.Length - fadeN) g *= (buf.Length - i) / (float)fadeN;
                buf[i] *= g;
            }
            _loop = AudioClip.Create("sq-music", buf.Length, 1, Rate, false);
            _loop.SetData(buf, 0);
            return _loop;
        }
    }
}
