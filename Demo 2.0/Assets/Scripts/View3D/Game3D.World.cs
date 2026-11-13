using System.Collections.Generic;
using UnityEngine;
using PokerDemo.Core;

/// <summary>The room: a table with a kilim rim, a rug, lights, and a place to play in (the oda, one of the cities, or the coast).</summary>
public partial class Game3D
{
    GameObject _back;
    Renderer _feltR, _feltInnerR, _floorR;
    Light _fireLight;
    int _city = -2;            // the map that is built right now (-1 oda, 0 Shkodër, 1 Berat, 2 Prishtinë, 3 Tiranë, 4 Sarandë, 5 Ulqin)
    int _face;                 // which side of the room is being built (0 = in front of the player, then 90, 180 and 270 degrees round)
    static bool _lite;         // true for the side that is behind the player: no window lights, it is rarely seen
    int _map = 0;              // the map of every mode except the tournament: Shkodër (the lake and Rozafa castle) behind the menu; "Harta" changes it in a game
    readonly Dictionary<int, Texture2D> _skyCache = new Dictionary<int, Texture2D>();

    // ---------------------------------------------------------------------------------------
    // small builders for the backdrop (nothing in the backdrop casts or receives shadows)
    // ---------------------------------------------------------------------------------------

    static GameObject Bx(Transform p, PrimitiveType type, float x, float y, float z, float sx, float sy, float sz, Material m, string name)
    {
        GameObject g = Prim.Make(type, p, new Vector3(x, y, z), new Vector3(sx, sy, sz), m, name);
        MeshKit.MakeLowPoly(g);                 // the backdrop is far away: no need for 2000-vertex spheres
        Renderer r = g.GetComponent<Renderer>();
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return g;
    }

    /// <summary>A quad that faces the camera (a wall decoration).</summary>
    static GameObject Wq(Transform p, float x, float y, float z, float w, float h, Material m)
    {
        return Bx(p, PrimitiveType.Quad, x, y, z, w, h, 1f, m, "Quad");
    }

    static GameObject Tilted(Transform p, PrimitiveType type, float x, float y, float z, float sx, float sy, float sz, float rotZ, Material m, string name)
    {
        GameObject g = Bx(p, type, x, y, z, sx, sy, sz, m, name);
        g.transform.localRotation = Quaternion.Euler(0f, 0f, rotZ);
        return g;
    }

    static GameObject Cn(Transform p, float x, float y, float z, float sx, float sy, float sz, Material m, string name)
    {
        GameObject g = Prim.Cone(p, new Vector3(x, y, z), new Vector3(sx, sy, sz), m, name);
        g.GetComponent<MeshFilter>().sharedMesh = MeshKit.ConeLo;
        Renderer r = g.GetComponent<Renderer>();
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return g;
    }

    /// <summary>A lit material with a texture that repeats about every `size` metres over a w x h surface.</summary>
    static Material Surf(Texture tex, Color tint, float w, float h, float size, float smooth = 0.08f)
    {
        return Mats.LitTex(tint, tex, w / size, h / size, smooth);
    }

    static bool IsLit(int seed, int a, int b)
    {
        int h = (seed * 73856093) ^ (a * 19349663) ^ (b * 83492791);
        return ((h >> 4) & 255) < 150;
    }

    /// <summary>A building: textured walls, a roof (0 none, 1 pitched, 2 flat slab) and a grid of windows, some lit.</summary>
    static void House(Transform p, Theme.CityLook L, float x, float z, float w, float h, float d, Texture wallTex, Color wallTint, Texture roofTex, Color roofTint, int roofKind, int seed, float windowsFrom)
    {
        Bx(p, PrimitiveType.Cube, x, h * 0.5f, z, w, h, d, Surf(wallTex, wallTint, w, h, 1.8f), "House");
        if (roofKind == 1)
            Tilted(p, PrimitiveType.Cube, x, h, z, w * 0.7071f, w * 0.7071f, d - 0.12f, 45f, Surf(roofTex, roofTint, w * 0.8f, d, 1.3f, 0.2f), "Roof");   // shallower than the walls, so the lower half stays hidden inside
        else if (roofKind == 2)
            Bx(p, PrimitiveType.Cube, x, h + 0.05f, z, w + 0.14f, 0.1f, d + 0.14f, Surf(roofTex, roofTint, w, d, 1.3f, 0.2f), "Roof");

        if (_lite) return;
        Material on = Mats.Unlit(L.Window), off = Mats.Unlit(new Color(0.06f, 0.06f, 0.09f));
        int cols = Mathf.Max(1, (int)(w / 0.75f));
        float cw = w / cols;
        int row = 0;
        for (float y = windowsFrom + 0.4f; y < h - 0.25f; y += 0.8f, row++)
            for (int c = 0; c < cols; c++)
                Wq(p, x - w * 0.5f + cw * (c + 0.5f), y, z - d * 0.5f - 0.02f, 0.28f, 0.46f, IsLit(seed, c, row) ? on : off);
    }

    /// <summary>A fortress wall with merlons, two round towers, a gate and a flag.</summary>
    static void Castle(Transform p, float x, float baseY, float z, float s, Color stone)
    {
        Material m = Surf(Theme.Stone, stone, 5f * s, 1.3f * s, 1.6f, 0.05f);
        Material round = Mats.Lit(Color.Lerp(stone, new Color(0.5f, 0.5f, 0.5f), 0.4f), 0.05f);
        Material dark = Mats.Unlit(new Color(0.05f, 0.05f, 0.08f));
        float W = 5f * s;
        Bx(p, PrimitiveType.Cube, x, baseY + 0.65f * s, z, W, 1.3f * s, 1.1f * s, m, "Wall");
        int n = 11;
        for (int i = 0; i < n; i++)
            Bx(p, PrimitiveType.Cube, x - W * 0.5f + (i + 0.5f) * W / n, baseY + 1.3f * s + 0.14f * s, z - 0.4f * s, 0.28f * s, 0.28f * s, 0.3f * s, round, "Merlon");
        for (int side = -1; side <= 1; side += 2)
        {
            float tx = x + side * W * 0.5f;
            Bx(p, PrimitiveType.Cylinder, tx, baseY + 1.1f * s, z, 0.9f * s, 1.1f * s, 0.9f * s, round, "Tower");
            Bx(p, PrimitiveType.Cylinder, tx, baseY + 2.2f * s + 0.05f * s, z, 1.05f * s, 0.06f * s, 1.05f * s, Mats.Lit(Color.Lerp(stone, Color.black, 0.45f), 0.1f), "TowerCap");
            Cn(p, tx, baseY + 2.2f * s + 0.5f * s, z, 1.0f * s, 0.45f * s, 1.0f * s, Mats.Lit(new Color(0.45f, 0.2f, 0.15f), 0.1f), "TowerRoof");
        }
        Wq(p, x, baseY + 0.4f * s, z - 0.56f * s, 0.7f * s, 0.8f * s, dark);
        Bx(p, PrimitiveType.Sphere, x, baseY + 0.8f * s, z - 0.56f * s, 0.7f * s, 0.7f * s, 0.04f, dark, "Gate");
    }

    static void Mountain(Transform p, float x, float z, float size, Color color, bool snow)
    {
        Tilted(p, PrimitiveType.Cube, x, 0f, z, size, size, 6f, 45f, Mats.Lit(color, 0.05f), "Mountain");
        if (!snow) return;
        float c = size * 0.34f;
        Tilted(p, PrimitiveType.Cube, x, 0.7071f * (size - c), z - 0.25f, c, c, 6.4f, 45f, Mats.Lit(new Color(0.92f, 0.94f, 0.98f), 0.1f), "Snow");
    }

    /// <summary>A pine: a trunk and three stacked cones.</summary>
    static void Pine(Transform p, float x, float z, float h, Color leaf)
    {
        Material m = Mats.Lit(leaf, 0.05f);
        Bx(p, PrimitiveType.Cylinder, x, h * 0.08f, z, h * 0.05f, h * 0.08f, h * 0.05f, Mats.Lit(new Color(0.25f, 0.15f, 0.08f), 0.05f), "Trunk");
        for (int i = 0; i < 3; i++)
        {
            float w = h * (0.42f - i * 0.09f);
            Cn(p, x, h * (0.30f + i * 0.24f), z, w, h * 0.28f, w, m, "Pine");
        }
    }

    /// <summary>A palm frond: a flat green capsule that leaves the top of the trunk in a given direction.</summary>
    static void Frond(Transform p, Vector3 top, float azimuth, float elevation, float length, Material m)
    {
        float az = azimuth * Mathf.Deg2Rad, el = elevation * Mathf.Deg2Rad;
        var d = new Vector3(Mathf.Sin(az) * Mathf.Cos(el), Mathf.Sin(el), Mathf.Cos(az) * Mathf.Cos(el));
        GameObject f = Bx(p, PrimitiveType.Capsule, top.x + d.x * length * 0.5f, top.y + d.y * length * 0.5f, top.z + d.z * length * 0.5f, 0.10f, length * 0.5f, 0.02f, m, "Frond");
        f.transform.localRotation = Quaternion.LookRotation(d, Vector3.up) * Quaternion.Euler(90f, 0f, 0f);
    }

    static void Palm(Transform p, float x, float z, float h, float lean)
    {
        Material trunk = Mats.Lit(new Color(0.42f, 0.30f, 0.20f), 0.05f), leaf = Mats.Lit(new Color(0.16f, 0.40f, 0.18f), 0.15f);
        GameObject t = Bx(p, PrimitiveType.Cylinder, x + lean * 0.5f, h * 0.5f, z, 0.30f, h * 0.5f, 0.30f, trunk, "Trunk");
        t.transform.localRotation = Quaternion.Euler(0f, 0f, -lean * 12f);
        var top = new Vector3(x + lean, h, z);
        for (int i = 0; i < 9; i++)
            Frond(p, top, i * 40f, (i % 2 == 0) ? 35f : -20f, 1.7f, leaf);
    }

