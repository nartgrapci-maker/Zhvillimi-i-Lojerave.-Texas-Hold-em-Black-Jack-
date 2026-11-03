using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A little ritual at the table: a glass of tea (çaj rusi) and a cigarette. Two buttons next to the log. In first person
/// your own hand comes up from the bottom of the picture (with the glass or the cigarette, steam and smoke); from above,
/// your player does it. The bots and the people at the other tables do it too, now and then.
/// </summary>
public partial class Game3D
{
    enum Habit { None, Tea, Cig, Pour }

    // table props (always there) and the props that sit in front of the camera
    static readonly Vector3 TeaSpot = new Vector3(1.85f, TableTop, -1.25f);
    static readonly Vector3 AshSpot = new Vector3(-1.85f, TableTop, -1.25f);
    GameObject _tableTea, _ashCig;
    Transform _tableTeaLiquid;
    Renderer _ashEmber;
    readonly List<GameObject> _butts = new List<GameObject>();
    GameObject _fpRoot, _fpGlass, _fpCig;
    Transform _fpLiquid;
    Renderer _fpEmber;

    Habit _habit;
    float _habitT, _nextWisp;
    bool _habitFp, _habitMark, _habitMark2;
    float _teaLevel = 1f;                // how full the glass is (0..1); one sip is 0.2
    int _cigDrags;                       // drags left in the cigarette that is lit (0 = none lit)
    int _cigsSmoked;

    const float TeaTime = 3.0f, CigTime = 4.6f, PourTime = 1.3f;

    // ------------------------------------------------------------------------------------------
    // building
    // ------------------------------------------------------------------------------------------

    /// <summary>A glass of tea in a brass holder, origin at the bottom of the glass. Returns the group; liquid receives the tea.</summary>
    static GameObject BuildTeaGlass(Transform parent, Vector3 localPos, float scale, bool saucer, out Transform liquid)
    {
        var g = new GameObject("TeaGlass");
        g.transform.SetParent(parent, false);
        g.transform.localPosition = localPos;
        g.transform.localScale = Vector3.one * scale;
        Material amber = Mats.Lit(new Color(0.72f, 0.32f, 0.07f), 0.8f);
        Material glass = Mats.Lit(new Color(0.88f, 0.94f, 0.96f), 0.9f);
        Material brass = Mats.Lit(new Color(0.82f, 0.64f, 0.24f), 0.7f, 0.6f);
        if (saucer) Prim.Make(PrimitiveType.Cylinder, g.transform, new Vector3(0f, 0.004f, 0f), new Vector3(0.15f, 0.004f, 0.15f), brass, "Saucer");
        Prim.Make(PrimitiveType.Cylinder, g.transform, new Vector3(0f, 0.05f, 0.0f), new Vector3(0.066f, 0.045f, 0.066f), glass, "Glass");
        GameObject l = Prim.Make(PrimitiveType.Cylinder, g.transform, new Vector3(0f, 0.045f, 0f), new Vector3(0.058f, 0.04f, 0.058f), amber, "Tea");
        liquid = l.transform;
        Prim.Make(PrimitiveType.Cylinder, g.transform, new Vector3(0f, 0.024f, 0f), new Vector3(0.076f, 0.022f, 0.076f), brass, "Holder");
        Prim.Make(PrimitiveType.Cylinder, g.transform, new Vector3(0f, 0.099f, 0f), new Vector3(0.072f, 0.004f, 0.072f), brass, "HolderRim");
        for (int i = 0; i < 4; i++)
        {
            float a = i * 90f * Mathf.Deg2Rad;
            Prim.Make(PrimitiveType.Cube, g.transform, new Vector3(Mathf.Cos(a) * 0.036f, 0.07f, Mathf.Sin(a) * 0.036f), new Vector3(0.005f, 0.06f, 0.005f), brass, "Bar");
        }
        Prim.Make(PrimitiveType.Cube, g.transform, new Vector3(0.062f, 0.055f, 0f), new Vector3(0.022f, 0.006f, 0.012f), brass, "HandleTop");
        Prim.Make(PrimitiveType.Cube, g.transform, new Vector3(0.072f, 0.04f, 0f), new Vector3(0.006f, 0.04f, 0.012f), brass, "Handle");
        SetLiquid(liquid, 1f);
        return g;
    }

