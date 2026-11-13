// Painted city pictures for the menu wheel segments (procedural, 256x256).
// Recovered from the last good build after the source file went missing.
using System;
using UnityEngine;

public static class CityArt
{
    private static class Material2D
    {
        public static void Castle(Painter p, float x0, float y, float w, Color light, Color dark)
        {
            p.Rect(x0, y, x0 + w, y + w * 0.22f, light);
            int num = 9;
            for (int i = 0; i < num; i++)
            {
                p.Rect(x0 + ((float)i + 0.2f) * w / (float)num, y + w * 0.22f, x0 + ((float)i + 0.7f) * w / (float)num, y + w * 0.27f, light);
            }
            float[] array = new float[2]
            {
                x0 - w * 0.04f,
                x0 + w * 0.88f
            };
            foreach (float num2 in array)
            {
                p.Rect(num2, y, num2 + w * 0.16f, y + w * 0.38f, dark);
                for (int k = 0; k < 3; k++)
                {
                    p.Rect(num2 + (float)k * w * 0.06f, y + w * 0.38f, num2 + (float)k * w * 0.06f + w * 0.035f, y + w * 0.42f, dark);
                }
            }
            p.Rect(x0 + w * 0.44f, y, x0 + w * 0.56f, y + w * 0.12f, C(30, 25, 30));
            p.Disc(x0 + w * 0.5f, y + w * 0.12f, w * 0.06f, C(30, 25, 30));
        }
    }

    public class Painter
    {
        private readonly int _n;

        private readonly Color[] _px;

        public Painter(int n)
        {
            _n = n;
            _px = new Color[n * n];
        }

        private void Blend(int x, int y, Color c, float a)
        {
            if (!(a <= 0f) && x >= 0 && y >= 0 && x < _n && y < _n)
            {
                int num = y * _n + x;
                a = Mathf.Clamp01(a * c.a);
                Color color = _px[num];
                _px[num] = new Color(color.r + (c.r - color.r) * a, color.g + (c.g - color.g) * a, color.b + (c.b - color.b) * a, 1f);
            }
        }

        public void Grad(Color top, Color bottom, float y0, float y1)
        {
            int num = Mathf.Clamp(Mathf.FloorToInt(y0 * (float)_n), 0, _n);
            int num2 = Mathf.Clamp(Mathf.CeilToInt(y1 * (float)_n), 0, _n);
            for (int i = num; i < num2; i++)
            {
                Color c = Color.Lerp(bottom, top, Mathf.InverseLerp(y0 * (float)_n, y1 * (float)_n, (float)i + 0.5f));
                for (int j = 0; j < _n; j++)
                {
                    Blend(j, i, c, 1f);
                }
            }
        }

        public void Rect(float x0, float y0, float x1, float y1, Color c)
        {
            float num = x0 * (float)_n;
            float num2 = y0 * (float)_n;
            float num3 = x1 * (float)_n;
            float num4 = y1 * (float)_n;
            for (int i = Mathf.Max(0, (int)num2); i < Mathf.Min(_n, Mathf.CeilToInt(num4)); i++)
            {
                float num5 = Mathf.Clamp01(Mathf.Min(num4, i + 1) - Mathf.Max(num2, i));
                for (int j = Mathf.Max(0, (int)num); j < Mathf.Min(_n, Mathf.CeilToInt(num3)); j++)
                {
                    Blend(j, i, c, num5 * Mathf.Clamp01(Mathf.Min(num3, j + 1) - Mathf.Max(num, j)));
                }
            }
        }

        public void Ellipse(float cx, float cy, float rx, float ry, Color c)
        {
            float num = cx * (float)_n;
            float num2 = cy * (float)_n;
            float num3 = rx * (float)_n;
            float num4 = ry * (float)_n;
            for (int i = Mathf.Max(0, (int)(num2 - num4 - 1f)); i < Mathf.Min(_n, (int)(num2 + num4 + 2f)); i++)
            {
                for (int j = Mathf.Max(0, (int)(num - num3 - 1f)); j < Mathf.Min(_n, (int)(num + num3 + 2f)); j++)
                {
                    float num5 = ((float)j + 0.5f - num) / num3;
                    float num6 = ((float)i + 0.5f - num2) / num4;
                    float num7 = (Mathf.Sqrt(num5 * num5 + num6 * num6) - 1f) * Mathf.Min(num3, num4);
                    Blend(j, i, c, Mathf.Clamp01(0.5f - num7));
                }
            }
        }

