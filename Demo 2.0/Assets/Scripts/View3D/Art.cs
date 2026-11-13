using System;

/// <summary>
/// Textures drawn in code for the Albanian theme, drawn from formulas (no image files): the double-headed eagle and
/// kilim (woven rug) patterns. Pure math returning RGBA bytes, so it can be checked outside Unity.
/// </summary>
public static class Art
{
    // palette: kilim colours
    static readonly byte[][] Pal =
    {
        new byte[] { 18, 16, 20 },       // 0 black
        new byte[] { 196, 18, 40 },      // 1 red
        new byte[] { 236, 224, 196 },    // 2 cream
        new byte[] { 214, 168, 58 },     // 3 gold
        new byte[] { 120, 10, 26 }       // 4 dark red
    };

    // ---------------------------------------------------------------------------------------
    // distance helpers (negative = inside)
    // ---------------------------------------------------------------------------------------

    static float Capsule(float u, float v, float ax, float ay, float bx, float by, float r0, float r1)
    {
        float dx = bx - ax, dy = by - ay;
        float len2 = dx * dx + dy * dy;
        float t = len2 <= 0f ? 0f : ((u - ax) * dx + (v - ay) * dy) / len2;
        if (t < 0f) t = 0f;
        if (t > 1f) t = 1f;
        float px = ax + dx * t, py = ay + dy * t;
        float r = r0 + (r1 - r0) * t;
        float ex = u - px, ey = v - py;
        return (float)Math.Sqrt(ex * ex + ey * ey) - r;
    }

    static float Ellipse(float u, float v, float cx, float cy, float rx, float ry)
    {
        float x = (u - cx) / rx, y = (v - cy) / ry;
        return ((float)Math.Sqrt(x * x + y * y) - 1f) * Math.Min(rx, ry);
    }

    static float SegDist(float u, float v, float ax, float ay, float bx, float by)
    {
        float dx = bx - ax, dy = by - ay;
        float len2 = dx * dx + dy * dy;
        float t = len2 <= 0f ? 0f : ((u - ax) * dx + (v - ay) * dy) / len2;
        if (t < 0f) t = 0f;
        if (t > 1f) t = 1f;
        float ex = u - (ax + dx * t), ey = v - (ay + dy * t);
        return (float)Math.Sqrt(ex * ex + ey * ey);
    }

    static float Triangle(float u, float v, float ax, float ay, float bx, float by, float cx, float cy)
    {
        float d = Math.Min(SegDist(u, v, ax, ay, bx, by), Math.Min(SegDist(u, v, bx, by, cx, cy), SegDist(u, v, cx, cy, ax, ay)));
        float s1 = (bx - ax) * (v - ay) - (by - ay) * (u - ax);
        float s2 = (cx - bx) * (v - by) - (cy - by) * (u - bx);
        float s3 = (ax - cx) * (v - cy) - (ay - cy) * (u - cx);
        bool inside = (s1 >= 0 && s2 >= 0 && s3 >= 0) || (s1 <= 0 && s2 <= 0 && s3 <= 0);
        return inside ? -d : d;
    }

    // ---------------------------------------------------------------------------------------
    // the double-headed eagle
    // ---------------------------------------------------------------------------------------