    static void SetLiquid(Transform liquid, float level)
    {
        if (liquid == null) return;
        float h = 0.078f * Mathf.Max(0.02f, level);
        liquid.localScale = new Vector3(0.058f, h * 0.5f, 0.058f);
        liquid.localPosition = new Vector3(0f, 0.012f + h * 0.5f, 0f);
        liquid.gameObject.SetActive(level > 0.04f);
    }

    static void NoShadows(GameObject go)
    {
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
        {
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }
    }

    /// <summary>The glass of tea and the ashtray on the table in front of the player.</summary>
    void BuildHabitProps(Transform w)
    {
        _tableTea = BuildTeaGlass(w, TeaSpot, 1.5f, true, out _tableTeaLiquid);

        // the ashtray: a dark glazed dish with a notch for the cigarette
        Material glaze = Mats.Lit(new Color(0.16f, 0.14f, 0.16f), 0.6f);
        Material inner = Mats.Lit(new Color(0.07f, 0.06f, 0.07f), 0.5f);
        var ash = new GameObject("Ashtray");
        ash.transform.SetParent(w, false);
        ash.transform.localPosition = AshSpot;
        Prim.Make(PrimitiveType.Cylinder, ash.transform, new Vector3(0f, 0.014f, 0f), new Vector3(0.24f, 0.014f, 0.24f), glaze, "Dish");
        Prim.Make(PrimitiveType.Cylinder, ash.transform, new Vector3(0f, 0.0285f, 0f), new Vector3(0.20f, 0.002f, 0.20f), inner, "Hollow");
        for (int i = 0; i < 6; i++)
        {
            GameObject b = Prim.Make(PrimitiveType.Cylinder, ash.transform, new Vector3(-0.05f + (i % 3) * 0.045f, 0.036f, -0.03f + (i / 3) * 0.07f), new Vector3(0.012f, 0.014f, 0.012f), Mats.Lit(new Color(0.85f, 0.8f, 0.7f), 0.2f), "Butt");
            b.transform.localRotation = Quaternion.Euler(80f, i * 50f, 0f);
            b.SetActive(false);
            _butts.Add(b);
        }
        // the lit cigarette resting on the rim
        _ashCig = new GameObject("RestingCigarette");
        _ashCig.transform.SetParent(ash.transform, false);
        _ashCig.transform.localPosition = new Vector3(0f, 0.05f, 0.02f);
        _ashCig.transform.localRotation = Quaternion.Euler(78f, 20f, 0f);
        Prim.Make(PrimitiveType.Cylinder, _ashCig.transform, new Vector3(0f, 0.045f, 0f), new Vector3(0.014f, 0.045f, 0.014f), Mats.Lit(new Color(0.97f, 0.97f, 0.95f), 0.2f), "Cigarette");
        Prim.Make(PrimitiveType.Cylinder, _ashCig.transform, new Vector3(0f, 0.015f, 0f), new Vector3(0.0145f, 0.016f, 0.0145f), Mats.Lit(new Color(0.78f, 0.52f, 0.22f), 0.2f), "Filter");
        _ashEmber = Prim.Make(PrimitiveType.Sphere, _ashCig.transform, new Vector3(0f, 0.093f, 0f), new Vector3(0.017f, 0.017f, 0.017f), Mats.Unlit(new Color(1f, 0.45f, 0.1f)), "Ember").GetComponent<Renderer>();
        _ashCig.SetActive(false);
        NoShadows(_tableTea);
        NoShadows(ash);
    }