        public void Disc(float cx, float cy, float r, Color c)
        {
            Ellipse(cx, cy, r, r, c);
        }

        public void Glow(float cx, float cy, float r, Color c, float strength)
        {
            float num = cx * (float)_n;
            float num2 = cy * (float)_n;
            float num3 = r * (float)_n;
            for (int i = Mathf.Max(0, (int)(num2 - num3)); i < Mathf.Min(_n, (int)(num2 + num3 + 1f)); i++)
            {
                for (int j = Mathf.Max(0, (int)(num - num3)); j < Mathf.Min(_n, (int)(num + num3 + 1f)); j++)
                {
                    float num4 = Mathf.Sqrt(((float)j + 0.5f - num) * ((float)j + 0.5f - num) + ((float)i + 0.5f - num2) * ((float)i + 0.5f - num2)) / num3;
                    if (num4 < 1f)
                    {
                        Blend(j, i, c, strength * (1f - num4) * (1f - num4));
                    }
                }
            }
        }

        public void Tri(Vector2 a, Vector2 b, Vector2 c, Color col)
        {
            a *= (float)_n;
            b *= (float)_n;
            c *= (float)_n;
            int num = Mathf.Max(0, (int)Mathf.Min(a.x, Mathf.Min(b.x, c.x)));
            int num2 = Mathf.Min(_n - 1, (int)Mathf.Max(a.x, Mathf.Max(b.x, c.x)) + 1);
            int num3 = Mathf.Max(0, (int)Mathf.Min(a.y, Mathf.Min(b.y, c.y)));
            int num4 = Mathf.Min(_n - 1, (int)Mathf.Max(a.y, Mathf.Max(b.y, c.y)) + 1);
            for (int i = num3; i <= num4; i++)
            {
                for (int j = num; j <= num2; j++)
                {
                    int num5 = 0;
                    for (int k = 0; k < 4; k++)
                    {
                        if (Inside(new Vector2((float)j + 0.25f + 0.5f * (float)(k & 1), (float)i + 0.25f + 0.5f * (float)(k >> 1)), a, b, c))
                        {
                            num5++;
                        }
                    }
                    Blend(j, i, col, (float)num5 / 4f);
                }
            }
        }

        private static bool Inside(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float num = (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);
            float num2 = (p.x - c.x) * (b.y - c.y) - (b.x - c.x) * (p.y - c.y);
            float num3 = (p.x - a.x) * (c.y - a.y) - (c.x - a.x) * (p.y - a.y);
            bool flag = num < 0f || num2 < 0f || num3 < 0f;
            bool flag2 = num > 0f || num2 > 0f || num3 > 0f;
            return !(flag && flag2);
        }

        public void Vignette(float amount)
        {
            for (int i = 0; i < _n; i++)
            {
                for (int j = 0; j < _n; j++)
                {
                    float num = ((float)j + 0.5f) / (float)_n - 0.5f;
                    float num2 = ((float)i + 0.5f) / (float)_n - 0.5f;
                    float num3 = Mathf.Clamp01((Mathf.Sqrt(num * num + num2 * num2) - 0.3f) / 0.45f);
                    Blend(j, i, Color.black, num3 * num3 * amount);
                }
            }
        }

        public Texture2D ToTexture()
        {
            Texture2D texture2D = new Texture2D(_n, _n, TextureFormat.RGBA32, mipChain: false);
            texture2D.wrapMode = TextureWrapMode.Clamp;
            texture2D.filterMode = FilterMode.Bilinear;
            texture2D.SetPixels(_px);
            texture2D.Apply(updateMipmaps: false);
            return texture2D;
        }
    }

    private const int N = 256;

    private static readonly Texture2D[] _cache = new Texture2D[7];

    public static Texture2D Get(int map)
    {
        int num = Mathf.Clamp(map + 1, 0, 6);
        if (_cache[num] != null)
        {
            return _cache[num];
        }
        Painter painter = new Painter(256);
        switch (map)
        {
        case -1:
            Oda(painter);
            break;
        case 0:
            Shkoder(painter);
            break;
        case 1:
            Berat(painter);
            break;
        case 2:
            Prishtine(painter);
            break;
        case 3:
            Tirane(painter);
            break;
        case 4:
            Sarande(painter);
            break;
        default:
            Ulqin(painter);
            break;
        }
        painter.Vignette(0.35f);
        _cache[num] = painter.ToTexture();
        return _cache[num];
    }