    /// <summary>Signed distance to the eagle silhouette at (u, v), both in -1..1 (v up). Negative means inside.</summary>
    public static float Eagle(float u, float v)
    {
        float a = Math.Abs(u);
        float d = 9f;

        // body and legs
        d = Math.Min(d, Ellipse(a, v, 0f, -0.04f, 0.20f, 0.34f));
        d = Math.Min(d, Capsule(a, v, 0.10f, -0.26f, 0.22f, -0.52f, 0.055f, 0.04f));     // leg
        d = Math.Min(d, Capsule(a, v, 0.22f, -0.52f, 0.34f, -0.64f, 0.028f, 0.018f));    // talons
        d = Math.Min(d, Capsule(a, v, 0.22f, -0.52f, 0.25f, -0.69f, 0.028f, 0.018f));
        d = Math.Min(d, Capsule(a, v, 0.22f, -0.52f, 0.14f, -0.66f, 0.028f, 0.018f));

        // tail: a fan of feathers
        float[] tailAng = { 0f, 11f, 22f, 33f };
        for (int i = 0; i < tailAng.Length; i++)
        {
            float r = tailAng[i] * 0.0174532925f;
            d = Math.Min(d, Capsule(a, v, 0.04f * (float)Math.Sin(r), -0.28f, 0.52f * (float)Math.Sin(r), -0.28f - 0.50f * (float)Math.Cos(r), 0.075f, 0.035f));
        }

        // necks, heads, beaks (the beaks point away from each other)
        d = Math.Min(d, Capsule(a, v, 0.07f, 0.22f, 0.15f, 0.44f, 0.085f, 0.060f));
        d = Math.Min(d, Ellipse(a, v, 0.175f, 0.52f, 0.085f, 0.075f));
        d = Math.Min(d, Triangle(a, v, 0.235f, 0.575f, 0.385f, 0.515f, 0.245f, 0.485f));    // upper beak
        d = Math.Min(d, Triangle(a, v, 0.235f, 0.485f, 0.350f, 0.485f, 0.265f, 0.445f));    // lower beak

        // wings: a thick inner mass plus five long feathers fanning up and out, kept clear of the heads
        d = Math.Min(d, Capsule(a, v, 0.18f, 0.10f, 0.60f, 0.26f, 0.12f, 0.075f));
        d = Math.Min(d, Capsule(a, v, 0.20f, 0.18f, 0.52f, 0.46f, 0.085f, 0.06f));
        float sx = 0.20f, sy = 0.10f;
        float[] ang = { 1f, 15f, 29f, 43f, 56f };
        float[] len = { 0.78f, 0.82f, 0.76f, 0.66f, 0.50f };
        for (int i = 0; i < ang.Length; i++)
        {
            float r = ang[i] * 0.0174532925f;
            float ex = sx + (float)Math.Cos(r) * len[i], ey = sy + (float)Math.Sin(r) * len[i];
            d = Math.Min(d, Capsule(a, v, sx, sy, ex, ey, 0.068f, 0.030f));
        }

        // carved details (negative space): eyes and chest feathers
        float eye = Ellipse(a, v, 0.195f, 0.53f, 0.020f, 0.020f);
        d = Math.Max(d, -eye);
        for (int i = 0; i < 3; i++)
        {
            float y = 0.10f - i * 0.13f;
            float line = SegDist(a, v, 0.00f, y, 0.15f - i * 0.02f, y - 0.05f) - 0.011f;
            d = Math.Max(d, -line);
        }
        return d;
    }