    /// <summary>Your hand with the glass and your hand with the cigarette, parented to the camera and hidden below the picture.</summary>
    void BuildFpProps()
    {
        if (_cam == null) return;
        _fpRoot = new GameObject("FirstPersonProps");
        _fpRoot.transform.SetParent(_cam.transform, false);
        Material skin = Mats.Lit(Skin0, 0.3f), sleeve = Mats.Lit(Theme.Cream, 0.3f), gold = Mats.Lit(Theme.Gold, 0.5f, 0.3f);

        _fpGlass = new GameObject("FpGlassHand");
        _fpGlass.transform.SetParent(_fpRoot.transform, false);
        Transform lq;
        BuildTeaGlass(_fpGlass.transform, new Vector3(0f, -0.05f, 0f), 1.5f, false, out lq);
        _fpLiquid = lq;
        // the hand around the glass and a wide white sleeve coming in from the bottom right
        Prim.Make(PrimitiveType.Sphere, _fpGlass.transform, new Vector3(0.0f, -0.02f, -0.05f), new Vector3(0.11f, 0.10f, 0.08f), skin, "Palm");
        Prim.Make(PrimitiveType.Sphere, _fpGlass.transform, new Vector3(0.0f, 0.02f, 0.045f), new Vector3(0.05f, 0.09f, 0.05f), skin, "Fingers");
        Prim.Make(PrimitiveType.Sphere, _fpGlass.transform, new Vector3(-0.07f, 0.0f, 0.0f), new Vector3(0.05f, 0.08f, 0.05f), skin, "Thumb");
        GameObject arm = Prim.Make(PrimitiveType.Capsule, _fpGlass.transform, new Vector3(0.12f, -0.30f, -0.10f), new Vector3(0.12f, 0.30f, 0.12f), sleeve, "Sleeve");
        arm.transform.localRotation = Quaternion.Euler(-12f, 0f, 22f);
        Prim.Make(PrimitiveType.Cylinder, _fpGlass.transform, new Vector3(0.045f, -0.11f, -0.075f), new Vector3(0.14f, 0.012f, 0.14f), gold, "Cuff").transform.localRotation = Quaternion.Euler(-12f, 0f, 22f);

        _fpCig = new GameObject("FpCigaretteHand");
        _fpCig.transform.SetParent(_fpRoot.transform, false);
        Prim.Make(PrimitiveType.Sphere, _fpCig.transform, new Vector3(0f, 0f, 0f), new Vector3(0.10f, 0.10f, 0.08f), skin, "Palm");
        Prim.Make(PrimitiveType.Sphere, _fpCig.transform, new Vector3(0.04f, 0.04f, 0.04f), new Vector3(0.04f, 0.07f, 0.05f), skin, "Finger1");
        Prim.Make(PrimitiveType.Sphere, _fpCig.transform, new Vector3(0.0f, 0.045f, 0.045f), new Vector3(0.04f, 0.07f, 0.05f), skin, "Finger2");
        GameObject arm2 = Prim.Make(PrimitiveType.Capsule, _fpCig.transform, new Vector3(-0.10f, -0.30f, -0.10f), new Vector3(0.12f, 0.30f, 0.12f), sleeve, "Sleeve");
        arm2.transform.localRotation = Quaternion.Euler(-12f, 0f, -22f);
        Prim.Make(PrimitiveType.Cylinder, _fpCig.transform, new Vector3(-0.04f, -0.11f, -0.075f), new Vector3(0.14f, 0.012f, 0.14f), gold, "Cuff").transform.localRotation = Quaternion.Euler(-12f, 0f, -22f);
        var cig = new GameObject("Cig");
        cig.transform.SetParent(_fpCig.transform, false);
        cig.transform.localPosition = new Vector3(0.0f, 0.04f, 0.05f);
        cig.transform.localRotation = Quaternion.Euler(0f, 0f, 14f);
        Prim.Make(PrimitiveType.Cylinder, cig.transform, new Vector3(0f, 0.07f, 0f), new Vector3(0.018f, 0.07f, 0.018f), Mats.Lit(new Color(0.97f, 0.97f, 0.95f), 0.2f), "Cigarette");
        Prim.Make(PrimitiveType.Cylinder, cig.transform, new Vector3(0f, 0.02f, 0f), new Vector3(0.0185f, 0.02f, 0.0185f), Mats.Lit(new Color(0.78f, 0.52f, 0.22f), 0.2f), "Filter");
        _fpEmber = Prim.Make(PrimitiveType.Sphere, cig.transform, new Vector3(0f, 0.143f, 0f), new Vector3(0.022f, 0.022f, 0.022f), Mats.Unlit(new Color(1f, 0.45f, 0.1f)), "Ember").GetComponent<Renderer>();

        NoShadows(_fpRoot);
        _fpGlass.SetActive(false);
        _fpCig.SetActive(false);
    }

    static Color Skin0 { get { return new Color(0.96f, 0.80f, 0.66f); } }

    // ------------------------------------------------------------------------------------------
    // the buttons
    // ------------------------------------------------------------------------------------------

