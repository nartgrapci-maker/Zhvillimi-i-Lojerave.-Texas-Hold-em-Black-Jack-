using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Smooth meshes made at runtime. Unity's built-in cylinder (20 sides) and sphere look faceted on a big table, so
/// the game swaps them for these (same size: radius 0.5, cylinder height 2, capsule height 2) with 64 sides and smooth
/// normals. Everything is built by revolving a small profile around the Y axis.
/// </summary>
public static class MeshKit
{
    static Mesh _cylinder, _sphere, _capsule, _cone;
    static readonly Dictionary<string, Mesh> _tori = new Dictionary<string, Mesh>();

    public static Mesh Cylinder { get { if (_cylinder == null) _cylinder = BuildCylinder(72); return _cylinder; } }
    public static Mesh Sphere { get { if (_sphere == null) _sphere = BuildSphere(64, 32); return _sphere; } }
    public static Mesh Cone { get { if (_cone == null) _cone = BuildCone(72); return _cone; } }
    public static Mesh Capsule { get { if (_capsule == null) _capsule = BuildCapsule(48, 14); return _capsule; } }

    /// <summary>A ring (donut) with the given centre-line radius and tube radius, e.g. a rounded table edge.</summary>
    public static Mesh Torus(float radius, float tube)
    {
        string key = radius.ToString("0.###") + "/" + tube.ToString("0.###");
        Mesh m;
        if (_tori.TryGetValue(key, out m) && m != null) return m;
        const int steps = 20;
        var r = new List<float>();
        var y = new List<float>();
        var nr = new List<float>();
        var ny = new List<float>();
        for (int i = 0; i <= steps; i++)
        {
            float a = i / (float)steps * 2f * Mathf.PI;
            r.Add(radius + tube * Mathf.Cos(a));
            y.Add(tube * Mathf.Sin(a));
            nr.Add(Mathf.Cos(a));
            ny.Add(Mathf.Sin(a));
        }
        m = Lathe(r, y, nr, ny, 96);
        m.name = "Torus " + key;
        _tori[key] = m;
        return m;
    }

    /// <summary>Swaps the mesh of a freshly created primitive for the smooth version (no effect for cubes and quads).</summary>
    public static void Smooth(GameObject go, PrimitiveType type)
    {
        MeshFilter mf = go.GetComponent<MeshFilter>();
        if (mf == null) return;
        switch (type)
        {
            case PrimitiveType.Cylinder: mf.sharedMesh = Cylinder; break;
            case PrimitiveType.Sphere: mf.sharedMesh = Sphere; break;
            case PrimitiveType.Capsule: mf.sharedMesh = Capsule; break;
        }
    }

    // ---------------------------------------------------------------------------------------
    // shapes
    // ---------------------------------------------------------------------------------------

    static Mesh BuildCylinder(int seg)
    {
        // flat top, side, flat bottom: the corners are doubled so each part keeps its own normal
        var r = new List<float> { 0f, 0.5f, 0.5f, 0.5f, 0.5f, 0f };
        var y = new List<float> { 1f, 1f, 1f, -1f, -1f, -1f };
        var nr = new List<float> { 0f, 0f, 1f, 1f, 0f, 0f };
        var ny = new List<float> { 1f, 1f, 0f, 0f, -1f, -1f };
        Mesh m = Lathe(r, y, nr, ny, seg);
        m.name = "SmoothCylinder";
        return m;
    }

    static Mesh BuildCone(int seg)
    {
        // apex at y = +1, base circle (radius 0.5) at y = -1, flat base
        float nr0 = 2f / 2.0616f, ny0 = 0.5f / 2.0616f;      // outward normal of the slanted side
        var r = new List<float> { 0f, 0.5f, 0.5f, 0f };
        var y = new List<float> { 1f, -1f, -1f, -1f };
        var nr = new List<float> { nr0, nr0, 0f, 0f };
        var ny = new List<float> { ny0, ny0, -1f, -1f };
        Mesh m = Lathe(r, y, nr, ny, seg);
        m.name = "Cone";
        return m;
    }