    /// <summary>RGBA pixels (row 0 = bottom) of the eagle in the given colour on transparent, n x n.</summary>
    public static byte[] EagleRGBA(int n, byte r, byte g, byte b)
    {
        var px = new byte[n * n * 4];
        float p = 2f / n;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n * 2f - 1f;
                float v = (y + 0.5f) / n * 2f - 1f;
                float cov = 0.5f - Eagle(u, v) / p;
                if (cov < 0f) cov = 0f;
                if (cov > 1f) cov = 1f;
                int i = (y * n + x) * 4;
                px[i] = r; px[i + 1] = g; px[i + 2] = b; px[i + 3] = (byte)(cov * 255f);
            }
        return px;
    }

    // ---------------------------------------------------------------------------------------
    // kilim patterns
    // ---------------------------------------------------------------------------------------

    static float Frac(float x) { return x - (float)Math.Floor(x); }

    /// <summary>
    /// One kilim band: u runs along the band (one repeat per 1.0), v goes across (0..1).
    /// Returns a palette index: borders, zigzag teeth, then a row of nested diamonds.
    /// </summary>
    public static int Band(float u, float v)
    {
        float x = Frac(u);
        if (v < 0.07f || v > 0.93f) return 0;
        if (v < 0.12f || v > 0.88f) return 3;

        // zigzag teeth along both edges
        float tri = x < 0.5f ? x * 2f : (1f - x) * 2f;            // 0..1..0
        if (v < 0.22f) return (v - 0.12f) / 0.10f < tri ? 2 : 4;
        if (v > 0.78f) return (0.88f - v) / 0.10f < tri ? 2 : 4;
        if (v < 0.25f || v > 0.75f) return 0;

        // field: nested diamonds
        float cx = Math.Abs(x - 0.5f) * 2f;                         // 0 centre .. 1 edge of the cell
        float cy = Math.Abs((v - 0.25f) / 0.50f - 0.5f) * 2f;       // 0 middle .. 1 edge
        float m = cx * 0.72f + cy;                                  // diamond distance (wider cell than tall)
        if (m < 0.22f) return 3;
        if (m < 0.40f) return 0;
        if (m < 0.62f) return 2;
        if (m < 0.82f) return 1;
        return (cx > 0.80f && cy > 0.35f) ? 3 : 4;
    }

    static void Put(byte[] px, int i, int pal, byte alpha)
    {
        px[i] = Pal[pal][0]; px[i + 1] = Pal[pal][1]; px[i + 2] = Pal[pal][2]; px[i + 3] = alpha;
    }

    /// <summary>
    /// Anti-aliased drawing: every pixel averages ss x ss samples of a palette function (-1 = transparent), so the
    /// edges are smooth instead of stair-stepped.
    /// </summary>
    static byte[] Render(int w, int h, int ss, Func<float, float, int> pal)
    {
        var px = new byte[w * h * 4];
        int total = ss * ss;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int r = 0, g = 0, b = 0, n = 0;
                for (int sy = 0; sy < ss; sy++)
                    for (int sx = 0; sx < ss; sx++)
                    {
                        int p = pal((x + (sx + 0.5f) / ss) / w, (y + (sy + 0.5f) / ss) / h);
                        if (p < 0) continue;
                        r += Pal[p][0]; g += Pal[p][1]; b += Pal[p][2]; n++;
                    }
                int i = (y * w + x) * 4;
                if (n == 0) { px[i] = Pal[0][0]; px[i + 1] = Pal[0][1]; px[i + 2] = Pal[0][2]; px[i + 3] = 0; }
                else { px[i] = (byte)(r / n); px[i + 1] = (byte)(g / n); px[i + 2] = (byte)(b / n); px[i + 3] = (byte)(255 * n / total); }
            }
        return px;
    }

    /// <summary>A long horizontal kilim strip (for the wall and flags), repeated `repeats` times across.</summary>
    public static byte[] StripRGBA(int w, int h, int repeats)
    {
        return Render(w, h, 3, delegate (float u, float v) { return Band(u * repeats, v); });
    }

    /// <summary>
    /// A round kilim band for the table rim: only the ring between r0 and r1 (fractions of the radius) is opaque.
    /// </summary>
    public static byte[] RingRGBA(int n, float r0, float r1, int repeats)
    {
        return Render(n, n, 2, delegate (float fu, float fv)
        {
            float u = fu * 2f - 1f, v = fv * 2f - 1f;
            float r = (float)Math.Sqrt(u * u + v * v);
            if (r < r0 || r > r1) return -1;
            float ang = (float)Math.Atan2(v, u) / (2f * (float)Math.PI);
            return Band(ang * repeats, (r - r0) / (r1 - r0));
        });
    }

    /// <summary>A square rug: red field with nested diamonds, kilim borders around it.</summary>
    public static byte[] RugRGBA(int n)
    {
        return Render(n, n, 2, delegate (float fu, float fv)
        {
            float u = fu * 2f - 1f, v = fv * 2f - 1f;
            float au = Math.Abs(u), av = Math.Abs(v);
            float edge = Math.Max(au, av);                       // 0 centre .. 1 edge
            if (edge > 0.82f)
            {
                // border band: pattern runs along the nearest side
                float along = au > av ? v : u;
                return Band(along * 9f, (1f - edge) / 0.18f);
            }
            float m = au + av;                                   // diamond distance
            if (m < 0.10f) return 3;
            if (m < 0.16f) return 0;
            if (m > 0.30f && m < 0.36f) return 2;
            if (m > 0.50f && m < 0.55f) return 0;
            if (m > 0.70f && m < 0.78f) return 3;
            if (m > 0.96f && m < 1.02f) return 2;
            if (m > 0.20f && m < 0.28f && (((int)(Math.Atan2(v, u) * 8f / Math.PI + 16f)) % 2 == 0)) return 4;
            if (m > 0.40f && m < 0.48f && (((int)(Math.Atan2(v, u) * 12f / Math.PI + 24f)) % 2 == 0)) return 4;
            return 1;
        });
    }

    // ---------------------------------------------------------------------------------------
    // materials: wood, stone, roof tiles, plaster (tileable, so they can repeat over a wall)
    // ---------------------------------------------------------------------------------------

    static float Hash(int x, int y, int seed)
    {
        unchecked
        {
            int h = x * 374761393 + y * 668265263 + seed * 1274126177;
            h = (h ^ (h >> 13)) * 1274126177;
            h ^= h >> 16;
            return (h & 0xffffff) / 16777216f;
        }
    }

    static int Wrap(int a, int p) { int m = a % p; return m < 0 ? m + p : m; }

    /// <summary>Smooth noise that repeats every px x py cells, so textures tile without seams.</summary>
    static float Noise(float x, float y, int px, int py, int seed)
    {
        int x0 = (int)Math.Floor(x), y0 = (int)Math.Floor(y);
        float fx = x - x0, fy = y - y0;
        fx = fx * fx * (3f - 2f * fx);
        fy = fy * fy * (3f - 2f * fy);
        float a = Hash(Wrap(x0, px), Wrap(y0, py), seed), b = Hash(Wrap(x0 + 1, px), Wrap(y0, py), seed);
        float c = Hash(Wrap(x0, px), Wrap(y0 + 1, py), seed), d = Hash(Wrap(x0 + 1, px), Wrap(y0 + 1, py), seed);
        return (a + (b - a) * fx) + ((c + (d - c) * fx) - (a + (b - a) * fx)) * fy;
    }

    static float Smooth(float a, float b, float t)
    {
        t = (t - a) / (b - a);
        if (t < 0f) t = 0f;
        if (t > 1f) t = 1f;
        return t * t * (3f - 2f * t);
    }

    static byte B(float v) { return (byte)(Math.Max(0f, Math.Min(1f, v)) * 255f); }

    static void Rgb(byte[] px, int i, float r, float g, float b)
    {
        px[i] = B(r); px[i + 1] = B(g); px[i + 2] = B(b); px[i + 3] = 255;
    }

    /// <summary>Wooden planks running left to right, with grain, knots and dark seams. Mid brown; tint it with the material colour.</summary>
    public static byte[] WoodRGBA(int n, int planks)
    {
        var px = new byte[n * n * 4];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n, v = (y + 0.5f) / n;
                float pv = v * planks;
                int plank = (int)Math.Floor(pv);
                float local = pv - plank;
                float shift = Hash(plank, 3, 11);                                  // each plank is cut at a different place
                float uu = u + shift;
                float tint = 0.82f + 0.30f * Hash(plank, 1, 5);
                float grain = Noise(uu * 5f, pv * 16f, 5, planks * 16, 21) * 0.6f + Noise(uu * 11f, pv * 40f, 11, planks * 40, 22) * 0.4f;
                float ring = 0.5f + 0.5f * (float)Math.Sin((grain * 9f + pv * 3f) * 3.1416f);
                float k = tint * (0.72f + 0.38f * grain + 0.10f * ring);
                // a knot now and then
                float kx = Hash(plank, 7, 31) , ky = 0.5f;
                float dx = (Math.Abs(uu % 1f - kx)) * 6f, dy = (local - ky) * 1.6f;
                if (Hash(plank, 9, 41) > 0.55f) k *= 1f - 0.35f * (1f - Smooth(0f, 0.5f, (float)Math.Sqrt(dx * dx + dy * dy)));
                // seams between planks and end joints
                float seam = Math.Min(local, 1f - local);
                k *= 0.35f + 0.65f * Smooth(0.0f, 0.045f, seam);
                float joint = Math.Abs((uu % 1f) - Hash(plank, 5, 51));
                k *= 0.45f + 0.55f * Smooth(0.0f, 0.006f, joint);
                Rgb(px, (y * n + x) * 4, 0.60f * k, 0.38f * k, 0.20f * k);
            }
        return px;
    }

    /// <summary>Courses of rough stone blocks with grey mortar between them (the castles, the old town of Ulqin).</summary>
    public static byte[] StoneRGBA(int n)
    {
        var px = new byte[n * n * 4];
        const int rows = 8;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n, v = (y + 0.5f) / n;
                float rv = v * rows;
                int row = (int)Math.Floor(rv);
                float lv = rv - row;
                int cnt = 3 + (int)(Hash(row, 0, 1) * 3f);                       // 3 to 5 blocks in this course
                float uu = u - Hash(row, 1, 2);
                uu -= (float)Math.Floor(uu);
                float bu = uu * cnt;
                int block = (int)Math.Floor(bu);
                float lu = bu - block;
                float tint = 0.78f + 0.34f * Hash(block, row, 7);
                float speck = Noise(u * 48f, v * 48f, 48, 48, 9) * 0.25f + Noise(u * 16f, v * 16f, 16, 16, 8) * 0.25f;
                float k = tint * (0.80f + speck);
                // bevel: blocks are lighter in the middle and darker toward the joints
                float edge = Math.Min(Math.Min(lu * cnt * 0.5f, (1f - lu) * cnt * 0.5f), Math.Min(lv * 1.6f, (1f - lv) * 1.6f));
                k *= 0.70f + 0.30f * Smooth(0f, 0.18f, edge);
                float mortar = 1f - Smooth(0.0f, 0.05f, Math.Min(Math.Min(lu * cnt * 0.12f, (1f - lu) * cnt * 0.12f), Math.Min(lv * 0.5f, (1f - lv) * 0.5f)) * 1.0f);
                float rr = 0.62f * k, gg = 0.60f * k, bb = 0.58f * k;
                rr += (0.30f - rr) * mortar; gg += (0.29f - gg) * mortar; bb += (0.28f - bb) * mortar;
                Rgb(px, (y * n + x) * 4, rr, gg, bb);
            }
        return px;
    }

    /// <summary>Terracotta roof tiles in overlapping scalloped rows.</summary>
    public static byte[] RoofTileRGBA(int n)
    {
        var px = new byte[n * n * 4];
        const int rows = 8, cols = 6;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n, v = (y + 0.5f) / n;
                float rv = v * rows;
                int row = (int)Math.Floor(rv);
                float tv = rv - row;
                float cu = u * cols + ((row & 1) == 1 ? 0.5f : 0f);
                int col = (int)Math.Floor(cu);
                float tu = cu - col;
                float tint = 0.80f + 0.35f * Hash(col, row, 3);
                // the rounded lower end of each tile
                float curve = 0.20f * (1f - (2f * tu - 1f) * (2f * tu - 1f));
                float d = tv - curve;
                float shade = 0.50f + 0.50f * Smooth(0f, 0.55f, d);
                float edge = Smooth(0.0f, 0.04f, d) * Smooth(0f, 0.06f, Math.Min(tu, 1f - tu) + 0.02f);
                float k = tint * shade * (0.55f + 0.45f * edge) * (0.92f + 0.14f * Noise(u * 40f, v * 40f, 40, 40, 4));
                Rgb(px, (y * n + x) * 4, 0.86f * k, 0.40f * k, 0.24f * k);
            }
        return px;
    }

    /// <summary>Whitewashed plaster with soft blotches and the odd hairline crack (Berat, Sarandë, the walls of the oda).</summary>
    public static byte[] PlasterRGBA(int n)
    {
        var px = new byte[n * n * 4];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n, v = (y + 0.5f) / n;
                float k = 0.90f + 0.10f * Noise(u * 6f, v * 6f, 6, 6, 61) + 0.06f * Noise(u * 28f, v * 28f, 28, 28, 62) - 0.05f;
                Rgb(px, (y * n + x) * 4, 0.95f * k, 0.92f * k, 0.86f * k);
            }
        return px;
    }
}