    void OnTea()
    {
        if (_habit != Habit.None) return;
        StartHabit(_teaLevel < 0.15f ? Habit.Pour : Habit.Tea);
    }

    void OnCig()
    {
        if (_habit != Habit.None) return;
        StartHabit(Habit.Cig);
    }

    void StartHabit(Habit h)
    {
        _habit = h;
        _habitT = 0f;
        _habitFp = FirstPersonNow;
        _habitMark = _habitMark2 = false;
        AvatarView me = _avatars.Count > 0 ? _avatars[0] : null;
        if (h == Habit.Pour) Play(Sfx.Chip, 0.4f);
        if (h == Habit.Tea && !_habitFp && me != null) me.Sip();
        if (h == Habit.Cig)
        {
            if (_cigDrags <= 0) { _cigDrags = 4; Play(Sfx.Flick, 0.8f); }
            if (!_habitFp && me != null) me.Cigarette();
        }
        RefreshHud();
    }

    /// <summary>Texts of the two buttons.</summary>
    void RefreshHabitButtons()
    {
        if (_bTea == null) return;
        _bTea.T.text = _teaLevel < 0.15f ? "Mbushe" : "Çaj rusi";
        _bCig.T.text = _cigDrags > 0 ? "Thith" : "Cigare";
        _bTea.B.interactable = _habit == Habit.None;
        _bCig.B.interactable = _habit == Habit.None;
    }

    void SetTeaLevels()
    {
        SetLiquid(_tableTeaLiquid, _teaLevel);
        SetLiquid(_fpLiquid, _teaLevel);
        if (_avatars.Count > 0 && _avatars[0] != null) _avatars[0].SetTea(_teaLevel);
    }

    void ResetHabits()
    {
        _habit = Habit.None;
        if (_fpGlass != null) _fpGlass.SetActive(false);
        if (_fpCig != null) _fpCig.SetActive(false);
        if (_tableTea != null) _tableTea.SetActive(true);
        RefreshHabitButtons();
    }

    // ------------------------------------------------------------------------------------------
    // the ritual, once a frame
    // ------------------------------------------------------------------------------------------

    static float Env(float u, float up, float down)
    {
        if (u <= 0f || u >= 1f) return 0f;
        if (u < up) return Mathf.SmoothStep(0f, 1f, u / up);
        if (u > 1f - down) return Mathf.SmoothStep(0f, 1f, (1f - u) / down);
        return 1f;
    }