    public static void Release()
    {
        for (int i = 0; i < _cache.Length; i++)
        {
            if (_cache[i] != null)
            {
                UnityEngine.Object.Destroy(_cache[i]);
                _cache[i] = null;
            }
        }
    }

    private static Color C(int r, int g, int b)
    {
        return new Color((float)r / 255f, (float)g / 255f, (float)b / 255f);
    }

    private static void Oda(Painter p)
    {
        p.Grad(C(150, 82, 44), C(92, 44, 26), 0.3f, 1f);
        p.Rect(0f, 0f, 1f, 0.3f, C(58, 32, 20));
        for (int i = 0; i < 9; i++)
        {
            p.Rect((float)i / 9f, 0f, (float)i / 9f + 0.004f, 0.3f, C(40, 22, 14));
        }
        p.Rect(0f, 0.62f, 1f, 0.72f, C(160, 22, 30));
        for (int j = 0; j < 12; j++)
        {
            float num = (float)j / 12f;
            p.Tri(new Vector2(num, 0.63f), new Vector2(num + 1f / 12f, 0.63f), new Vector2(num + 1f / 24f, 0.71f), (j % 2 == 0) ? C(20, 14, 14) : C(220, 170, 70));
        }
        p.Rect(0.28f, 0.3f, 0.72f, 0.6f, C(208, 190, 168));
        p.Rect(0.25f, 0.58f, 0.75f, 0.62f, C(180, 160, 140));
        p.Rect(0.35f, 0.3f, 0.65f, 0.48f, C(22, 14, 12));
        p.Disc(0.5f, 0.48f, 0.15f, C(22, 14, 12));
        p.Rect(0.35f, 0.3f, 0.65f, 0.33f, C(70, 40, 25));
        p.Glow(0.5f, 0.38f, 0.28f, C(255, 150, 50), 0.55f);
        p.Tri(new Vector2(0.4f, 0.33f), new Vector2(0.5f, 0.33f), new Vector2(0.45f, 0.46f), C(255, 120, 30));
        p.Tri(new Vector2(0.46f, 0.33f), new Vector2(0.58f, 0.33f), new Vector2(0.52f, 0.52f), C(255, 170, 50));
        p.Tri(new Vector2(0.49f, 0.33f), new Vector2(0.56f, 0.33f), new Vector2(0.525f, 0.43f), C(255, 230, 140));
        float[] array = new float[2] { 0.12f, 0.88f };
        foreach (float cx in array)
        {
            p.Disc(cx, 0.5f, 0.075f, C(190, 105, 55));
            p.Disc(cx, 0.5f, 0.05f, C(215, 135, 75));
            p.Disc(cx, 0.5f, 0.015f, C(240, 200, 110));
        }
        p.Rect(0f, 0.86f, 1f, 0.9f, C(70, 40, 25));
    }

    private static void Shkoder(Painter p)
    {
        p.Grad(C(18, 26, 62), C(78, 98, 150), 0.42f, 1f);
        for (int i = 0; i < 40; i++)
        {
            p.Disc(Hash(i, 1), 0.6f + 0.4f * Hash(i, 2), 0.003f + 0.003f * Hash(i, 3), C(255, 245, 210));
        }
        p.Glow(0.25f, 0.82f, 0.18f, C(255, 240, 200), 0.35f);
        p.Disc(0.25f, 0.82f, 0.065f, C(250, 242, 215));
        Peak(p, -0.1f, 0.42f, 0.32f, 0.78f, 0.7f, C(34, 44, 84), snow: true);
        Peak(p, 0.35f, 0.42f, 0.62f, 0.72f, 0.95f, C(42, 54, 96), snow: true);
        Peak(p, 0.65f, 0.42f, 0.95f, 0.8f, 1.2f, C(30, 40, 78), snow: true);
        p.Ellipse(0.72f, 0.42f, 0.38f, 0.17f, C(24, 52, 38));
        Material2D.Castle(p, 0.56f, 0.53f, 0.32f, C(222, 205, 170), C(160, 140, 115));
        p.Grad(C(28, 52, 92), C(12, 24, 48), 0f, 0.42f);
        for (int j = 0; j < 9; j++)
        {
            float num = 0.38f - (float)j * 0.04f;
            float num2 = 0.05f + (float)j * 0.012f;
            p.Rect(0.25f - num2, num, 0.25f + num2, num + 0.008f, new Color(1f, 0.95f, 0.8f, 0.55f - (float)j * 0.04f));
        }
        for (int k = 0; k < 14; k++)
        {
            p.Rect(0.02f + (float)k * 0.04f, 0.42f, 0.045f + (float)k * 0.04f, 0.45f + 0.03f * Hash(k, 7), C(30, 30, 45));
        }
        for (int l = 0; l < 14; l++)
        {
            p.Rect(0.03f + (float)l * 0.04f, 0.43f, 0.037f + (float)l * 0.04f, 0.437f, C(255, 200, 100));
        }
    }