    static Mesh BuildSphere(int seg, int rings)
    {
        var r = new List<float>();
        var y = new List<float>();
        var nr = new List<float>();
        var ny = new List<float>();
        for (int i = 0; i <= rings; i++)
        {
            float t = i / (float)rings * Mathf.PI;
            r.Add(0.5f * Mathf.Sin(t));
            y.Add(0.5f * Mathf.Cos(t));
            nr.Add(Mathf.Sin(t));
            ny.Add(Mathf.Cos(t));
        }
        Mesh m = Lathe(r, y, nr, ny, seg);
        m.name = "SmoothSphere";
        return m;
    }

    static Mesh BuildCapsule(int seg, int capRings)
    {
        var r = new List<float>();
        var y = new List<float>();
        var nr = new List<float>();
        var ny = new List<float>();
        for (int i = 0; i <= capRings; i++)            // upper hemisphere, centre at y = +0.5
        {
            float t = i / (float)capRings * Mathf.PI * 0.5f;
            r.Add(0.5f * Mathf.Sin(t));
            y.Add(0.5f + 0.5f * Mathf.Cos(t));
            nr.Add(Mathf.Sin(t));
            ny.Add(Mathf.Cos(t));
        }
        for (int i = 0; i <= capRings; i++)            // lower hemisphere, centre at y = -0.5
        {
            float t = Mathf.PI * 0.5f + i / (float)capRings * Mathf.PI * 0.5f;
            r.Add(0.5f * Mathf.Sin(t));
            y.Add(-0.5f + 0.5f * Mathf.Cos(t));
            nr.Add(Mathf.Sin(t));
            ny.Add(Mathf.Cos(t));
        }
        Mesh m = Lathe(r, y, nr, ny, seg);
        m.name = "SmoothCapsule";
        return m;
    }

    /// <summary>
    /// Revolves a profile (radius, height, and the outward normal of each point as radial and vertical parts)
    /// around the Y axis. Triangles are wound so that they face the way the normals point.
    /// </summary>
    static Mesh Lathe(List<float> r, List<float> y, List<float> nr, List<float> ny, int seg)
    {
        int rows = r.Count;
        int cols = seg + 1;
        var verts = new Vector3[rows * cols];
        var norms = new Vector3[rows * cols];
        var uvs = new Vector2[rows * cols];
        for (int j = 0; j < rows; j++)
        {
            for (int i = 0; i < cols; i++)
            {
                float a = i / (float)seg * 2f * Mathf.PI;
                float c = Mathf.Cos(a), s = Mathf.Sin(a);
                int k = j * cols + i;
                verts[k] = new Vector3(r[j] * c, y[j], r[j] * s);
                norms[k] = new Vector3(nr[j] * c, ny[j], nr[j] * s);
                uvs[k] = new Vector2(i / (float)seg, j / (float)(rows - 1));
            }
        }

        var tris = new List<int>();
        for (int j = 0; j < rows - 1; j++)
        {
            for (int i = 0; i < seg; i++)
            {
                int a = j * cols + i, b = a + 1, c = a + cols, d = c + 1;
                AddTriangle(tris, verts, norms, a, c, b);
                AddTriangle(tris, verts, norms, b, c, d);
            }
        }

        var mesh = new Mesh();
        mesh.vertices = verts;
        mesh.normals = norms;
        mesh.uv = uvs;
        mesh.triangles = tris.ToArray();
        mesh.RecalculateBounds();
        return mesh;
    }

    static void AddTriangle(List<int> tris, Vector3[] v, Vector3[] n, int a, int b, int c)
    {
        Vector3 cross = Vector3.Cross(v[b] - v[a], v[c] - v[a]);
        if (cross.magnitude < 1e-9f) return;               // collapsed at a pole or at a doubled corner
        Vector3 outward = n[a] + n[b] + n[c];
        // Unity treats a triangle as front-facing when (b - a) x (c - a) points at the viewer
        if (Vector3.Dot(cross, outward) >= 0f) { tris.Add(a); tris.Add(b); tris.Add(c); }
        else { tris.Add(a); tris.Add(c); tris.Add(b); }
    }
}