    /// <summary>A flag in the colours of Albania on a bar: a red field with the black eagle and a kilim hem. Returns the root, which sways.</summary>
    Transform Flag(Transform p, float x, float yCenter, float z, float w, float h, bool gold)
    {
        var root = new GameObject("Flag");
        root.transform.SetParent(p, false);
        root.transform.localPosition = new Vector3(x, yCenter + h * 0.5f + 0.06f, z);
        Transform r = root.transform;
        float cy = -(h * 0.5f + 0.06f);          // the flag's centre relative to the bar
        Bx(r, PrimitiveType.Cube, 0f, 0f, 0.03f, w + 0.25f, 0.09f, 0.12f, Mats.Unlit(Theme.Gold), "FlagBar");
        if (gold) Wq(r, 0f, cy, 0.02f, w + 0.22f, h + 0.22f, Mats.Unlit(Theme.Gold));
        Wq(r, 0f, cy, 0.01f, w, h, Mats.Unlit(Theme.Red));
        Wq(r, 0f, cy + h * 0.04f, 0f, w * 0.78f, w * 0.78f, Mats.Unlit(Theme.Black, Theme.Eagle));
        float hem = Mathf.Min(0.32f, h * 0.1f);
        float tile = w / (hem * 1.33f) / 24f;
        Wq(r, 0f, cy - h * 0.5f + hem * 0.5f + 0.03f, -0.01f, w, hem, Mats.UnlitTiled(Color.white, Theme.Strip, tile, 1f));
        Wq(r, 0f, cy + h * 0.5f - hem * 0.5f - 0.03f, -0.01f, w, hem, Mats.UnlitTiled(Color.white, Theme.Strip, tile, 1f));
        _sways.Add(new Sway { T = r, AroundY = true, Amp = 3.5f, Speed = 1.1f + (x * 0.13f), Phase = x });
        return r;
    }

    // ---------------------------------------------------------------------------------------
    // the room
    // ---------------------------------------------------------------------------------------

    void BuildWorld()
    {
        _world = new GameObject("Game3D World");
        Transform w = _world.transform;

        // lights: a warm key light with soft shadows, a cool fill from the other side, and a lamp over the table
        var sunGo = new GameObject("Sun");
        sunGo.transform.SetParent(w, false);
        sunGo.transform.rotation = Quaternion.Euler(52f, -28f, 0f);
        Light sun = sunGo.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.intensity = 0.85f;
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = 0.8f;
        sun.shadowBias = 0.03f;
        sun.shadowNormalBias = 0.4f;
        sun.color = new Color(1f, 0.95f, 0.88f);

        var fillGo = new GameObject("Fill");
        fillGo.transform.SetParent(w, false);
        fillGo.transform.rotation = Quaternion.Euler(28f, 150f, 0f);
        Light fill = fillGo.AddComponent<Light>();
        fill.type = LightType.Directional;
        fill.intensity = 0.28f;
        fill.shadows = LightShadows.None;
        fill.color = new Color(0.72f, 0.80f, 1f);

        var spotGo = new GameObject("TableLamp");
        spotGo.transform.SetParent(w, false);
        spotGo.transform.position = new Vector3(0f, 5.0f, 0.2f);
        spotGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        Light spot = spotGo.AddComponent<Light>();
        spot.type = LightType.Spot;
        spot.spotAngle = 95f;
        spot.range = 14f;
        spot.intensity = 3.2f;
        spot.color = new Color(1f, 0.93f, 0.80f);

        // the floor (its material changes with the map) and the rug under the table
        _floorR = Prim.Make(PrimitiveType.Quad, w, new Vector3(0f, 0f, 8f), new Vector3(90f, 90f, 1f), Mats.Lit(new Color(0.1f, 0.06f, 0.07f), 0.15f), "Floor").GetComponent<Renderer>();
        _floorR.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        GameObject rug = Prim.FlatQuad(w, new Vector3(0f, 0.012f, 0.2f), 11.5f, 11.5f, new Color(0.66f, 0.64f, 0.64f), Theme.Rug, true);
        rug.name = "Rug";

        // table: dark wood with a rounded edge, a kilim ring on the rim, felt with a gold eagle
        Material wood = Mats.Lit(new Color(0.20f, 0.12f, 0.07f), 0.3f);
        Material woodDark = Mats.Lit(new Color(0.16f, 0.09f, 0.06f), 0.45f);
        Prim.Make(PrimitiveType.Cylinder, w, new Vector3(0f, 0.43f, 0f), new Vector3(0.9f, 0.43f, 0.9f), wood, "TableLeg");
        Prim.Make(PrimitiveType.Cylinder, w, new Vector3(0f, 0.03f, 0f), new Vector3(2.4f, 0.03f, 2.4f), wood, "TableBase");
        Prim.Make(PrimitiveType.Cylinder, w, new Vector3(0f, 0.86f, 0f), new Vector3(5.0f, 0.08f, 5.0f), woodDark, "TableRim");
        Prim.Torus(w, new Vector3(0f, 0.86f, 0f), 2.5f, 0.085f, woodDark, "TableEdge");
        Prim.FlatQuad(w, new Vector3(0f, 0.943f, 0f), 5.0f, 5.0f, Color.white, Theme.Ring, true).name = "RimKilim";
        _feltR = Prim.Make(PrimitiveType.Cylinder, w, new Vector3(0f, 0.87f, 0f), new Vector3(4.5f, 0.08f, 4.5f), Mats.Lit(Theme.Look(-1).Felt, 0.1f), "TableFelt").GetComponent<Renderer>();
        _feltInnerR = Prim.Make(PrimitiveType.Cylinder, w, new Vector3(0f, 0.872f, 0f), new Vector3(2.7f, 0.08f, 2.7f), Mats.Lit(Theme.Look(-1).FeltInner, 0.1f), "FeltInner").GetComponent<Renderer>();
        Prim.FlatQuad(w, new Vector3(0f, 0.956f, -0.78f), 1.25f, 1.25f, new Color(Theme.Gold.r, Theme.Gold.g, Theme.Gold.b, 0.34f), Theme.Eagle, true).name = "FeltEagle";

        // deck box at the dealer spot, with a small eagle on its lid
        Prim.Make(PrimitiveType.Cube, w, new Vector3(DeckPos.x, TableTop + 0.05f, DeckPos.z), new Vector3(0.52f, 0.1f, 0.72f), Mats.Lit(Theme.DeepRed, 0.3f), "DeckBox");
        Prim.FlatQuad(w, new Vector3(DeckPos.x, TableTop + 0.102f, DeckPos.z), 0.40f, 0.40f, Theme.Gold, Theme.Eagle, true).name = "DeckEagle";

        BuildTableDecor(w);
        BuildHabitProps(w);

        _dyn = new GameObject("Dynamic");
        _dyn.transform.SetParent(w, false);
    }

    /// <summary>Coffee and raki on the rim of the table, as a host would offer a guest.</summary>
    void BuildTableDecor(Transform w)
    {
        Material porcelain = Mats.Lit(new Color(0.96f, 0.95f, 0.92f), 0.6f), coffee = Mats.Lit(new Color(0.12f, 0.06f, 0.03f), 0.7f);
        Material copper = Mats.Lit(new Color(0.72f, 0.38f, 0.20f), 0.6f, 0.7f), gold = Mats.Lit(Theme.Gold, 0.6f, 0.6f);
        Material glass = Mats.Lit(new Color(0.88f, 0.92f, 0.88f), 0.8f), raki = Mats.Lit(new Color(0.96f, 0.92f, 0.78f), 0.8f);

        // a copper coffee pot (xhezve) and two small cups (filxhana) at the left of the far side
        Vector3 c = new Vector3(-1.55f, TableTop + 0.0f, 1.55f);
        Prim.Make(PrimitiveType.Cylinder, w, c + new Vector3(0f, 0.09f, 0f), new Vector3(0.14f, 0.09f, 0.14f), copper, "Xhezve");
        Prim.Make(PrimitiveType.Cylinder, w, c + new Vector3(0f, 0.20f, 0f), new Vector3(0.075f, 0.03f, 0.075f), copper, "XhezveNeck");
        Prim.Make(PrimitiveType.Cube, w, c + new Vector3(0.17f, 0.12f, 0f), new Vector3(0.20f, 0.025f, 0.025f), Mats.Lit(new Color(0.25f, 0.15f, 0.08f), 0.2f), "Handle");
        for (int i = 0; i < 2; i++)
        {
            Vector3 q = new Vector3(-1.2f + i * 0.20f, TableTop, 1.78f - i * 0.18f);
            Prim.Make(PrimitiveType.Cylinder, w, q + new Vector3(0f, 0.045f, 0f), new Vector3(0.065f, 0.045f, 0.065f), porcelain, "Cup");
            Prim.Make(PrimitiveType.Cylinder, w, q + new Vector3(0f, 0.088f, 0f), new Vector3(0.056f, 0.004f, 0.056f), coffee, "Coffee");
            Prim.Make(PrimitiveType.Cylinder, w, q + new Vector3(0f, 0.093f, 0f), new Vector3(0.068f, 0.004f, 0.068f), gold, "CupRim");
            Prim.Make(PrimitiveType.Cylinder, w, q + new Vector3(0f, 0.004f, 0f), new Vector3(0.10f, 0.004f, 0.10f), gold, "Saucer");
        }

        // a bottle of raki and two small glasses at the right of the far side
        Vector3 b = new Vector3(1.55f, TableTop, 1.5f);
        Prim.Make(PrimitiveType.Cylinder, w, b + new Vector3(0f, 0.16f, 0f), new Vector3(0.065f, 0.16f, 0.065f), raki, "RakiBottle");
        Prim.Make(PrimitiveType.Cylinder, w, b + new Vector3(0f, 0.38f, 0f), new Vector3(0.028f, 0.07f, 0.028f), glass, "RakiNeck");
        Prim.Make(PrimitiveType.Cylinder, w, b + new Vector3(0f, 0.46f, 0f), new Vector3(0.032f, 0.015f, 0.032f), copper, "RakiCork");
        for (int i = 0; i < 2; i++)
        {
            Vector3 q = b + new Vector3(0.26f + i * 0.14f, 0f, 0.20f - i * 0.16f);
            Prim.Make(PrimitiveType.Cylinder, w, q + new Vector3(0f, 0.05f, 0f), new Vector3(0.04f, 0.05f, 0.04f), glass, "Glass");
            Prim.Make(PrimitiveType.Cylinder, w, q + new Vector3(0f, 0.075f, 0f), new Vector3(0.035f, 0.02f, 0.035f), raki, "RakiFill");
        }
    }

    // ---------------------------------------------------------------------------------------
    // the backdrop of a map
    // ---------------------------------------------------------------------------------------