    private static void Berat(Painter p)
    {
        p.Grad(C(70, 40, 100), C(235, 140, 120), 0.3f, 1f);
        p.Glow(0.75f, 0.45f, 0.4f, C(255, 190, 140), 0.45f);
        p.Ellipse(0.5f, 0.15f, 0.75f, 0.6f, C(70, 80, 70));
        p.Rect(0.22f, 0.72f, 0.78f, 0.77f, C(160, 140, 125));
        for (int i = 0; i < 12; i++)
        {
            p.Rect(0.22f + (float)i * 0.048f, 0.77f, 0.24f + (float)i * 0.048f, 0.79f, C(160, 140, 125));
        }
        for (int j = 0; j < 6; j++)
        {
            float num = 0.08f + (float)j * 0.105f;
            float num2 = 0.1f - (float)j * 0.006f;
            float num3 = 0.075f - (float)j * 0.004f;
            float num4 = 0.95f - (float)j * 0.11f;
            float num5 = 0.5f - num4 * 0.5f;
            int num6 = Mathf.FloorToInt(num4 / (num3 + 0.006f));
            for (int k = 0; k < num6; k++)
            {
                float num7 = num5 + (float)k * (num3 + 0.006f);
                Color c = ((Hash(j * 31 + k, 4) < 0.2f) ? C(225, 220, 210) : C(246, 242, 232));
                p.Rect(num7, num, num7 + num3, num + num2, c);
                p.Rect(num7 - 0.004f, num + num2, num7 + num3 + 0.004f, num + num2 + 0.012f, C(150, 70, 55));
                for (int l = 0; l < 2; l++)
                {
                    for (int m = 0; m < 3; m++)
                    {
                        float num8 = num7 + num3 * (0.2f + (float)m * 0.3f);
                        float num9 = num + num2 * (0.28f + (float)l * 0.4f);
                        Color c2 = ((Hash(j * 97 + k * 13 + l * 3 + m, 5) < 0.45f) ? C(255, 200, 110) : C(60, 50, 60));
                        p.Rect(num8 - num3 * 0.07f, num9 - num2 * 0.1f, num8 + num3 * 0.07f, num9 + num2 * 0.1f, c2);
                    }
                }
            }
        }
        p.Rect(0f, 0f, 1f, 0.08f, C(40, 90, 110));
    }