    void UpdateHabits()
    {
        float dt = Time.deltaTime;
        if (_mode == Mode.Menu) { if (_habit != Habit.None) ResetHabits(); return; }

        // a cigarette that is lit smoulders in the ashtray between drags
        bool resting = _cigDrags > 0 && !(_habit == Habit.Cig);
        if (_ashCig != null && _ashCig.activeSelf != resting) _ashCig.SetActive(resting);
        if (resting && Time.time >= _nextWisp)
        {
            _nextWisp = Time.time + 0.55f;
            Vector3 tip = new Vector3(AshSpot.x, AshSpot.y + 0.10f, AshSpot.z + 0.0f);
            Smoke.Puff(tip, new Vector3(0.0f, 0.10f, 0f), 0.04f, 2.6f, 0.8f);
        }
        if (_ashEmber != null && resting) _ashEmber.sharedMaterial = Mats.Unlit(Color.Lerp(new Color(0.8f, 0.25f, 0.05f), new Color(1f, 0.55f, 0.15f), 0.5f + 0.5f * Mathf.Sin(Time.time * 3f)));
        for (int i = 0; i < _butts.Count; i++) _butts[i].SetActive(i < _cigsSmoked);

        if (_habit == Habit.None) return;
        _habitT += dt;
        Transform cam = _cam.transform;

        if (_habit == Habit.Pour)
        {
            float u = _habitT / PourTime;
            if (_habitFp && _fpGlass != null) { /* the pouring happens on the table */ }
            _teaLevel = Mathf.Lerp(_teaLevel, 1f, Mathf.Clamp01(dt * 3.5f));
            SetTeaLevels();
            if (_habitT >= PourTime) { _teaLevel = 1f; SetTeaLevels(); EndHabit(); }
            return;
        }

        if (_habit == Habit.Tea)
        {
            float u = _habitT / TeaTime;
            if (_habitFp)
            {
                // from below the picture, up to the glass, then up to the lips and back
                float a = Env(u, 0.22f, 0.25f);                 // 0..1: the hand is in the picture
                float b = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((u - 0.40f) / 0.18f)) * (1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((u - 0.70f) / 0.16f)));
                Vector3 hidden = new Vector3(0.16f, -0.62f, 0.42f), hold = new Vector3(0.13f, -0.20f, 0.44f), lips = new Vector3(0.02f, -0.04f, 0.30f);
                Vector3 pos = Vector3.Lerp(Vector3.Lerp(hidden, hold, a), lips, b);
                float tilt = Mathf.Lerp(8f, -62f, b);
                _fpGlass.SetActive(a > 0.01f);
                _tableTea.SetActive(a < 0.02f);
                _fpGlass.transform.localPosition = pos;
                _fpGlass.transform.localRotation = Quaternion.Euler(tilt, 0f, 0f);
                if (a > 0.6f && b < 0.3f && Time.time >= _nextWisp)
                {
                    _nextWisp = Time.time + 0.4f;
                    Smoke.Puff(cam.position + cam.forward * 0.42f + cam.right * 0.12f + cam.up * -0.07f, new Vector3(0f, 0.10f, 0f), 0.04f, 1.6f, 0.95f);
                }
            }
            if (!_habitMark && u > 0.5f)
            {
                _habitMark = true;
                _teaLevel = Mathf.Max(0f, _teaLevel - 0.2f);
                SetTeaLevels();
                Play(Sfx.Sip, 0.8f);
            }
            if (_habitT >= TeaTime) EndHabit();
            return;
        }

        // the cigarette
        float uc = _habitT / CigTime;
        if (_habitFp)
        {
            float a = Env(uc, 0.18f, 0.2f);
            float b = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((uc - 0.22f) / 0.16f)) * (1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((uc - 0.66f) / 0.12f)));
            Vector3 hidden = new Vector3(-0.18f, -0.62f, 0.42f), hold = new Vector3(-0.14f, -0.18f, 0.42f), lips = new Vector3(-0.025f, -0.075f, 0.26f);
            _fpCig.SetActive(a > 0.01f);
            _fpCig.transform.localPosition = Vector3.Lerp(Vector3.Lerp(hidden, hold, a), lips, b);
            _fpCig.transform.localRotation = Quaternion.Euler(Mathf.Lerp(0f, 28f, b), 0f, 0f);
            if (_fpEmber != null) _fpEmber.sharedMaterial = Mats.Unlit(Color.Lerp(new Color(0.85f, 0.25f, 0.05f), new Color(1f, 0.85f, 0.35f), b));
            if (b > 0.9f && Time.time >= _nextWisp)
            {
                _nextWisp = Time.time + 0.5f;
                Smoke.Puff(cam.position + cam.forward * 0.30f + cam.right * -0.03f + cam.up * 0.0f, cam.forward * 0.02f + new Vector3(0f, 0.08f, 0f), 0.04f, 2f, 0.8f);
            }
        }
        if (!_habitMark && uc > 0.74f)
        {
            // breathe the smoke out: a cloud that drifts across the view
            _habitMark = true;
            Vector3 fwd = cam.forward;
            fwd.y = 0f;
            if (fwd.magnitude < 0.1f) fwd = Vector3.forward;
            fwd = fwd.normalized;
            for (int i = 0; i < 6; i++)
                Smoke.Puff(cam.position + fwd * (0.35f + i * 0.06f) + cam.up * -0.10f, fwd * (0.30f + i * 0.05f) + new Vector3((i - 2.5f) * 0.03f, 0.10f, 0f), 0.16f + i * 0.03f, 3.4f, 0.88f);
            _cigDrags--;
            if (_cigDrags <= 0) { _cigsSmoked = Mathf.Min(6, _cigsSmoked + 1); }
        }
        if (_habitT >= CigTime) EndHabit();
    }

    void EndHabit()
    {
        _habit = Habit.None;
        if (_fpGlass != null) _fpGlass.SetActive(false);
        if (_fpCig != null) _fpCig.SetActive(false);
        if (_tableTea != null) _tableTea.SetActive(true);
        SetTeaLevels();
        RefreshHud();
    }
}