    void SetCity(int city)
    {
        if (city == _city && _back != null) return;
        _city = city;
        bool hadOld = _back != null;
        if (_back != null) Destroy(_back);
        _sways.Clear();
        _fireLight = null;
        Theme.CityLook L = Theme.Look(city);
        _back = new GameObject("Backdrop");
        _back.transform.SetParent(_world.transform, false);
        Transform p = _back.transform;
        var rng = new System.Random(1000 + city);

        if (_cam != null) _cam.backgroundColor = L.Sky;
        _feltR.sharedMaterial = Mats.Lit(L.Felt, 0.1f);
        _feltInnerR.sharedMaterial = Mats.Lit(L.FeltInner, 0.1f);
        RenderSettings.ambientLight = L.Style == 0 ? new Color(0.40f, 0.34f, 0.32f) : Color.Lerp(new Color(0.34f, 0.32f, 0.38f), L.Sky, 0.25f);

        // the floor: wooden planks on the oda and on the wooden terraces, paving stones in the cities
        bool planks = L.Style == 0 || L.Style == 5;
        _floorR.sharedMaterial = planks
            ? Mats.LitTex(L.Style == 0 ? new Color(0.78f, 0.62f, 0.50f) : new Color(0.62f, 0.52f, 0.46f), Theme.Wood, 90f / 3.2f, 90f / 3.2f, 0.25f)
            : Mats.LitTex(new Color(Mathf.Min(1f, L.Floor.r * 3.2f + 0.02f), Mathf.Min(1f, L.Floor.g * 3.2f + 0.02f), Mathf.Min(1f, L.Floor.b * 3.2f + 0.02f)), Theme.Stone, 90f / 3.0f, 90f / 3.0f, 0.2f);

        // the room all round: the backdrop is built four times and turned by 90 degrees each time, so you can look anywhere
        for (int face = 0; face < 4; face++)
        {
            var fg = new GameObject("Face" + face);
            fg.transform.SetParent(p, false);
            fg.transform.localRotation = Quaternion.Euler(0f, face * 90f, 0f);
            _face = face;
            _lite = face == 2;
            BuildFace(fg.transform, L, new System.Random(1000 + city + face * 131));
        }
        _face = 0;
        _lite = false;

        // the other tables, where other people play
        BuildAmbient(p, L, new System.Random(77 + city));

        BatchBackdrop();
        Gfx.Settle();
        if (hadOld) Invoke("UnloadUnused", 0.2f);      // once the old backdrop is really gone: free its combined meshes
    }

    void UnloadUnused() { Resources.UnloadUnusedAssets(); }