    private static void Tirane(Painter p)
    {
        p.Grad(C(15, 20, 50), C(110, 60, 110), 0.25f, 1f);
        p.Glow(0.5f, 0.25f, 0.6f, C(255, 150, 90), 0.35f);
        Color[] array = new Color[6]
        {
            C(225, 70, 70),
            C(250, 190, 60),
            C(60, 170, 160),
            C(140, 90, 200),
            C(240, 120, 60),
            C(80, 140, 220)
        };
        for (int i = 0; i < 9; i++)
        {
            float num = (float)i * 0.115f - 0.02f;
            float num2 = 0.35f + 0.3f * Hash(i, 8);
            p.Rect(num, 0.18f, num + 0.1f, 0.18f + num2, array[i % array.Length]);
            p.Rect(num, 0.18f + num2 * 0.45f, num + 0.1f, 0.18f + num2 * 0.5f, array[(i + 2) % array.Length]);
            for (int j = 0; j < 6; j++)
            {
                for (int k = 0; k < 3; k++)
                {
                    float num3 = 0.22f + (float)j * 0.07f;
                    if (!(num3 > 0.14f + num2))
                    {
                        p.Rect(num + 0.015f + (float)k * 0.03f, num3, num + 0.03f + (float)k * 0.03f, num3 + 0.03f, (Hash(i * 40 + j * 3 + k, 9) < 0.55f) ? C(255, 225, 140) : C(40, 30, 50));
                    }
                }
            }
        }
        p.Rect(0.08f, 0.18f, 0.36f, 0.42f, C(235, 215, 180));
        p.Disc(0.22f, 0.42f, 0.09f, C(200, 175, 140));
        p.Rect(0.215f, 0.5f, 0.225f, 0.54f, C(220, 190, 90));
        p.Rect(0.38f, 0.18f, 0.41f, 0.72f, C(235, 215, 180));
        p.Tri(new Vector2(0.375f, 0.72f), new Vector2(0.415f, 0.72f), new Vector2(0.395f, 0.82f), C(120, 110, 100));
        p.Rect(0.62f, 0.18f, 0.72f, 0.8f, C(205, 175, 130));
        p.Rect(0.61f, 0.8f, 0.73f, 0.83f, C(175, 145, 105));
        p.Tri(new Vector2(0.61f, 0.83f), new Vector2(0.73f, 0.83f), new Vector2(0.67f, 0.93f), C(150, 60, 50));
        p.Disc(0.67f, 0.72f, 0.035f, C(250, 240, 210));
        p.Rect(0.668f, 0.72f, 0.672f, 0.745f, C(30, 30, 30));
        p.Rect(0.67f, 0.718f, 0.69f, 0.722f, C(30, 30, 30));
        p.Rect(0f, 0f, 1f, 0.18f, C(70, 60, 75));
        p.Rect(0f, 0.17f, 1f, 0.185f, C(200, 160, 110));
    }

    private static void Sarande(Painter p)
    {
        p.Grad(C(250, 120, 70), C(120, 60, 130), 0.42f, 1f);
        p.Glow(0.55f, 0.46f, 0.4f, C(255, 220, 140), 0.55f);
        p.Disc(0.55f, 0.46f, 0.11f, C(255, 215, 120));
        p.Ellipse(0.15f, 0.42f, 0.3f, 0.06f, C(110, 60, 110));
        p.Ellipse(0.9f, 0.42f, 0.2f, 0.04f, C(120, 66, 118));
        p.Grad(C(40, 70, 130), C(15, 35, 80), 0f, 0.42f);
        for (int i = 0; i < 10; i++)
        {
            float num = 0.4f - (float)i * 0.04f;
            float num2 = 0.12f - (float)i * 0.006f;
            p.Rect(0.55f - num2, num, 0.55f + num2, num + 0.01f, new Color(1f, 0.8f, 0.5f, 0.6f - (float)i * 0.05f));
        }
        p.Ellipse(0.85f, -0.02f, 0.4f, 0.12f, C(220, 170, 110));
        for (int j = 0; j < 60; j++)
        {
            float num3 = (float)j / 59f;
            p.Disc(0.8f + 0.08f * num3 * num3, 0.05f + 0.6f * num3, 0.018f - 0.006f * num3, C(70, 45, 35));
        }
        Vector2 vector = new Vector2(0.88f, 0.66f);
        for (int k = 0; k < 7; k++)
        {
            float f = ((float)k * 52f + 10f) * (MathF.PI / 180f);
            float num4 = 0.2f;
            Vector2 vector2 = new Vector2(Mathf.Cos(f), Mathf.Sin(f) * 0.6f - 0.25f);
            Vector2 vector3 = new Vector2(0f - vector2.y, vector2.x) * 0.03f;
            p.Tri(vector + vector3, vector - vector3, vector + vector2 * num4, C(35, 80, 45));
        }
    }

    // Prishtinë at dusk: the National Library with its domes and net, the cathedral tower, a minaret,
    // the lamps of the boulevard and the yellow NEWBORN letters
    private static void Prishtine(Painter p)
    {
        p.Grad(C(40, 30, 90), C(245, 150, 95), 0.3f, 1f);
        p.Glow(0.5f, 0.3f, 0.55f, C(255, 170, 110), 0.4f);
        for (int i = 0; i < 18; i++)
            p.Disc(Hash(i, 21), 0.78f + 0.22f * Hash(i, 22), 0.003f, C(255, 240, 210));
        p.Ellipse(0.2f, 0.28f, 0.45f, 0.1f, C(70, 50, 90));
        p.Ellipse(0.82f, 0.28f, 0.4f, 0.08f, C(80, 55, 95));

        // tall blocks of flats behind
        for (int i = 0; i < 11; i++)
        {
            float x = i * 0.095f - 0.02f;
            float h = 0.16f + 0.22f * Hash(i, 23);
            p.Rect(x, 0.22f, x + 0.08f, 0.22f + h, C(58, 52, 84));
            for (int r = 0; r < 6; r++)
                for (int c = 0; c < 3; c++)
                {
                    float wy = 0.25f + r * 0.045f;
                    if (wy > 0.2f + h - 0.03f) continue;
                    if (Hash(i * 31 + r * 3 + c, 24) < 0.5f)
                        p.Rect(x + 0.012f + c * 0.022f, wy, x + 0.024f + c * 0.022f, wy + 0.02f, C(255, 210, 130));
                }
        }

        // the bell tower of the cathedral, and a minaret
        p.Rect(0.11f, 0.22f, 0.18f, 0.78f, C(230, 210, 185));
        p.Tri(new Vector2(0.105f, 0.78f), new Vector2(0.185f, 0.78f), new Vector2(0.145f, 0.92f), C(80, 112, 122));
        p.Rect(0.13f, 0.62f, 0.16f, 0.7f, C(255, 205, 120));
        p.Rect(0.855f, 0.22f, 0.875f, 0.7f, C(240, 236, 226));
        p.Rect(0.845f, 0.6f, 0.885f, 0.615f, C(255, 210, 120));
        p.Tri(new Vector2(0.852f, 0.7f), new Vector2(0.878f, 0.7f), new Vector2(0.865f, 0.77f), C(100, 110, 125));

        // the National Library: white blocks, lit windows, domes on the roof, a metal net over the front
        p.Rect(0.28f, 0.18f, 0.74f, 0.44f, C(226, 226, 220));
        p.Rect(0.3f, 0.26f, 0.72f, 0.34f, C(255, 205, 120));
        float[] domes = { 0.32f, 0.38f, 0.44f, 0.58f, 0.64f, 0.7f };
        for (int i = 0; i < domes.Length; i++)
            p.Ellipse(domes[i], 0.44f, 0.026f + 0.008f * Hash(i, 25), 0.024f, C(236, 236, 230));
        p.Ellipse(0.51f, 0.44f, 0.075f, 0.065f, C(240, 240, 234));
        for (float x = 0.285f; x < 0.74f; x += 0.026f) p.Rect(x, 0.18f, x + 0.004f, 0.44f, C(70, 72, 82));
        for (float y = 0.19f; y < 0.44f; y += 0.026f) p.Rect(0.28f, y, 0.74f, y + 0.004f, C(70, 72, 82));

        // the boulevard and its lamps
        p.Rect(0f, 0f, 1f, 0.18f, C(52, 48, 64));
        for (int i = 0; i < 6; i++)
        {
            float x = 0.06f + i * 0.178f;
            p.Rect(x, 0.04f, x + 0.006f, 0.2f, C(28, 28, 34));
            p.Glow(x + 0.003f, 0.205f, 0.035f, C(255, 220, 150), 0.8f);
            p.Disc(x + 0.003f, 0.205f, 0.009f, C(255, 236, 180));
        }

        // NEWBORN
        float[][][] word =
        {
            new[] { new[] { 0f, 0f, 0f, 1f }, new[] { 1f, 0f, 1f, 1f }, new[] { 0f, 1f, 1f, 0f } },
            new[] { new[] { 0f, 0f, 0f, 1f }, new[] { 0f, 1f, 1f, 1f }, new[] { 0f, 0.5f, 0.8f, 0.5f }, new[] { 0f, 0f, 1f, 0f } },
            new[] { new[] { 0f, 0f, 0f, 1f }, new[] { 1f, 0f, 1f, 1f }, new[] { 0f, 0f, 0.5f, 0.6f }, new[] { 0.5f, 0.6f, 1f, 0f } },
            new[] { new[] { 0f, 0f, 0f, 1f }, new[] { 0f, 1f, 0.85f, 1f }, new[] { 0.85f, 1f, 0.85f, 0.5f }, new[] { 0f, 0.5f, 1f, 0.5f }, new[] { 1f, 0.5f, 1f, 0f }, new[] { 0f, 0f, 1f, 0f } },
            new[] { new[] { 0f, 0f, 0f, 1f }, new[] { 1f, 0f, 1f, 1f }, new[] { 0f, 1f, 1f, 1f }, new[] { 0f, 0f, 1f, 0f } },
            new[] { new[] { 0f, 0f, 0f, 1f }, new[] { 0f, 1f, 1f, 1f }, new[] { 1f, 1f, 1f, 0.5f }, new[] { 0f, 0.5f, 1f, 0.5f }, new[] { 0.35f, 0.5f, 1f, 0f } },
            new[] { new[] { 0f, 0f, 0f, 1f }, new[] { 1f, 0f, 1f, 1f }, new[] { 0f, 1f, 1f, 0f } }
        };
        float lw = 0.07f, lh = 0.11f, gap = 0.018f, t = 0.018f, x0 = 0.2f, y0 = 0.035f;
        for (int li = 0; li < word.Length; li++)
        {
            float lx = x0 + li * (lw + gap);
            foreach (float[] s in word[li])
                Line(p, lx + t * 0.5f + s[0] * (lw - t), y0 + t * 0.5f + s[1] * (lh - t), lx + t * 0.5f + s[2] * (lw - t), y0 + t * 0.5f + s[3] * (lh - t), t, C(255, 205, 30));
        }
    }