    /// <summary>
    /// A backdrop is a few thousand small objects that never move. Static batching merges the ones that share a material
    /// into big meshes, so the GPU gets far fewer draw calls. Flags and lanterns (they sway) and people (they move) are left out.
    /// </summary>
    void BatchBackdrop()
    {
        var moving = new HashSet<Transform>();
        for (int i = 0; i < _sways.Count; i++) if (_sways[i].T != null) moving.Add(_sways[i].T);
        var list = new List<GameObject>();
        foreach (MeshRenderer r in _back.GetComponentsInChildren<MeshRenderer>())
        {
            MeshFilter mf = r.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null || !mf.sharedMesh.isReadable) continue;
            if (r.GetComponentInParent<AvatarView>() != null) continue;
            bool sways = false;
            for (Transform t = r.transform; t != null && t != _back.transform; t = t.parent)
                if (moving.Contains(t)) { sways = true; break; }
            if (!sways) list.Add(r.gameObject);
        }
        if (list.Count > 1) StaticBatchingUtility.Combine(list.ToArray(), _back);
    }

    void BuildFace(Transform p, Theme.CityLook L, System.Random rng)
    {
        // the sky
        if (L.Style != 0)
        {
            Texture2D sky;
            if (!_skyCache.TryGetValue(L.Style, out sky) || sky == null) { sky = Theme.Sky(L.Sky, L.SkyTop); _skyCache[L.Style] = sky; }
            Wq(p, 0f, 17f, 52f, 170f, 40f, Mats.Unlit(Color.white, sky));
        }

        switch (L.Style)
        {
            case 1: BuildShkoder(p, L, rng); break;
            case 2: BuildBerat(p, L, rng); break;
            case 3: BuildPrishtine(p, L, rng); break;
            case 4: BuildTirane(p, L, rng); break;
            case 5: BuildSarande(p, L, rng); break;
            case 6: BuildUlqin(p, L, rng); break;
            default: BuildOda(p, L); break;
        }

        // moon and stars over the cities at dusk (Ulqin has its own moon, low over the sea)
        if ((L.Style >= 1 && L.Style <= 4) || L.Style == 6)
        {
            if (_face == 0 && L.Style != 6) Bx(p, PrimitiveType.Sphere, -9.5f, 11.5f, 46f, 2.4f, 2.4f, 0.3f, Mats.Unlit(new Color(0.98f, 0.95f, 0.84f)), "Moon");
            Material star = Mats.Unlit(new Color(1f, 0.95f, 0.8f));
            for (int i = 0; i < 30; i++)
                Bx(p, PrimitiveType.Sphere, (float)(rng.NextDouble() * 70.0 - 35.0), 14f + (float)rng.NextDouble() * 12f, 50f, 0.14f, 0.14f, 0.14f, star, "Star");
        }

        // the flag above the far seat and two smaller ones to the sides
        Flag(p, 0f, 3.35f, 9.0f, 2.3f, 3.1f, true);
        Flag(p, -7.8f, 2.8f, 9.3f, 1.35f, 1.9f, false);
        Flag(p, 7.8f, 2.8f, 9.3f, 1.35f, 1.9f, false);

        // paper lanterns on a wire (or hanging from the beams of the oda)
        float wireY = L.Style == 0 ? 6.4f : 5.1f;
        Material lamp = Mats.Unlit(new Color(1f, 0.80f, 0.42f));
        Material cord = Mats.Unlit(new Color(0.9f, 0.7f, 0.3f));
        if (L.Style != 0) Bx(p, PrimitiveType.Cylinder, 0f, wireY, 7.2f, 0.02f, 14f, 0.02f, cord, "Wire").transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        for (int i = -4; i <= 4; i++)
        {
            if (i == 0) continue;
            float x = i * 2.9f;
            float len = 1.0f + (Mathf.Abs(i) % 2) * 0.35f;
            var root = new GameObject("Lantern");
            root.transform.SetParent(p, false);
            root.transform.localPosition = new Vector3(x, wireY, 7.2f);
            Bx(root.transform, PrimitiveType.Cylinder, 0f, -len * 0.5f, 0f, 0.02f, len * 0.5f, 0.02f, cord, "Cord");
            Bx(root.transform, PrimitiveType.Sphere, 0f, -len - 0.3f, 0f, 0.50f, 0.62f, 0.50f, lamp, "Lantern");
            Bx(root.transform, PrimitiveType.Cylinder, 0f, -len + 0.02f, 0f, 0.20f, 0.03f, 0.20f, Mats.Unlit(Theme.DarkGold), "LanternCap");
            _sways.Add(new Sway { T = root.transform, Amp = 4f, Speed = 0.9f + i * 0.07f, Phase = i + _face * 3f });
        }
    }

    // ---------------------------------------------------------------------------------------
    // the other tables: people play here too
    // ---------------------------------------------------------------------------------------

    static readonly string[] AmbientMen = { "Besnik", "Dritan", "Gezim", "Luan", "Petrit", "Arben", "Flamur", "Altin", "Skënder", "Ilir" };
    static readonly string[] AmbientWomen = { "Rozafa", "Drita", "Vjollca", "Teuta", "Elira", "Lule", "Klea", "Mrika" };

    /// <summary>Six more round tables around the main one, each with players, cards, chips and a rug; and four pots with plants.</summary>
    void BuildAmbient(Transform p, Theme.CityLook L, System.Random rng)
    {
        var root = new GameObject("OtherTables");
        root.transform.SetParent(p, false);
        float[] ang = { 135f, 45f, 180f, 0f, 225f, 315f };
        int[] seats = { 4, 3, 3, 4, 3, 3 };
        Color[] felts =
        {
            L.Felt, new Color(0.10f, 0.28f, 0.42f), new Color(0.40f, 0.08f, 0.14f), L.Felt, new Color(0.14f, 0.34f, 0.22f), new Color(0.30f, 0.16f, 0.36f)
        };
        Color[] rugs =
        {
            new Color(0.95f, 0.70f, 0.62f), new Color(0.70f, 0.78f, 0.95f), new Color(0.98f, 0.85f, 0.60f),
            new Color(0.85f, 0.65f, 0.65f), new Color(0.72f, 0.90f, 0.75f), new Color(0.86f, 0.74f, 0.92f)
        };
        for (int i = 0; i < ang.Length; i++)
            BuildSideTable(root.transform, Polar(ang[i], 5.5f, 0f), ang[i] + 40f * (i % 2 == 0 ? 1f : -1f), seats[i], felts[i], rugs[i], rng);

        // pots with plants flanking the far seat and behind the player
        float[] pa = { 68f, 112f, 248f, 292f };
        for (int i = 0; i < pa.Length; i++) BuildPlant(root.transform, Polar(pa[i], 6.6f, 0f), rng);
    }

    void BuildPlant(Transform p, Vector3 pos, System.Random rng)
    {
        Material pot = Mats.Lit(new Color(0.72f, 0.36f, 0.22f), 0.2f), leaf = Mats.Lit(new Color(0.14f, 0.38f, 0.18f), 0.15f), leaf2 = Mats.Lit(new Color(0.22f, 0.48f, 0.22f), 0.15f);
        Bx(p, PrimitiveType.Cylinder, pos.x, 0.3f, pos.z, 0.55f, 0.3f, 0.55f, pot, "FlowerPot");
        Bx(p, PrimitiveType.Cylinder, pos.x, 0.62f, pos.z, 0.64f, 0.04f, 0.64f, pot, "PotRim");
        for (int i = 0; i < 7; i++)
        {
            float a = i * 51f * Mathf.Deg2Rad;
            float r = i == 0 ? 0f : 0.22f;
            float h = 0.9f + (float)rng.NextDouble() * 0.7f;
            Bx(p, PrimitiveType.Sphere, pos.x + Mathf.Cos(a) * r, h, pos.z + Mathf.Sin(a) * r, 0.5f, 0.6f, 0.5f, i % 2 == 0 ? leaf : leaf2, "Leaves");
        }
    }

    void BuildSideTable(Transform root, Vector3 c, float firstSeat, int seats, Color feltColor, Color rugTint, System.Random rng)
    {
        var t = new GameObject("SideTable");
        t.transform.SetParent(root, false);
        t.transform.localPosition = c;
        Transform w = t.transform;
        Material wood = Mats.Lit(new Color(0.22f, 0.13f, 0.08f), 0.3f), woodDark = Mats.Lit(new Color(0.16f, 0.09f, 0.06f), 0.45f);

        Prim.FlatQuad(w, new Vector3(0f, 0.014f, 0f), 4.8f, 4.8f, rugTint, Theme.Rug, true).name = "SideRug";
        Bx(w, PrimitiveType.Cylinder, 0f, 0.43f, 0f, 0.45f, 0.43f, 0.45f, wood, "Pedestal");
        Bx(w, PrimitiveType.Cylinder, 0f, 0.03f, 0f, 1.3f, 0.03f, 1.3f, wood, "Base");
        Bx(w, PrimitiveType.Cylinder, 0f, 0.86f, 0f, 2.0f, 0.08f, 2.0f, woodDark, "Rim");
        Prim.FlatQuad(w, new Vector3(0f, 0.943f, 0f), 2.0f, 2.0f, Color.white, Theme.Ring, true).name = "RimKilim";
        Bx(w, PrimitiveType.Cylinder, 0f, 0.87f, 0f, 1.78f, 0.08f, 1.78f, Mats.Lit(feltColor, 0.1f), "Felt");

        // the cards in the middle: a flop, and a pot of chips
        for (int i = 0; i < 4; i++)
        {
            Vector3 cp = new Vector3(-0.36f + i * 0.24f, TableTop + 0.006f, 0.05f);
            GameObject card = Bx(w, PrimitiveType.Cube, cp.x, cp.y, cp.z, 0.17f, 0.006f, 0.24f, Mats.Lit(new Color(0.96f, 0.95f, 0.91f), 0.3f), "Card");
            card.transform.localRotation = Quaternion.Euler(0f, (float)(rng.NextDouble() * 12.0 - 6.0), 0f);
            Color ink = rng.Next(2) == 0 ? new Color(0.78f, 0.07f, 0.10f) : new Color(0.07f, 0.07f, 0.11f);
            GameObject suit = Prim.FlatQuad(w, new Vector3(cp.x, cp.y + 0.0045f, cp.z), 0.11f, 0.11f, ink, SuitTextures.Get((CardSuit)rng.Next(4)), true);
            suit.transform.localRotation = card.transform.localRotation * Quaternion.Euler(90f, 0f, 0f);
        }
        NoShadow(ChipPile.Build(w, 20 + 5 * rng.Next(40), c + new Vector3(0.0f, TableTop + 0.02f, -0.42f), false));

        for (int k = 0; k < seats; k++)
        {
            float a = firstSeat + k * 360f / seats;
            Vector3 outward = Polar(a, 1f, 0f), tangent = new Vector3(-outward.z, 0f, outward.x);
            bool woman = rng.Next(4) == 0;
            string name = woman ? AmbientWomen[rng.Next(AmbientWomen.Length)] : AmbientMen[rng.Next(AmbientMen.Length)];
            AvatarView av = AvatarView.Create(root, c + outward * 1.55f, YawToCenter(a), name, rng.Next(8));
            av.MakeAmbient();

            // two face-down cards and a few chips in front of each player
            float yaw = YawToCenter(a);
            for (int j = 0; j < 2; j++)
            {
                Vector3 pp = outward * 0.62f + tangent * (j == 0 ? -0.1f : 0.1f);
                GameObject card = Bx(w, PrimitiveType.Cube, pp.x, TableTop + 0.006f, pp.z, 0.17f, 0.006f, 0.24f, Mats.Lit(Theme.Red, 0.3f), "HoleCard");
                card.transform.localRotation = Quaternion.Euler(0f, yaw + (j == 0 ? -8f : 8f), 0f);
                Prim.FlatQuad(w, new Vector3(pp.x, TableTop + 0.0045f, pp.z), 0.11f, 0.11f, Theme.Gold, Theme.Eagle, true).transform.localRotation = Quaternion.Euler(90f, yaw + (j == 0 ? -8f : 8f), 0f);
            }
            Vector3 chipAt = c + outward * 0.9f - tangent * 0.45f;
            NoShadow(ChipPile.Build(w, 10 + 5 * rng.Next(30), new Vector3(chipAt.x, TableTop + 0.02f, chipAt.z), false));
        }
    }

    static void NoShadow(GameObject go)
    {
        if (go == null) return;
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
        {
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }
    }

    // ---------------------------------------------------------------------------------------
    // Oda e burrave: the guest room of an Albanian house
    // ---------------------------------------------------------------------------------------

    void BuildOda(Transform p, Theme.CityLook L)
    {
        Texture wood = Theme.Wood, plaster = Theme.Plaster, stone = Theme.Stone;
        Color dark = new Color(0.55f, 0.38f, 0.28f);
        Material copper = Mats.Lit(new Color(0.72f, 0.38f, 0.20f), 0.55f, 0.7f);
        Material amber = Mats.Unlit(new Color(1f, 0.78f, 0.40f));
        Material black = Mats.Unlit(new Color(0.07f, 0.05f, 0.05f));

        // the walls of the room are built four times, one for each side (the same shell, different furniture)
        Bx(p, PrimitiveType.Cube, 0f, 3.4f, 13.15f, 28f, 6.8f, 0.3f, Surf(plaster, new Color(0.96f, 0.87f, 0.74f), 28f, 6.8f, 2.6f), "Wall");
        Bx(p, PrimitiveType.Cube, 0f, 0.7f, 12.95f, 28f, 1.4f, 0.12f, Surf(wood, dark, 28f, 1.4f, 2.4f, 0.25f), "Wainscot");
        float hem = 0.28f;
        Wq(p, 0f, 1.58f, 12.86f, 28f, hem, Mats.UnlitTiled(new Color(0.92f, 0.92f, 0.92f), Theme.Strip, 28f / (hem * 1.33f) / 24f, 1f));

        // the minder: a low bench with cushions along the wall
        Bx(p, PrimitiveType.Cube, 0f, 0.32f, 12.25f, 11f, 0.64f, 1.1f, Mats.Lit(new Color(0.40f, 0.06f, 0.10f), 0.2f), "Minder");
        Bx(p, PrimitiveType.Cube, 0f, 0.74f, 12.55f, 11f, 0.25f, 0.5f, Mats.Lit(new Color(0.42f, 0.07f, 0.11f), 0.2f), "MinderBack");
        for (int i = -4; i <= 4; i += 2)
            Bx(p, PrimitiveType.Cube, i * 1.2f, 0.80f, 12.0f, 0.95f, 0.22f, 0.7f, Mats.LitTex(new Color(0.95f, 0.9f, 0.9f), Theme.Rug, 1f, 1f, 0.1f), "Cushion").transform.localRotation = Quaternion.Euler(0f, i * 3f + _face * 5f, 0f);

        // two arched windows with wooden lattices, glowing warm
        for (int side = -1; side <= 1; side += 2)
        {
            float wx = side * 6.0f;
            Wq(p, wx, 2.9f, 12.84f, 1.3f, 2.1f, amber);
            Bx(p, PrimitiveType.Sphere, wx, 3.95f, 12.84f, 1.3f, 1.3f, 0.03f, amber, "Arch");
            for (int i = -1; i <= 1; i++) Wq(p, wx + i * 0.32f, 3.2f, 12.82f, 0.05f, 2.6f, black);
            for (int j = 0; j < 4; j++) Wq(p, wx, 2.2f + j * 0.55f, 12.82f, 1.34f, 0.05f, black);
            Bx(p, PrimitiveType.Cube, wx, 1.82f, 12.78f, 1.6f, 0.08f, 0.3f, Surf(wood, dark, 1.6f, 0.08f, 1.2f), "Sill");
        }

        if (_face == 0)
        {
            // ceiling of planks with beams (one ceiling for the whole room)
            GameObject ceil = Prim.Make(PrimitiveType.Quad, p, new Vector3(0f, 6.6f, 0f), new Vector3(28f, 28f, 1f), Surf(wood, new Color(0.55f, 0.40f, 0.30f), 28f, 28f, 2.4f, 0.2f), "Ceiling");
            ceil.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            ceil.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ceil.GetComponent<Renderer>().receiveShadows = false;
            Material beam = Surf(wood, new Color(0.45f, 0.30f, 0.22f), 28f, 0.4f, 2.4f, 0.3f);
            for (int i = 0; i < 9; i++)
                Bx(p, PrimitiveType.Cube, 0f, 6.42f, -8.2f + i * 2.05f, 28f, 0.34f, 0.34f, beam, "Beam");
            for (int i = -1; i <= 1; i += 2)
                Bx(p, PrimitiveType.Cube, i * 3.2f, 6.2f, 0f, 0.3f, 0.6f, 28f, beam, "Joist");

            // hanging kilims either side of the flag
            OdaKilims(p, 3.6f);

            // fireplace (oxhak) on the right, with a fire
            Material hearth = Surf(stone, new Color(0.95f, 0.88f, 0.80f), 3f, 1.6f, 1.6f, 0.05f);
            Bx(p, PrimitiveType.Cube, 10f, 0.8f, 12.45f, 3.2f, 1.6f, 1.0f, hearth, "Hearth");
            Bx(p, PrimitiveType.Cube, 10f, 0.7f, 11.93f, 1.8f, 1.1f, 0.06f, black, "FireBox");
            Bx(p, PrimitiveType.Cube, 10f, 2.15f, 12.55f, 2.8f, 1.2f, 0.8f, hearth, "Hood");
            Bx(p, PrimitiveType.Cube, 10f, 4.2f, 12.8f, 1.5f, 3.4f, 0.6f, Surf(stone, new Color(0.9f, 0.84f, 0.78f), 1.5f, 3.4f, 1.6f, 0.05f), "Chimney");
            for (int i = -1; i <= 1; i++)
            {
                Material flame = Mats.Unlit(i == 0 ? new Color(1f, 0.84f, 0.3f) : new Color(1f, 0.5f, 0.1f));
                Tilted(p, PrimitiveType.Cube, 10f + i * 0.4f, 0.55f + (i == 0 ? 0.12f : 0f), 11.88f, 0.36f, 0.36f, 0.02f, 45f, flame, "Flame");
            }
            for (int i = 0; i < 3; i++)
                Bx(p, PrimitiveType.Cylinder, 10f, 0.2f + i * 0.04f, 11.88f, 0.14f, 0.45f, 0.14f, Mats.Lit(new Color(0.22f, 0.12f, 0.06f), 0.1f), "Log").transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            var fireGo = new GameObject("Fire");
            fireGo.transform.SetParent(p, false);
            fireGo.transform.position = new Vector3(10f, 0.9f, 11.0f);
            _fireLight = fireGo.AddComponent<Light>();
            _fireLight.type = LightType.Point;
            _fireLight.color = new Color(1f, 0.58f, 0.24f);
            _fireLight.range = 9f;
            _fireLight.intensity = 1.8f;
            _fireLight.shadows = LightShadows.None;

            OdaShelf(p, -10f, copper);
            OdaCiftelia(p, -8.2f);
        }
        else if (_face == 1)
        {
            // the door to the hall: a wooden arch with iron studs, and a bench with shoes (the guests leave their shoes outside)
            OdaKilims(p, 3.6f);
            Material doorM = Surf(wood, new Color(0.50f, 0.32f, 0.20f), 1.6f, 3f, 1.2f, 0.3f);
            Bx(p, PrimitiveType.Cube, 0f, 1.5f, 12.93f, 1.9f, 3.0f, 0.1f, Surf(stone, new Color(0.9f, 0.85f, 0.8f), 1.9f, 3f, 1.6f, 0.05f), "DoorFrame");
            Bx(p, PrimitiveType.Sphere, 0f, 3.0f, 12.93f, 1.9f, 1.9f, 0.1f, Surf(stone, new Color(0.9f, 0.85f, 0.8f), 1.9f, 1.9f, 1.6f, 0.05f), "DoorArchFrame");
            Bx(p, PrimitiveType.Cube, 0f, 1.4f, 12.86f, 1.6f, 2.8f, 0.1f, doorM, "Door");
            Bx(p, PrimitiveType.Sphere, 0f, 2.8f, 12.86f, 1.6f, 1.6f, 0.1f, doorM, "DoorArch");
            for (int i = -1; i <= 1; i += 2)
                for (int j = 0; j < 4; j++) Bx(p, PrimitiveType.Sphere, i * 0.45f, 0.6f + j * 0.65f, 12.78f, 0.1f, 0.1f, 0.05f, black, "Stud");
            Bx(p, PrimitiveType.Sphere, 0.55f, 1.4f, 12.76f, 0.16f, 0.16f, 0.08f, copper, "Handle");
            OdaShelf(p, 10f, copper);
            OdaCiftelia(p, -9.5f);
        }
        else if (_face == 2)
        {
            // behind the player: a tall cupboard (dollap), a big kilim, a shelf with plates
            OdaKilims(p, 3.6f);
            Material dollap = Surf(wood, new Color(0.55f, 0.36f, 0.22f), 2.4f, 3.4f, 1.4f, 0.3f);
            Bx(p, PrimitiveType.Cube, -10f, 1.7f, 12.6f, 2.6f, 3.4f, 0.7f, dollap, "Dollap");
            Bx(p, PrimitiveType.Cube, -10f, 3.5f, 12.55f, 2.8f, 0.14f, 0.82f, dollap, "DollapTop");
            for (int i = -1; i <= 1; i += 2)
            {
                Wq(p, -10f + i * 0.62f, 1.7f, 12.24f, 1.1f, 2.8f, Mats.Unlit(new Color(0.34f, 0.2f, 0.12f)));
                Bx(p, PrimitiveType.Sphere, -10f + i * 0.12f, 1.7f, 12.22f, 0.12f, 0.12f, 0.06f, copper, "Knob");
            }
            Wq(p, 0f, 3.4f, 12.82f, 3.2f, 2.4f, Mats.Unlit(new Color(0.9f, 0.82f, 0.82f), Theme.Rug));
            OdaShelf(p, 10f, copper);
        }
        else
        {
            // a framed double-headed eagle, a shelf and the çiftelia
            OdaKilims(p, 3.6f);
            Bx(p, PrimitiveType.Cube, 0f, 3.5f, 12.88f, 1.7f, 2.2f, 0.06f, Mats.Lit(Theme.Gold, 0.5f, 0.4f), "FrameGold");
            Wq(p, 0f, 3.5f, 12.84f, 1.45f, 1.95f, Mats.Unlit(Theme.Red));
            Wq(p, 0f, 3.5f, 12.83f, 1.2f, 1.2f, Mats.Unlit(Theme.Black, Theme.Eagle));
            OdaShelf(p, -10f, copper);
            OdaCiftelia(p, 8.2f);
        }
    }

    /// <summary>Two woven kilims hanging on rods, left and right of the middle of a wall.</summary>
    void OdaKilims(Transform p, float dx)
    {
        for (int side = -1; side <= 1; side += 2)
        {
            Wq(p, side * dx, 3.6f, 12.84f, 1.9f, 1.9f, Mats.Unlit(new Color(0.86f, 0.80f, 0.80f), Theme.Rug));
            Bx(p, PrimitiveType.Cylinder, side * dx, 4.62f, 12.8f, 0.04f, 1.05f, 0.04f, Mats.Lit(new Color(0.25f, 0.15f, 0.08f), 0.3f), "Rod").transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        }
    }

    /// <summary>A shelf on the wall with copper plates, a pot, a jug and a cup.</summary>
    void OdaShelf(Transform p, float cx, Material copper)
    {
        Color dark = new Color(0.55f, 0.38f, 0.28f);
        Bx(p, PrimitiveType.Cube, cx, 2.0f, 12.7f, 3.0f, 0.08f, 0.42f, Surf(Theme.Wood, dark, 3f, 0.08f, 1.5f), "Shelf");
        for (int i = 0; i < 3; i++)
        {
            GameObject plate = Bx(p, PrimitiveType.Cylinder, cx - 1f + i * 1.0f, 3.0f, 12.82f, 0.62f, 0.02f, 0.62f, copper, "CopperPlate");
            plate.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Bx(p, PrimitiveType.Sphere, cx - 1f + i * 1.0f, 3.0f, 12.78f, 0.16f, 0.16f, 0.06f, Mats.Lit(Theme.Gold, 0.6f, 0.6f), "PlateBoss");
        }
        Bx(p, PrimitiveType.Sphere, cx - 0.8f, 2.28f, 12.7f, 0.36f, 0.36f, 0.36f, copper, "Pot");
        Bx(p, PrimitiveType.Cylinder, cx - 0.8f, 2.5f, 12.7f, 0.14f, 0.06f, 0.14f, copper, "PotNeck");
        Bx(p, PrimitiveType.Cylinder, cx + 0.4f, 2.18f, 12.7f, 0.22f, 0.14f, 0.22f, copper, "Jug");
        Bx(p, PrimitiveType.Cylinder, cx + 1.0f, 2.20f, 12.7f, 0.18f, 0.16f, 0.18f, Mats.Lit(new Color(0.85f, 0.83f, 0.78f), 0.5f), "Cup");
    }

    /// <summary>The çiftelia (a two-stringed folk instrument) hanging on the wall.</summary>
    void OdaCiftelia(Transform p, float x)
    {
        Material wood2 = Mats.Lit(new Color(0.62f, 0.38f, 0.18f), 0.4f);
        Material black = Mats.Unlit(new Color(0.07f, 0.05f, 0.05f));
        GameObject ci = new GameObject("Ciftelia");
        ci.transform.SetParent(p, false);
        ci.transform.localPosition = new Vector3(x, 3.0f, 12.78f);
        ci.transform.localRotation = Quaternion.Euler(0f, 0f, -14f);
        Bx(ci.transform, PrimitiveType.Sphere, 0f, 0f, 0f, 0.34f, 0.46f, 0.12f, wood2, "CiftBody");
        Bx(ci.transform, PrimitiveType.Cube, 0f, 0.80f, 0f, 0.07f, 1.1f, 0.05f, wood2, "CiftNeck");
        Bx(ci.transform, PrimitiveType.Cube, 0f, 1.40f, 0f, 0.14f, 0.18f, 0.05f, wood2, "CiftHead");
        Wq(ci.transform, 0f, 0.55f, -0.07f, 0.02f, 1.7f, black);
        Bx(ci.transform, PrimitiveType.Sphere, 0f, 0.05f, -0.07f, 0.12f, 0.12f, 0.03f, black, "CiftHole");
    }

    // ---------------------------------------------------------------------------------------
    // the cities
    // ---------------------------------------------------------------------------------------

    void BuildShkoder(Transform p, Theme.CityLook L, System.Random rng)
    {
        // the Albanian Alps, the lake, the hill of Rozafa castle and the old town
        float[] fx = { -26f, -9f, 9f, 27f };
        float[] fs = { 17f, 21f, 16f, 20f };
        for (int i = 0; i < fx.Length; i++) Mountain(p, fx[i], 36f, fs[i], new Color(0.20f + i * 0.01f, 0.26f, 0.42f), true);
        float[] nx = { -17f, -3f, 15f };
        float[] ns = { 12f, 10f, 13f };
        for (int i = 0; i < nx.Length; i++) Mountain(p, nx[i], 24f, ns[i], new Color(0.11f, 0.19f, 0.25f), false);

        Prim.FlatQuad(p, new Vector3(0f, 0.02f, 9.4f), 80f, 2.8f, new Color(0.10f, 0.30f, 0.46f), null, true).name = "Lake";
        Prim.FlatQuad(p, new Vector3(0f, 0.025f, 8.0f), 80f, 0.14f, new Color(0.55f, 0.70f, 0.85f), null, true).name = "Shore";
        Prim.FlatQuad(p, new Vector3(-9.5f, 0.03f, 8.8f), 0.9f, 3.6f, new Color(0.96f, 0.93f, 0.80f, 0.45f), null, true).name = "MoonGlow";

        if (_face == 0)
        {
            Bx(p, PrimitiveType.Sphere, 8f, -0.4f, 16.5f, 15f, 8f, 8.5f, Mats.Lit(new Color(0.14f, 0.24f, 0.15f), 0.05f), "Hill");
            Castle(p, 8f, 3.1f, 16.5f, 1.4f, new Color(0.88f, 0.82f, 0.74f));
        }

        int seed = 1;
        Color roof = new Color(0.95f, 0.68f, 0.58f);
        for (float x = -22f; x < 24f; x += 2.5f)
        {
            if (x > 0.5f && x < 15f) continue;
            float w = 2.0f + (float)rng.NextDouble() * 0.8f;
            float h = 1.5f + (float)rng.NextDouble() * 1.4f;
            House(p, L, x, 11.2f, w, h, 1.7f, Theme.Plaster, seed % 3 == 0 ? new Color(0.88f, 0.80f, 0.68f) : new Color(0.96f, 0.90f, 0.80f), Theme.RoofTile, roof, 1, seed++, 0.2f);
        }
        for (float x = -22f; x < -1f; x += 3.1f)
        {
            float w = 2.2f + (float)rng.NextDouble() * 0.8f;
            float h = 2.4f + (float)rng.NextDouble() * 1.6f;
            House(p, L, x, 13.4f, w, h, 1.7f, Theme.Plaster, seed % 2 == 0 ? new Color(0.82f, 0.76f, 0.70f) : new Color(0.94f, 0.88f, 0.78f), Theme.RoofTile, Color.Lerp(roof, Color.black, 0.1f), 1, seed++, 1.3f);
        }
        for (int i = 0; i < 9; i++) Pine(p, -20f + i * 3.4f + (float)rng.NextDouble(), 9.6f, 1.6f + (float)rng.NextDouble() * 0.8f, new Color(0.10f, 0.26f, 0.15f));
    }

    void BuildBerat(Transform p, Theme.CityLook L, System.Random rng)
    {
        // white houses climbing the hill, row after row, with the castle on top
        for (int i = 0; i < 5; i++) Mountain(p, -30f + i * 15f, 38f, 18f + (i % 2) * 5f, new Color(0.26f, 0.20f, 0.36f), i % 2 == 1);

        Color tile = new Color(0.98f, 0.62f, 0.50f);
        int seed = 5;
        float prevTop = 0.3f;
        for (int row = 0; row < 5; row++)
        {
            float z = 10.4f + row * 1.05f;
            float baseH = 1.0f + row * 0.7f;
            float x = -20f + (float)rng.NextDouble();
            float maxTop = 0f;
            while (x < 20f)
            {
                float w = 1.4f + (float)rng.NextDouble() * 0.9f;
                float h = baseH + (float)rng.NextDouble() * 0.6f;
                bool grey = seed % 4 == 0;
                House(p, L, x + w * 0.5f, z, w, h, 0.95f, Theme.Plaster, grey ? new Color(0.84f, 0.82f, 0.80f) : Color.white, Theme.RoofTile, tile, 2, seed++, prevTop - 0.15f);
                if (h > maxTop) maxTop = h;
                x += w + 0.1f;
            }
            prevTop = maxTop;
        }
        if (_face == 0) Castle(p, -9f, 4.6f, 16.0f, 1.5f, new Color(0.94f, 0.88f, 0.78f));
        for (int i = 0; i < 12; i++) Cn(p, -22f + i * 4f, 5.6f + (i % 3) * 0.3f, 15.2f, 0.5f, 1.4f, 0.5f, Mats.Lit(new Color(0.08f, 0.25f, 0.12f), 0.05f), "Cypress");
    }

    void BuildPrishtine(Transform p, Theme.CityLook L, System.Random rng)
    {
        // a young capital at dusk: the low hills of Gërmia, tall blocks of flats, the boulevard with its lamps,
        // and in front of the player the NEWBORN letters, the National Library with its domes and net, a minaret and the cathedral tower
        for (int i = 0; i < 5; i++) Mountain(p, -32f + i * 16f, 38f, 12f + (i % 2) * 3f, new Color(0.20f, 0.20f, 0.34f), false);

        Color[] paint =
        {
            new Color(0.96f, 0.92f, 0.84f), new Color(0.80f, 0.82f, 0.86f), new Color(0.94f, 0.78f, 0.52f),
            new Color(0.66f, 0.78f, 0.90f), new Color(0.88f, 0.60f, 0.48f), new Color(0.76f, 0.86f, 0.74f)
        };
        Color flat = new Color(0.45f, 0.45f, 0.50f);
        int seed = 31;

        // the tall blocks behind
        for (float x = -24f; x < 24f; x += 3.4f)
        {
            float w = 2.6f + (float)rng.NextDouble() * 0.7f;
            float h = 5.5f + (float)rng.NextDouble() * 3.5f;
            House(p, L, x, 16.5f, w, h, 2.4f, Theme.Plaster, paint[seed % paint.Length], Theme.RoofTile, flat, 2, seed++, 0.6f);
        }
        // the lower buildings along the boulevard (with a gap for the library in front of the player)
        for (float x = -22f; x < 22f; x += 2.7f)
        {
            if (_face == 0 && x > 3.5f && x < 14.5f) continue;
            float w = 2.0f + (float)rng.NextDouble() * 0.6f;
            float h = 2.2f + (float)rng.NextDouble() * 1.6f;
            House(p, L, x, 11.8f, w, h, 1.9f, Theme.Plaster, paint[(seed + 3) % paint.Length], Theme.RoofTile, flat, 2, seed++, 0.9f);
        }

        // the street lamps of the boulevard
        Material iron = Mats.Lit(new Color(0.12f, 0.12f, 0.14f), 0.4f, 0.3f);
        Material globe = Mats.Unlit(new Color(1f, 0.90f, 0.62f));
        for (int i = -6; i <= 6; i++)
        {
            if (i == 0) continue;
            float x = i * 3.4f;
            Bx(p, PrimitiveType.Cylinder, x, 1.3f, 8.4f, 0.08f, 1.3f, 0.08f, iron, "LampPost");
            Bx(p, PrimitiveType.Sphere, x, 2.72f, 8.4f, 0.34f, 0.34f, 0.34f, globe, "LampGlobe");
        }

        if (_face != 0) return;

        // NEWBORN: the big yellow letters, put up the day Kosovo declared independence
        Newborn(p, -13.2f, 10.4f, 1.15f, 1.7f, 0.35f);

        // the National Library: white blocks under a metal net, with domes of every size on the roof
        float lx = 9f, lz = 14.2f, lw = 8.6f, lh = 3.8f, ld = 4f;
        Material libWall = Surf(Theme.Plaster, new Color(0.92f, 0.92f, 0.90f), lw, lh, 2.2f);
        Material dome = Mats.Lit(new Color(0.90f, 0.90f, 0.88f), 0.35f);
        Material net = Mats.Unlit(new Color(0.26f, 0.27f, 0.30f));
        Material glass = Mats.Unlit(new Color(1f, 0.82f, 0.48f));
        Bx(p, PrimitiveType.Cube, lx, lh * 0.5f, lz, lw, lh, ld, libWall, "Library");
        Bx(p, PrimitiveType.Cube, lx - lw * 0.32f, lh + 0.6f, lz + 0.3f, 2.4f, 1.2f, 2.6f, libWall, "LibraryBlock");
        Bx(p, PrimitiveType.Cube, lx + lw * 0.30f, lh + 0.45f, lz + 0.2f, 2.2f, 0.9f, 2.4f, libWall, "LibraryBlock");
        float fz = lz - ld * 0.5f - 0.03f;
        for (int i = 0; i < 9; i++)                                  // a band of lit windows behind the net
            Wq(p, lx - lw * 0.5f + 0.6f + i * (lw - 1.2f) / 8f, lh * 0.55f, fz + 0.01f, 0.45f, 0.9f, glass);
        for (float x = lx - lw * 0.5f + 0.3f; x < lx + lw * 0.5f; x += 0.55f)
            Wq(p, x, lh * 0.5f, fz - 0.01f, 0.05f, lh, net);
        for (float y = 0.3f; y < lh; y += 0.55f)
            Wq(p, lx, y, fz - 0.02f, lw, 0.05f, net);
        Bx(p, PrimitiveType.Sphere, lx, lh, lz, 3.0f, 2.4f, 3.0f, dome, "BigDome");
        for (int i = 0; i < 12; i++)
        {
            float dx = (i % 6) / 5f - 0.5f, dz = i < 6 ? -0.3f : 0.3f;
            if (Mathf.Abs(dx) < 0.18f) continue;                    // the big dome is in the middle
            float s = 0.8f + (float)rng.NextDouble() * 0.8f;
            Bx(p, PrimitiveType.Sphere, lx + dx * (lw - 1.4f), lh, lz + dz * ld, s, s * 0.9f, s, dome, "Dome");
        }

        // the minaret of an old mosque, rising above the boulevard
        Material white = Mats.Lit(new Color(0.96f, 0.94f, 0.90f), 0.2f);
        Material lead = Mats.Lit(new Color(0.42f, 0.46f, 0.52f), 0.4f, 0.3f);
        Bx(p, PrimitiveType.Cylinder, 2.4f, 3.8f, 13.4f, 0.4f, 3.8f, 0.4f, white, "Minaret");
        Bx(p, PrimitiveType.Cylinder, 2.4f, 6.4f, 13.4f, 0.6f, 0.07f, 0.6f, globe, "Balcony");
        Cn(p, 2.4f, 8.2f, 13.4f, 0.42f, 1.0f, 0.42f, lead, "MinaretTop");

        // the bell tower of the cathedral of Mother Teresa
        Material cathedral = Surf(Theme.Plaster, new Color(0.98f, 0.92f, 0.82f), 1.6f, 10f, 2f);
        Bx(p, PrimitiveType.Cube, -1.6f, 5.2f, 15.4f, 1.6f, 10.4f, 1.6f, cathedral, "BellTower");
        Cn(p, -1.6f, 11.4f, 15.4f, 1.7f, 2.0f, 1.7f, Mats.Lit(new Color(0.30f, 0.44f, 0.48f), 0.3f, 0.2f), "Spire");
        Wq(p, -1.6f, 8.6f, 14.58f, 0.5f, 1.2f, glass);
        Bx(p, PrimitiveType.Sphere, -1.6f, 9.2f, 14.58f, 0.5f, 0.5f, 0.03f, glass, "BelfryArch");
    }

    /// <summary>The NEWBORN letters, each built from yellow blocks along its strokes.</summary>
    static void Newborn(Transform p, float x0, float z, float w, float h, float gap)
    {
        // each letter as strokes in a unit box (x, y from 0 to 1)
        float[][] N = { new[] { 0f, 0f, 0f, 1f }, new[] { 1f, 0f, 1f, 1f }, new[] { 0f, 1f, 1f, 0f } };
        float[][] E = { new[] { 0f, 0f, 0f, 1f }, new[] { 0f, 1f, 1f, 1f }, new[] { 0f, 0.5f, 0.8f, 0.5f }, new[] { 0f, 0f, 1f, 0f } };
        float[][] W = { new[] { 0f, 0f, 0f, 1f }, new[] { 1f, 0f, 1f, 1f }, new[] { 0f, 0f, 0.5f, 0.6f }, new[] { 0.5f, 0.6f, 1f, 0f } };
        float[][] B = { new[] { 0f, 0f, 0f, 1f }, new[] { 0f, 1f, 0.85f, 1f }, new[] { 0.85f, 1f, 0.85f, 0.5f }, new[] { 0f, 0.5f, 1f, 0.5f }, new[] { 1f, 0.5f, 1f, 0f }, new[] { 0f, 0f, 1f, 0f } };
        float[][] O = { new[] { 0f, 0f, 0f, 1f }, new[] { 1f, 0f, 1f, 1f }, new[] { 0f, 1f, 1f, 1f }, new[] { 0f, 0f, 1f, 0f } };
        float[][] R = { new[] { 0f, 0f, 0f, 1f }, new[] { 0f, 1f, 1f, 1f }, new[] { 1f, 1f, 1f, 0.5f }, new[] { 0f, 0.5f, 1f, 0.5f }, new[] { 0.35f, 0.5f, 1f, 0f } };
        float[][][] word = { N, E, W, B, O, R, N };

        Material yellow = Mats.Lit(new Color(1f, 0.80f, 0.12f), 0.35f);
        float t = h * 0.17f, depth = 0.45f;
        for (int li = 0; li < word.Length; li++)
        {
            float lx = x0 + li * (w + gap);
            foreach (float[] s in word[li])
            {
                var a = new Vector2(lx + t * 0.5f + s[0] * (w - t), t * 0.5f + s[1] * (h - t));
                var b = new Vector2(lx + t * 0.5f + s[2] * (w - t), t * 0.5f + s[3] * (h - t));
                Vector2 d = b - a;
                Vector2 m = (a + b) * 0.5f;
                Tilted(p, PrimitiveType.Cube, m.x, m.y, z, d.magnitude + t, t, depth, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg, yellow, "Letter");
            }
        }
    }

    void BuildTirane(Transform p, Theme.CityLook L, System.Random rng)
    {
        // the final: coloured blocks of the capital, the clock tower and mosque on the square, the statue of Skanderbeg
        for (int i = 0; i < 4; i++) Mountain(p, -26f + i * 17f, 38f, 19f + (i % 2) * 3f, new Color(0.26f, 0.16f, 0.26f), i == 1);

        Color[] paint =
        {
            new Color(0.92f, 0.40f, 0.32f), new Color(0.98f, 0.74f, 0.32f), new Color(0.97f, 0.90f, 0.62f),
            new Color(0.42f, 0.72f, 0.76f), new Color(0.86f, 0.78f, 0.74f), new Color(0.78f, 0.42f, 0.52f)
        };
        Color flat = new Color(0.55f, 0.50f, 0.52f);
        int seed = 13;
        for (float x = -22f; x < 22f; x += 2.6f)
        {
            float w = 2.1f + (float)rng.NextDouble() * 0.6f;
            float h = 2.0f + (float)rng.NextDouble() * 2.0f;
            House(p, L, x, 11.8f, w, h, 1.9f, Theme.Plaster, paint[seed % paint.Length], Theme.RoofTile, flat, 2, seed++, 0.2f);
        }
        for (float x = -20f; x < 20f; x += 3.5f)
        {
            float w = 2.7f + (float)rng.NextDouble() * 0.8f;
            float h = 3.6f + (float)rng.NextDouble() * 2.0f;
            House(p, L, x, 14.0f, w, h, 1.9f, Theme.Plaster, paint[(seed + 2) % paint.Length], Theme.RoofTile, flat, 0, seed++, 1.9f);
        }

        // clock tower with a pointed roof, and a small mosque with a dome and a minaret (only in front of the player)
        if (_face != 0) return;
        Material stone = Surf(Theme.Stone, new Color(1f, 0.96f, 0.90f), 1.1f, 5f, 1.6f, 0.05f);
        Material copper = Mats.Lit(new Color(0.30f, 0.60f, 0.54f), 0.35f, 0.3f);
        Material white = Mats.Lit(new Color(0.96f, 0.93f, 0.88f), 0.2f);
        Bx(p, PrimitiveType.Cube, 4.8f, 2.6f, 10.8f, 1.1f, 5.2f, 1.1f, stone, "ClockTower");
        Cn(p, 4.8f, 5.9f, 10.8f, 1.3f, 1.2f, 1.3f, copper, "TowerRoof");
        Bx(p, PrimitiveType.Sphere, 4.8f, 4.3f, 10.2f, 0.7f, 0.7f, 0.05f, Mats.Unlit(Theme.Cream), "ClockFace");
        Bx(p, PrimitiveType.Cylinder, 8.6f, 1.0f, 10.8f, 2.6f, 1.0f, 2.6f, white, "Mosque");
        Bx(p, PrimitiveType.Sphere, 8.6f, 2.0f, 10.8f, 2.6f, 2.0f, 2.6f, copper, "Dome");
        Bx(p, PrimitiveType.Cylinder, 10.4f, 2.6f, 10.3f, 0.34f, 2.6f, 0.34f, white, "Minaret");
        Bx(p, PrimitiveType.Cylinder, 10.4f, 4.6f, 10.3f, 0.52f, 0.06f, 0.52f, white, "Balcony");
        Cn(p, 10.4f, 5.6f, 10.3f, 0.36f, 0.9f, 0.36f, copper, "MinaretTop");

        // the statue: a plinth with a gold horse and rider
        Material gold = Mats.Lit(Theme.Gold, 0.5f, 0.5f);
        Bx(p, PrimitiveType.Cube, -4.2f, 0.8f, 9.6f, 1.2f, 1.6f, 1.2f, stone, "Plinth");
        Bx(p, PrimitiveType.Capsule, -4.2f, 1.95f, 9.6f, 0.5f, 0.7f, 0.5f, gold, "Horse").transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        Bx(p, PrimitiveType.Sphere, -3.65f, 2.3f, 9.6f, 0.3f, 0.36f, 0.3f, gold, "HorseHead");
        Bx(p, PrimitiveType.Capsule, -4.25f, 2.45f, 9.6f, 0.28f, 0.38f, 0.28f, gold, "Rider");
        Bx(p, PrimitiveType.Sphere, -4.25f, 2.88f, 9.6f, 0.26f, 0.26f, 0.26f, gold, "RiderHead");
    }

    // ---------------------------------------------------------------------------------------
    // the coast
    // ---------------------------------------------------------------------------------------

    void BuildSarande(Transform p, Theme.CityLook L, System.Random rng)
    {
        // the Ionian sea at sunset, with the island of Corfu on the horizon
        Prim.FlatQuad(p, new Vector3(0f, 0.01f, 36f), 170f, 56f, new Color(0.08f, 0.28f, 0.52f), null, true).name = "Sea";
        for (int i = 0; i < 9; i++)
        {
            float z = 12f + i * i * 0.55f + i * 1.2f;
            Prim.FlatQuad(p, new Vector3(0f, 0.02f + i * 0.001f, z), 170f, 0.35f + i * 0.08f, Color.Lerp(new Color(0.2f, 0.42f, 0.62f), new Color(1f, 0.66f, 0.36f), i / 8f), null, true).name = "SeaBand";
        }
        // the sun and its reflection
        if (_face == 0)
        {
            Bx(p, PrimitiveType.Sphere, 3f, 2.6f, 60f, 6f, 6f, 0.2f, Mats.Unlit(new Color(1f, 0.86f, 0.52f)), "Sun");
            Bx(p, PrimitiveType.Sphere, 3f, 2.6f, 61f, 11f, 11f, 0.2f, Mats.Unlit(new Color(1f, 0.70f, 0.40f, 0.35f)), "SunGlow");
            Prim.FlatQuad(p, new Vector3(3f, 0.03f, 30f), 2.4f, 36f, new Color(1f, 0.78f, 0.45f, 0.55f), null, true).name = "SunPath";
        }
        Tilted(p, PrimitiveType.Cube, -14f, 0f, 58f, 8f, 8f, 4f, 45f, Mats.Unlit(new Color(0.36f, 0.26f, 0.44f)), "Corfu");
        Tilted(p, PrimitiveType.Cube, 22f, 0f, 57f, 6f, 6f, 4f, 45f, Mats.Unlit(new Color(0.40f, 0.28f, 0.46f)), "Corfu2");

        // the town: white terraces climbing both slopes of the bay, the sea open in the middle
        Color tile = new Color(1f, 0.62f, 0.45f);
        int seed = 21;
        for (int side = -1; side <= 1; side += 2)
        {
            Mountain(p, side * 26f, 30f, 17f, new Color(0.22f, 0.20f, 0.30f), false);
            for (int row = 0; row < 4; row++)
            {
                float z = 10.6f + row * 1.0f;
                float baseH = 1.1f + row * 0.9f;
                float x = side * (7.5f + row * 0.6f);
                for (int k = 0; k < 7; k++)
                {
                    float w = 1.5f + (float)rng.NextDouble() * 0.9f;
                    float h = baseH + (float)rng.NextDouble() * 0.7f;
                    float cx = x + side * (k * 2.05f + w * 0.5f);
                    House(p, L, cx, z, w, h, 1.0f, Theme.Plaster, k % 3 == 0 ? new Color(0.98f, 0.92f, 0.80f) : Color.white, Theme.RoofTile, tile, k % 2 == 0 ? 2 : 1, seed++, 0.2f);
                }
            }
        }

        // the promenade: a railing, lamp posts and palms
        Material iron = Mats.Lit(new Color(0.10f, 0.10f, 0.12f), 0.4f, 0.3f);
        Bx(p, PrimitiveType.Cube, 0f, 1.05f, 7.4f, 60f, 0.06f, 0.06f, iron, "Rail");
        Bx(p, PrimitiveType.Cube, 0f, 0.55f, 7.4f, 60f, 0.04f, 0.04f, iron, "Rail2");
        for (int i = -14; i <= 14; i++) Bx(p, PrimitiveType.Cylinder, i * 2f, 0.52f, 7.4f, 0.06f, 0.52f, 0.06f, iron, "Post");
        for (int i = -3; i <= 3; i++)
        {
            if (i == 0) continue;
            Palm(p, i * 4.6f + 1.3f, 8.4f, 3.4f + (Mathf.Abs(i) % 3) * 0.5f, (i % 2 == 0 ? 0.3f : -0.3f));
        }
        // boats with white sails
        for (int i = 0; i < 4; i++)
        {
            float bx = -16f + i * 9.5f, bz = 20f + (i % 2) * 9f;
            Bx(p, PrimitiveType.Cube, bx, 0.25f, bz, 2.2f, 0.45f, 0.8f, Mats.Lit(new Color(0.96f, 0.95f, 0.92f), 0.3f), "Hull");
            Bx(p, PrimitiveType.Cylinder, bx, 1.6f, bz, 0.06f, 1.4f, 0.06f, Mats.Lit(new Color(0.3f, 0.2f, 0.12f), 0.1f), "Mast");
            Tilted(p, PrimitiveType.Cube, bx + 0.45f, 1.6f, bz - 0.05f, 1.1f, 1.1f, 0.02f, 45f, Mats.Unlit(new Color(1f, 0.94f, 0.86f)), "Sail");
        }
    }

    void BuildUlqin(Transform p, Theme.CityLook L, System.Random rng)
    {
        // the final: the Adriatic by moonlight, the old town (Kalaja) on its rock, the new town on the hills with its minarets,
        // a corsair ship at anchor (Ulqin was a pirate port) and the square fishing nets (kalimera) of the Buna
        Prim.FlatQuad(p, new Vector3(0f, 0.01f, 36f), 170f, 56f, new Color(0.03f, 0.10f, 0.20f), null, true).name = "Sea";
        for (int i = 0; i < 7; i++)
        {
            float z = 12f + i * i * 0.6f + i * 1.4f;
            Prim.FlatQuad(p, new Vector3(0f, 0.02f + i * 0.001f, z), 170f, 0.25f + i * 0.06f, Color.Lerp(new Color(0.06f, 0.18f, 0.30f), new Color(0.18f, 0.30f, 0.44f), i / 6f), null, true).name = "SeaBand";
        }
        // the beach in front of the terrace
        Prim.FlatQuad(p, new Vector3(0f, 0.015f, 8.9f), 80f, 1.8f, new Color(0.62f, 0.56f, 0.46f), null, true).name = "Beach";

        Material moonM = Mats.Unlit(new Color(0.97f, 0.95f, 0.86f));
        if (_face == 0)
        {
            // the full moon low over the sea, and its silver path on the water
            Bx(p, PrimitiveType.Sphere, 5f, 8.5f, 58f, 5.2f, 5.2f, 0.2f, moonM, "Moon");
            Bx(p, PrimitiveType.Sphere, 5f, 8.5f, 59f, 10f, 10f, 0.2f, Mats.Unlit(new Color(0.55f, 0.66f, 0.80f, 0.35f)), "MoonGlow");
            Prim.FlatQuad(p, new Vector3(5f, 0.03f, 32f), 2.2f, 40f, new Color(0.85f, 0.88f, 0.90f, 0.5f), null, true).name = "MoonPath";
        }
        else if (_face != 2)
            Bx(p, PrimitiveType.Sphere, -12f, 14f, 48f, 1.2f, 1.2f, 0.2f, moonM, "Venus");

        // the new town: white houses with red tiles on the hills either side of the bay
        Color tile = new Color(0.86f, 0.42f, 0.30f);
        int seed = 51;
        for (int side = -1; side <= 1; side += 2)
        {
            Mountain(p, side * 27f, 30f, 17f, new Color(0.10f, 0.14f, 0.22f), false);
            if (_face == 0 && side < 0) continue;                    // the rock of the old town stands there
            for (int row = 0; row < 4; row++)
            {
                float z = 10.8f + row * 1.0f;
                float baseH = 1.0f + row * 0.9f;
                float x = side * (7.0f + row * 0.6f);
                for (int k = 0; k < 7; k++)
                {
                    float w = 1.4f + (float)rng.NextDouble() * 0.9f;
                    float h = baseH + (float)rng.NextDouble() * 0.7f;
                    float cx = x + side * (k * 2.05f + w * 0.5f);
                    House(p, L, cx, z, w, h, 1.0f, Theme.Plaster, k % 4 == 0 ? new Color(0.92f, 0.86f, 0.74f) : Color.white, Theme.RoofTile, tile, 1, seed++, 0.2f);
                }
            }
            // minarets among the houses
            for (int m = 0; m < 2; m++)
                Minaret(p, side * (10.5f + m * 6.5f), 13.6f + m * 0.8f, 5.6f + m * 0.8f);
        }

        if (_face == 0)
        {
            // Kalaja: the old town on a rock that falls into the sea, walled, with the tower of the Balshajs
            Material rock = Surf(Theme.Stone, new Color(0.55f, 0.52f, 0.50f), 12f, 3.4f, 3f, 0.05f);
            Color stone = new Color(0.88f, 0.82f, 0.72f);
            Bx(p, PrimitiveType.Cube, -11.5f, 1.7f, 17.5f, 12f, 3.4f, 7f, rock, "Rock");
            Bx(p, PrimitiveType.Sphere, -5.6f, 0.6f, 16.5f, 4f, 3.6f, 6f, rock, "RockEnd");
            Bx(p, PrimitiveType.Sphere, -11.5f, 0.3f, 14.2f, 12f, 2.2f, 2.2f, rock, "RockFoot");
            Castle(p, -11.5f, 3.4f, 14.6f, 1.15f, stone);
            for (int i = 0; i < 6; i++)                               // stone houses inside the walls, rising over them
            {
                float w = 1.5f + (float)rng.NextDouble() * 0.6f;
                float h = 5.2f + (float)rng.NextDouble() * 1.4f;
                House(p, L, -15.5f + i * 1.75f, 16.8f + (i % 2) * 1.2f, w, h, 1.4f, Theme.Stone, Color.Lerp(stone, Color.white, 0.2f), Theme.RoofTile, tile, 1, seed++, 4.6f);
            }
            Material tower = Surf(Theme.Stone, stone, 1.8f, 8f, 1.6f, 0.05f);
            Bx(p, PrimitiveType.Cube, -16.8f, 4.1f, 15.6f, 1.8f, 8.2f, 1.8f, tower, "BalshaTower");
            for (int i = 0; i < 4; i++)
                Bx(p, PrimitiveType.Cube, -17.5f + i * 0.47f, 8.4f, 14.8f, 0.3f, 0.36f, 0.3f, tower, "Merlon");
            Wq(p, -16.8f, 6.6f, 14.68f, 0.24f, 0.5f, Mats.Unlit(L.Window));
            // the lights of the old town shimmer on the water below
            for (int i = 0; i < 5; i++)
                Prim.FlatQuad(p, new Vector3(-14.5f + i * 1.6f, 0.035f, 12.2f + (i % 2) * 0.6f), 0.4f, 2.4f, new Color(1f, 0.74f, 0.36f, 0.45f), null, true).name = "Glimmer";

            // a corsair ship at anchor: dark hull, two masts, dark sails and a lantern at the stern
            Material hull = Mats.Lit(new Color(0.20f, 0.12f, 0.08f), 0.2f);
            Material sail = Mats.Lit(new Color(0.16f, 0.14f, 0.16f), 0.05f);
            float sx = 9f, sz = 26f;
            Bx(p, PrimitiveType.Cube, sx, 0.5f, sz, 5.2f, 1.0f, 1.5f, hull, "Hull");
            Bx(p, PrimitiveType.Cube, sx + 2.3f, 1.2f, sz, 1.2f, 0.7f, 1.5f, hull, "Stern");
            Tilted(p, PrimitiveType.Cube, sx - 3.2f, 1.1f, sz, 1.8f, 0.1f, 0.1f, 18f, hull, "Bowsprit");
            for (int m = 0; m < 2; m++)
            {
                float mx = sx - 1.3f + m * 2.2f;
                Bx(p, PrimitiveType.Cylinder, mx, 3.4f, sz, 0.09f, 2.9f, 0.09f, hull, "Mast");
                Bx(p, PrimitiveType.Cube, mx, 3.6f, sz - 0.1f, 1.8f, 2.6f, 0.03f, sail, "Sail");
                Bx(p, PrimitiveType.Cube, mx, 5.4f, sz - 0.1f, 1.3f, 0.9f, 0.03f, sail, "TopSail");
            }
            Bx(p, PrimitiveType.Sphere, sx + 2.9f, 1.8f, sz - 0.4f, 0.3f, 0.36f, 0.3f, Mats.Unlit(L.Window), "ShipLantern");
        }
        else if (_face != 2)
        {
            // the kalimera: a wooden hut on stilts over the water, with a big square net hanging from a boom
            Kalimera(p, -9f, 13.5f);
            Kalimera(p, 11f, 15f);
        }

        // palms along the beach
        for (int i = -3; i <= 3; i++)
        {
            if (i == 0) continue;
            Palm(p, i * 5.0f + 1.0f, 8.6f, 3.2f + (Mathf.Abs(i) % 3) * 0.5f, (i % 2 == 0 ? 0.3f : -0.3f));
        }
    }

    /// <summary>A white minaret with a lit balcony and a pointed lead roof.</summary>
    static void Minaret(Transform p, float x, float z, float h)
    {
        Material white = Mats.Lit(new Color(0.95f, 0.93f, 0.88f), 0.2f);
        Bx(p, PrimitiveType.Cylinder, x, h * 0.5f, z, 0.34f, h * 0.5f, 0.34f, white, "Minaret");
        Bx(p, PrimitiveType.Cylinder, x, h * 0.78f, z, 0.54f, 0.06f, 0.54f, Mats.Unlit(new Color(1f, 0.84f, 0.50f)), "Balcony");
        Cn(p, x, h + 0.45f, z, 0.36f, 0.9f, 0.36f, Mats.Lit(new Color(0.42f, 0.46f, 0.52f), 0.4f, 0.3f), "MinaretTop");
    }

    /// <summary>A fishing hut on stilts with a square net hung from a long boom over the water (the kalimera of the Buna).</summary>
    static void Kalimera(Transform p, float x, float z)
    {
        Material wood = Surf(Theme.Wood, new Color(0.62f, 0.46f, 0.32f), 2.4f, 1.6f, 1.2f, 0.2f);
        Material pole = Mats.Lit(new Color(0.30f, 0.20f, 0.12f), 0.1f);
        Material rope = Mats.Unlit(new Color(0.55f, 0.50f, 0.42f));
        for (int i = 0; i < 4; i++)
            Bx(p, PrimitiveType.Cylinder, x + (i % 2 == 0 ? -0.9f : 0.9f), 0.8f, z + (i < 2 ? -0.6f : 0.6f), 0.1f, 0.8f, 0.1f, pole, "Stilt");
        Bx(p, PrimitiveType.Cube, x, 2.4f, z, 2.4f, 1.6f, 1.6f, wood, "Hut");
        Tilted(p, PrimitiveType.Cube, x, 3.2f, z, 1.75f, 1.75f, 1.5f, 45f, Mats.Lit(new Color(0.40f, 0.30f, 0.22f), 0.1f), "HutRoof");
        Wq(p, x, 2.4f, z - 0.82f, 0.4f, 0.5f, Mats.Unlit(new Color(1f, 0.78f, 0.40f)));

        // the boom leans out to one side, and the net hangs from its end
        float dir = x < 0f ? 1f : -1f;
        float ex = x + dir * 4.2f;
        Tilted(p, PrimitiveType.Cube, x + dir * 2.1f, 3.6f, z - 0.2f, 4.6f, 0.08f, 0.08f, dir * 22f, pole, "Boom");
        float top = 4.6f, size = 2.6f;
        for (int i = 0; i <= 5; i++)
        {
            float k = i / 5f;
            Wq(p, ex - size * 0.5f + k * size, top - size * 0.5f, z - 0.3f, 0.03f, size, rope);
            Wq(p, ex, top - k * size, z - 0.3f, size, 0.03f, rope);
        }
    }
}