    // Ulqin by moonlight: the old town walled on its rock, the tower of the Balshajs, a corsair ship on the moon's path,
    // white houses and a minaret on the hill across the bay
    private static void Ulqin(Painter p)
    {
        p.Grad(C(10, 18, 48), C(45, 75, 115), 0.4f, 1f);
        for (int i = 0; i < 50; i++)
            p.Disc(Hash(i, 31), 0.55f + 0.45f * Hash(i, 32), 0.002f + 0.003f * Hash(i, 33), C(255, 248, 225));
        p.Glow(0.64f, 0.74f, 0.26f, C(200, 215, 240), 0.45f);
        p.Disc(0.64f, 0.74f, 0.07f, C(248, 244, 225));

        // the sea and the moon's path on it
        p.Grad(C(20, 45, 80), C(8, 20, 42), 0f, 0.4f);
        for (int j = 0; j < 11; j++)
        {
            float y = 0.38f - j * 0.034f;
            float hw = 0.025f + j * 0.008f;
            p.Rect(0.64f - hw, y, 0.64f + hw, y + 0.008f, new Color(0.9f, 0.92f, 0.95f, 0.6f - j * 0.045f));
        }

        // the hill across the bay, with houses and a minaret
        p.Ellipse(0.95f, 0.36f, 0.3f, 0.15f, C(24, 38, 50));
        for (int i = 0; i < 9; i++)
        {
            float x = 0.72f + (i % 5) * 0.055f + (i / 5) * 0.025f;
            float y = 0.38f + (i / 5) * 0.06f + 0.01f * Hash(i, 34);
            p.Rect(x, y, x + 0.04f, y + 0.035f, C(232, 228, 216));
            p.Tri(new Vector2(x - 0.004f, y + 0.035f), new Vector2(x + 0.044f, y + 0.035f), new Vector2(x + 0.02f, y + 0.055f), C(170, 80, 60));
            if (Hash(i, 35) < 0.6f) p.Rect(x + 0.014f, y + 0.01f, x + 0.026f, y + 0.024f, C(255, 205, 120));
        }
        p.Rect(0.9f, 0.4f, 0.915f, 0.6f, C(238, 234, 224));
        p.Rect(0.893f, 0.55f, 0.922f, 0.56f, C(255, 210, 120));
        p.Tri(new Vector2(0.898f, 0.6f), new Vector2(0.917f, 0.6f), new Vector2(0.9075f, 0.65f), C(100, 110, 125));

        // the rock of the old town and its walls, houses and tower
        p.Rect(0f, 0.28f, 0.36f, 0.5f, C(82, 74, 70));
        p.Ellipse(0.36f, 0.36f, 0.08f, 0.13f, C(82, 74, 70));
        p.Ellipse(0.18f, 0.29f, 0.24f, 0.04f, C(60, 54, 52));
        for (int i = 0; i < 6; i++)
        {
            float x = 0.12f + i * 0.04f;
            float h = 0.06f + 0.03f * Hash(i, 36);
            p.Rect(x, 0.54f, x + 0.034f, 0.54f + h, C(214, 200, 175));
            p.Tri(new Vector2(x - 0.004f, 0.54f + h), new Vector2(x + 0.038f, 0.54f + h), new Vector2(x + 0.017f, 0.56f + h), C(160, 76, 58));
            if (Hash(i, 37) < 0.7f) p.Rect(x + 0.012f, 0.555f, x + 0.022f, 0.57f, C(255, 205, 120));
        }
        p.Rect(0.02f, 0.5f, 0.38f, 0.56f, C(205, 190, 165));
        for (int i = 0; i < 9; i++) p.Rect(0.03f + i * 0.04f, 0.56f, 0.05f + i * 0.04f, 0.575f, C(205, 190, 165));
        p.Rect(0.03f, 0.5f, 0.09f, 0.72f, C(196, 180, 156));
        for (int i = 0; i < 3; i++) p.Rect(0.03f + i * 0.024f, 0.72f, 0.042f + i * 0.024f, 0.74f, C(196, 180, 156));
        p.Rect(0.052f, 0.64f, 0.066f, 0.67f, C(255, 205, 120));
        for (int i = 0; i < 5; i++)
            p.Rect(0.06f + i * 0.06f, 0.08f + (i % 2) * 0.05f, 0.072f + i * 0.06f, 0.24f, new Color(1f, 0.75f, 0.38f, 0.35f));

        // a corsair ship on the moon's path
        Color dark = C(22, 16, 18);
        p.Tri(new Vector2(0.53f, 0.27f), new Vector2(0.76f, 0.27f), new Vector2(0.56f, 0.22f), dark);
        p.Tri(new Vector2(0.56f, 0.22f), new Vector2(0.76f, 0.27f), new Vector2(0.73f, 0.22f), dark);
        p.Rect(0.73f, 0.27f, 0.77f, 0.3f, dark);
        Line(p, 0.53f, 0.27f, 0.49f, 0.3f, 0.005f, dark);
        for (int m = 0; m < 2; m++)
        {
            float mx = 0.6f + m * 0.09f;
            p.Rect(mx - 0.003f, 0.27f, mx + 0.003f, 0.52f, dark);
            p.Rect(mx - 0.035f, 0.31f, mx + 0.035f, 0.41f, C(48, 44, 50));
            p.Rect(mx - 0.026f, 0.43f, mx + 0.026f, 0.49f, C(48, 44, 50));
        }
        p.Disc(0.775f, 0.31f, 0.008f, C(255, 210, 120));
    }

    /// <summary>A straight stroke of thickness t from (x0, y0) to (x1, y1), with square ends.</summary>
    private static void Line(Painter p, float x0, float y0, float x1, float y1, float t, Color c)
    {
        Vector2 a = new Vector2(x0, y0), b = new Vector2(x1, y1);
        Vector2 d = (b - a).normalized;
        Vector2 n = new Vector2(0f - d.y, d.x) * (t * 0.5f);
        a -= d * (t * 0.5f);
        b += d * (t * 0.5f);
        p.Tri(a + n, b + n, b - n, c);
        p.Tri(a + n, b - n, a - n, c);
    }

    private static void Peak(Painter p, float x0, float yBase, float x1, float yTop, float lean, Color c, bool snow)
    {
        float x2 = Mathf.Lerp(x0, x1, Mathf.Clamp(0.5f + (lean - 0.9f) * 0.25f, 0.3f, 0.7f));
        Vector2 vector = new Vector2(x0, yBase);
        Vector2 b = new Vector2(x1, yBase);
        Vector2 vector2 = new Vector2(x2, yTop);
        p.Tri(vector, b, vector2, c);
        if (snow)
        {
            p.Tri(Vector2.Lerp(vector2, vector, 0.28f), Vector2.Lerp(vector2, b, 0.28f), vector2, C(240, 244, 252));
        }
    }

    private static float Hash(int i, int salt)
    {
        int num = i * 374761393 + salt * 668265263;
        int num2 = (num ^ (num >> 13)) * 1274126177;
        return (float)((num2 ^ (num2 >> 16)) & 0xFFFF) / 65535f;
    }
}
