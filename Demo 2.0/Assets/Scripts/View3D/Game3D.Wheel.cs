using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// The city wheel (pick a city, or the whole tour in the hub), the help card, and the round help / sound buttons
/// of the main menu. (Recovered from the last good build after the source file went missing.)
/// </summary>
public partial class Game3D
{
    GameObject _wheelPanel, _helpPanel;
    Text _wheelTitle;
    bool _wheelPoker;
    Button _helpButton;
    readonly List<Button> _wheelButtons = new List<Button>();
    readonly List<Button> _helpButtons = new List<Button>();
    static Sprite _circle;
    static Texture2D _ringTex, _fadeTex;
    GameObject _soundSlash;
    Text _soundIcon;
    int _musicBeforeMute = -1;

    const float WheelOuter = 272f;   // ring radii in UI pixels
    const float WheelInner = 112f;
    const float WheelGap = 3f;       // half the gap between two segments
    const float WheelTex = 512f;     // segment texture size

    bool WheelOpen => _wheelPanel != null && _wheelPanel.activeSelf;
    bool HelpOpen => _helpPanel != null && _helpPanel.activeSelf;

    // ---------- round shapes ----------

    static Sprite Circle()
    {
        if (_circle == null)
        {
            if (_disc == null) _disc = Disc(0f);
            _circle = Sprite.Create(_disc, new Rect(0f, 0f, _disc.width, _disc.height), new Vector2(0.5f, 0.5f), 100f);
        }
        return _circle;
    }

    static Texture2D RingTex()
    {
        if (_ringTex != null) return _ringTex;
        const int n = 256;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, true);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Trilinear;
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n * 2f - 1f;
                float v = (y + 0.5f) / n * 2f - 1f;
                float r = Mathf.Sqrt(u * u + v * v);
                float aa = 1f / n;
                float a = Mathf.Clamp01((1f - r) / aa) * Mathf.Clamp01((r - 0.93f) / aa);
                px[y * n + x] = new Color32(255, 255, 255, (byte)(255f * a));
            }
        tex.SetPixels32(px);
        tex.Apply(true, true);
        _ringTex = tex;
        return tex;
    }

    static Texture2D FadeTex()
    {
        if (_fadeTex != null) return _fadeTex;
        var tex = new Texture2D(1, 32, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        for (int i = 0; i < 32; i++)
        {
            float k = 1f - i / 31f;
            tex.SetPixel(0, i, new Color(0f, 0f, 0f, Mathf.Clamp01((k - 0.35f) / 0.65f) * 0.85f));
        }
        tex.Apply(false, true);
        _fadeTex = tex;
        return tex;
    }

    static Image CircleImg(Transform parent, string name, float size, Color c)
    {
        Image img = At(parent, name, 0.5f, 0.5f, size, size).gameObject.AddComponent<Image>();
        img.sprite = Circle();
        img.color = c;
        img.raycastTarget = false;
        return img;
    }

    static RawImage RingImg(Transform parent, string name, float size, Color c)
    {
        RawImage img = At(parent, name, 0.5f, 0.5f, size, size).gameObject.AddComponent<RawImage>();
        img.texture = RingTex();
        img.color = c;
        img.raycastTarget = false;
        return img;
    }

    Button RoundButton(Transform group, float size, UnityAction onClick, out RawImage ring, Text label, List<Button> list)
    {
        Image hit = CircleImg(group, "Button", size + 8f, new Color(MGold.r * 0.7f, MGold.g * 0.7f, MGold.b * 0.7f, 1f));
        hit.raycastTarget = true;
        hit.transform.SetAsFirstSibling();
        ring = RingImg(group, "Ring", size + 8f, MGold);
        Button btn = hit.gameObject.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;
        btn.onClick.AddListener(delegate { Play(Sfx.Click, 0.6f); });
        btn.onClick.AddListener(onClick);
        MenuCardHover hover = hit.gameObject.AddComponent<MenuCardHover>();
        hover.Target = group;
        hover.Frame = ring;
        hover.FrameNormal = MGold;
        hover.FrameHover = new Color(1f, 0.92f, 0.6f);
        hover.Play = label;
        hover.PlayNormal = label != null ? label.color : Color.white;
        hover.PlayHover = MGoldText;
        list?.Add(btn);
        return btn;
    }

    // ---------- help and sound buttons ----------

    Button BuildHelpButton(Transform m)
    {
        RectTransform g = At(m, "Help", 0.045f, 0.075f, 58f, 58f);
        CircleImg(g, "Fill", 58f, new Color(0.035f, 0.15f, 0.1f, 0.96f));
        Text q = MText(g, "Q", V(0f, 0f), V(1f, 1f), "<b>?</b>", Serif, 32, FontStyle.Normal, MGoldText, TextAnchor.MiddleCenter);
        DropShadow(q, 1.5f, 0.6f);
        return RoundButton(g, 50f, delegate { OpenHelp(); }, out _, q, null);
    }

    Button BuildSoundButton(Transform m)
    {
        RectTransform g = At(m, "Sound", 0.955f, 0.075f, 58f, 58f);
        CircleImg(g, "Fill", 58f, new Color(0.035f, 0.15f, 0.1f, 0.96f));
        _soundIcon = MText(g, "Note", V(0f, 0f), V(1f, 1f), "♫", Mats.Font, 28, FontStyle.Bold, MGoldText, TextAnchor.MiddleCenter);
        DropShadow(_soundIcon, 1.5f, 0.6f);
        RectTransform slash = At(g, "Muted", 0.5f, 0.5f, 44f, 5f, 45f);
        Image si = slash.gameObject.AddComponent<Image>();
        si.color = new Color(0.85f, 0.15f, 0.18f, 1f);
        si.raycastTarget = false;
        _soundSlash = slash.gameObject;
        Button b = RoundButton(g, 50f, delegate { ToggleMute(); }, out _, _soundIcon, null);
        RefreshSoundButton();
        return b;
    }

    void ToggleMute()
    {
        if (_musicMode != 2)
        {
            _musicBeforeMute = _musicMode;
            _musicMode = 2;
        }
        else
        {
            _musicMode = _musicBeforeMute >= 0 ? _musicBeforeMute : (Music.Anthem != null ? 0 : 1);
            if (_musicMode == 0 && Music.Anthem == null) _musicMode = 1;
        }
        ApplyMusic();
        RefreshHud();
    }

    void RefreshSoundButton()
    {
        if (_soundSlash == null) return;
        _soundSlash.SetActive(_musicMode == 2);
        _soundIcon.text = "♫";
    }

    // ---------- help card ----------

    void BuildHelp(Transform m)
    {
        Image dim = NewPanel(m, "HelpOverlay", V(0f, 0f), V(1f, 1f), new Color(0f, 0f, 0f, 0.72f));
        dim.raycastTarget = true;
        _helpPanel = dim.gameObject;
        Transform o = _helpPanel.transform;
        Vector2 a = V(0.1f, 0.06f), b = V(0.9f, 0.94f);
        RoundRect(o, "Frame", a, b, 0f, MGold, 8, 6f);
        Image card = RoundRect(o, "Card", a, b, 2f, new Color(0.06f, 0.03f, 0.04f, 0.98f), 8, 5f);
        card.raycastTarget = true;
        Transform c = card.transform;
        NewRaw(c, "Glow", V(0.1f, 0.4f), V(0.9f, 1.3f), _glow, new Color(0.55f, 0.08f, 0.12f, 0.35f), new Rect(0f, 0f, 1f, 1f));
        DropShadow(MText(c, "Title", V(0f, 0.885f), V(1f, 0.975f), "SI LUHET", Serif, 36, FontStyle.Bold, MGoldText, TextAnchor.MiddleCenter), 2f, 0.6f);
        NewPanel(c, "Rule", V(0.38f, 0.875f), V(0.62f, 0.878f), MGold);
        NewPanel(c, "Divider", V(0.4995f, 0.17f), V(0.5005f, 0.84f), new Color(MGold.r, MGold.g, MGold.b, 0.4f));
        HelpColumn(c, 0.035f, 0.48f, "♠  POKER TEXAS HOLD'EM",
            "<b>Qëllimi:</b> fito lekët e potit, me dorën më të mirë ose duke i bërë të tjerët të heqin dorë.\n\n" +
            "• Secili lojtar merr <b>2 letra të fshehura</b>. Dy lojtarë vënë <b>bllindat</b> (baste të detyruara).\n" +
            "• Në mes dalin <b>5 letra të përbashkëta</b>: 3 (flopi), pastaj 1 (turni), pastaj 1 (riveri).\n" +
            "• Para çdo radhe letrash vjen një raund bastesh: <b>Hiq dorë</b>, <b>Kalo</b> (kur s'ka bast), <b>Barazo</b> bastin, ose <b>Ngre</b> më shumë.\n" +
            "• Në fund fiton dora më e mirë me <b>5 letra nga 7</b> (2 të tuat + 5 të tavolinës).\n\n" +
            "<b>Duart, nga më e forta:</b>\nShkallë ngjyrë · Katërshe · Full · Ngjyrë · Shkallë · Treshe · Dy çifte · Çift · Letër e lartë\n\n" +
            "<color=#e9c46a>Ndihmë</color> të tregon fuqinë e dorës dhe gjasat për të fituar.");
        HelpColumn(c, 0.52f, 0.965f, "♥  BLACKJACK",
            "<b>Qëllimi:</b> afrohu sa më shumë te <b>21</b> pa e kaluar, dhe mund krupierin.\n\n" +
            "• Letrat 2–10 vlejnë sa numri, <b>J, Q, K</b> vlejnë 10, <b>Asi</b> vlen 1 ose 11.\n" +
            "• Zgjidh <b>bastin</b> dhe ndaj letrat: merr 2 letra, krupieri tregon vetëm njërën nga të tijat.\n" +
            "• <b>Merr letër</b> për t'u afruar te 21, <b>Qëndro</b> kur je mirë, <b>Dyfisho</b> për të dyfishuar bastin dhe marrë vetëm një letër.\n" +
            "• Mbi 21 humbet menjëherë. Krupieri merr letra derisa të ketë të paktën <b>17</b>.\n" +
            "• <b>Blackjack</b> (As + letër 10 me dy letrat e para) paguan 3:2. Barazimi ta kthen bastin.\n\n" +
            "<b>Qytetet dhe turi</b>\nZgjidh një qytet për të luajtur aty, ose bëj <b>turin e plotë</b>: oda e burrave, Shkodër, Berat, Prishtinë, Tiranë, Sarandë dhe Ulqin, njëri pas tjetrit si nivele. Në poker fito kundër të tre lojtarëve; në blackjack arrij qëllimin e lekëve.");
        MText(c, "Keys", V(0.03f, 0.1f), V(0.97f, 0.16f), "Tab / Enter: zgjidh në menu  ·  Esc: pauzë ose kthehu  ·  Tërhiq me miun: shiko përreth tavolinës",
            Mats.Font, 13, FontStyle.Italic, new Color(MCream.r, MCream.g, MCream.b, 0.75f), TextAnchor.MiddleCenter).horizontalOverflow = HorizontalWrapMode.Wrap;
        UiBtn close = NewButton(c, "Mbyll", V(0.42f, 0.025f), V(0.58f, 0.09f), delegate { CloseOverlay(); }, new Color(0.47f, 0.06f, 0.1f), 17);
        Glow(close.B);
        _helpButtons.Add(close.B);
        _helpPanel.SetActive(false);
    }

    static void HelpColumn(Transform c, float x0, float x1, string head, string body)
    {
        MText(c, "Head", V(x0, 0.78f), V(x1, 0.84f), head, Serif, 21, FontStyle.Bold, MGoldText, TextAnchor.MiddleLeft);
        Text t = MText(c, "Body", V(x0, 0.17f), V(x1, 0.775f), body, Mats.Font, 14, FontStyle.Normal, MCream, TextAnchor.UpperLeft);
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Truncate;
        t.resizeTextForBestFit = true;
        t.resizeTextMinSize = 10;
        t.resizeTextMaxSize = 14;
        t.lineSpacing = 1.05f;
    }

    void OpenHelp()
    {
        if (_helpPanel == null) return;
        _helpPanel.SetActive(true);
        ClearSelection();
    }

    // ---------- city wheel ----------

    void BuildWheel(Transform m)
    {
        Image dim = NewPanel(m, "WheelOverlay", V(0f, 0f), V(1f, 1f), new Color(0.015f, 0.01f, 0.02f, 0.8f));
        dim.raycastTarget = true;
        _wheelPanel = dim.gameObject;
        Transform o = _wheelPanel.transform;
        _wheelTitle = MText(o, "Title", V(0f, 0.885f), V(1f, 0.975f), "", Serif, 36, FontStyle.Bold, MGoldText, TextAnchor.MiddleCenter);
        DropShadow(_wheelTitle, 2f, 0.6f);
        MText(o, "Sub", V(0f, 0.835f), V(1f, 0.885f), "Zgjidh qytetin ku do të luash, ose bëj turin e plotë në mes", Serif, 17, FontStyle.Italic, MCream, TextAnchor.MiddleCenter);

        RectTransform back = At(o, "Back", 0.955f, 0.925f, 46f, 46f);
        CircleImg(back, "Fill", 46f, new Color(0.47f, 0.06f, 0.1f, 1f));
        Text x = MText(back, "X", V(0f, 0f), V(1f, 1f), "×", Mats.Font, 26, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
        Button backBtn = RoundButton(back, 40f, delegate { CloseOverlay(); }, out _, x, null);

        float size = WheelOuter * 2f;
        Transform wheel = At(o, "Wheel", 0.5f, 0.42f, size, size);
        NewRaw(wheel, "Glow", V(-0.15f, -0.15f), V(1.15f, 1.15f), _glow, new Color(0.55f, 0.1f, 0.12f, 0.4f), new Rect(0f, 0f, 1f, 1f));
        CircleImg(wheel, "Back", size + 14f, new Color(0.05f, 0.02f, 0.03f, 0.9f));
        RingImg(wheel, "OuterRing", size + 14f, MGold);
        _wheelButtons.Clear();
        const int n = 7;
        for (int i = 0; i < n; i++) BuildSegment(wheel, i, n, size);
        CircleImg(wheel, "Hole", 222f, new Color(0.05f, 0.02f, 0.03f, 1f));
        RingImg(wheel, "InnerRing", 222f, MGold);
        BuildHubButton(wheel, 198f);
        // Tab order: the whole tour (hub) first, then the cities, then the close button
        _wheelButtons.Insert(0, _wheelButtons[_wheelButtons.Count - 1]);
        _wheelButtons.RemoveAt(_wheelButtons.Count - 1);
        _wheelButtons.Add(backBtn);
        _wheelPanel.SetActive(false);
    }

    void BuildSegment(Transform h, int idx, int n, float size)
    {
        int city = idx - 1;                 // segment 0 is the oda (map -1)
        Sq.MapInfo info = Sq.Maps[idx];
        float centre = 90f - idx * 360f / n;
        float half = 180f / n;
        RectTransform seg = At(h, "Segment " + info.Name, 0.5f, 0.5f, size, size);
        RawImage img = seg.gameObject.AddComponent<RawImage>();
        img.texture = SegmentTexture(CityArt.Get(city), centre, half);
        SegmentHit hit = seg.gameObject.AddComponent<SegmentHit>();
        hit.Centre = centre;
        hit.Half = half - 0.3f;
        hit.Inner = WheelInner;
        hit.Outer = WheelOuter;
        Color normal = new Color(0.86f, 0.86f, 0.86f, 1f);
        img.color = normal;
        img.raycastTarget = true;

        float labelR = (WheelInner + WheelOuter) * 0.5f;
        float rad = centre * Mathf.Deg2Rad;
        Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
        RectTransform nameRt = At(seg, "Name", 0.5f, 0.5f, 150f, 30f);
        nameRt.anchoredPosition = dir * (labelR - 12f);
        Text t = nameRt.gameObject.AddComponent<Text>();
        t.font = Serif;
        t.fontSize = 19;
        t.alignment = TextAnchor.MiddleCenter;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.supportRichText = true;
        t.raycastTarget = false;
        t.color = Color.white;
        t.text = "<b>" + info.Name + "</b>";
        DropShadow(t, 2f, 0.9f);
        t.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.5f);

        Button btn = seg.gameObject.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;
        btn.onClick.AddListener(delegate { Play(Sfx.Click, 0.6f); });
        btn.onClick.AddListener(delegate { PlayWheel(city); });
        MenuCardHover hover = seg.gameObject.AddComponent<MenuCardHover>();
        hover.Target = seg;
        hover.Frame = img;
        hover.FrameNormal = normal;
        hover.FrameHover = Color.white;
        hover.Play = t;
        hover.PlayNormal = Color.white;
        hover.PlayHover = MGoldText;
        hover.Push = dir * 10f;
        _wheelButtons.Add(btn);
    }

    /// <summary>One ring segment with the city art cropped into it and a gold edge, drawn into a WheelTex-sized texture.</summary>
    static Texture2D SegmentTexture(Texture2D art, float centre, float half)
    {
        int n = (int)WheelTex;
        float scale = WheelTex / (WheelOuter * 2f);
        float outer = WheelOuter * scale;
        float inner = WheelInner * scale;
        float gap = WheelGap * scale;
        float edge = 3.5f * scale;
        float mid = n * 0.5f;

        // bounding box of the segment, so the art fills it
        float minX = 1e9f, minY = 1e9f, maxX = -1e9f, maxY = -1e9f;
        for (int i = 0; i <= 16; i++)
        {
            float a = (centre - half + 2f * half * i / 16f) * Mathf.Deg2Rad;
            foreach (float r in new[] { inner, outer })
            {
                float px = mid + Mathf.Cos(a) * r;
                float py = mid + Mathf.Sin(a) * r;
                minX = Mathf.Min(minX, px); maxX = Mathf.Max(maxX, px);
                minY = Mathf.Min(minY, py); maxY = Mathf.Max(maxY, py);
            }
        }
        float box = Mathf.Max(maxX - minX, maxY - minY);
        float bx = (minX + maxX) * 0.5f;
        float by = (minY + maxY) * 0.5f;

        var pix = new Color32[n * n];
        Color gold = MGold;
        Color dark = new Color(0.45f, 0.32f, 0.1f);
        float halfRad = half * Mathf.Deg2Rad;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = x + 0.5f - mid;
                float dy = y + 0.5f - mid;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                if (r < inner - 2f || r > outer + 2f) continue;
                float off = Mathf.Abs(Mathf.DeltaAngle(centre, Mathf.Atan2(dy, dx) * Mathf.Rad2Deg)) * Mathf.Deg2Rad;
                if (off > halfRad + 0.05f) continue;
                float side = r * Mathf.Sin(halfRad - off);           // distance to the straight sides
                float d = Mathf.Min(Mathf.Min(outer - r, r - inner), side - gap);
                float alpha = Mathf.Clamp01(d + 0.5f);
                if (alpha <= 0f) continue;
                float u = (x + 0.5f - bx) / box + 0.5f;
                float v = (y + 0.5f - by) / box + 0.5f;
                Color c = art.GetPixelBilinear(u, v);
                c = Color.Lerp(c * 0.62f, c, Mathf.Clamp01((r - inner) / (outer - inner) * 1.6f));
                if (d < edge) c = Color.Lerp(gold, dark, Mathf.Clamp01(d / edge));
                else if (d < edge + 1.5f) c = Color.Lerp(dark, c, (d - edge) / 1.5f);
                c.a = alpha;
                pix[y * n + x] = c;
            }
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        tex.SetPixels32(pix);
        tex.Apply(false, true);
        return tex;
    }

    void BuildHubButton(Transform h, float size)
    {
        RectTransform g = At(h, "WholeTour", 0.5f, 0.5f, size, size);
        CircleImg(g, "Red", size, MRed);
        NewRaw(CircleImg(g, "Inner", size - 12f, new Color(0.55f, 0.07f, 0.12f, 1f)).transform, "Glow", V(0f, 0f), V(1f, 1f), _glow, new Color(0.95f, 0.25f, 0.25f, 0.35f), new Rect(0f, 0f, 1f, 1f));
        RingImg(g, "InnerRing", size - 12f, new Color(MGold.r, MGold.g, MGold.b, 0.6f));
        RectTransform eagleRt = At(g, "Eagle", 0.5f, 0.5f, size * 0.52f, size * 0.52f);
        eagleRt.anchoredPosition = new Vector2(0f, size * 0.14f);
        RawImage eagle = eagleRt.gameObject.AddComponent<RawImage>();
        eagle.texture = Theme.Eagle;
        eagle.color = new Color(0.06f, 0.04f, 0.05f, 0.95f);
        eagle.raycastTarget = false;
        Text name = MText(g, "Name", V(0f, 0.22f), V(1f, 0.38f), "<b>TURI I PLOTË</b>", Serif, 17, FontStyle.Normal, MGoldText, TextAnchor.MiddleCenter);
        DropShadow(name, 1.5f, 0.6f);
        MText(g, "Sub", V(0f, 0.11f), V(1f, 0.23f), "7 nivele, nga oda në Ulqin", Mats.Font, 11, FontStyle.Italic, MCream, TextAnchor.MiddleCenter);
        RoundButton(g, size, delegate { PlayWheel(-2); }, out _, name, _wheelButtons);
    }

    void OpenWheel(bool poker)
    {
        if (_wheelPanel == null && _menuRootT != null)
        {
            BuildWheel(_menuRootT);
            CityArt.Release();              // the segments have their own textures now
        }
        if (_wheelPanel == null) return;
        _wheelPoker = poker;
        _wheelTitle.text = poker ? "POKER TEXAS HOLD'EM" : "BLACKJACK";
        _wheelPanel.SetActive(true);
        ClearSelection();
    }

    /// <summary>city: -2 = the whole tour, -1 = the oda, 0.. = a city map.</summary>
    void PlayWheel(int city)
    {
        CloseOverlay();
        ClearSelection();
        if (city == -2)
        {
            if (_wheelPoker) StartTournament();
            else StartBjTour();
            return;
        }
        _map = city;
        if (_wheelPoker) StartFreePlay();
        else StartBjFree();
    }

    /// <summary>Closes the city wheel or the help card; true if one was open.</summary>
    bool CloseOverlay()
    {
        bool was = WheelOpen || HelpOpen;
        if (_wheelPanel != null) _wheelPanel.SetActive(false);
        if (_helpPanel != null) _helpPanel.SetActive(false);
        if (was) ClearSelection();
        return was;
    }
}

/// <summary>Only clicks inside the ring segment count (the segment's rect is the whole wheel).</summary>
public class SegmentHit : MonoBehaviour, ICanvasRaycastFilter
{
    public float Centre, Half, Inner, Outer;

    public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
    {
        var rt = (RectTransform)transform;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, screenPoint, eventCamera, out Vector2 p)) return false;
        p -= rt.rect.center;
        float r = p.magnitude;
        if (r < Inner || r > Outer) return false;
        return Mathf.Abs(Mathf.DeltaAngle(Centre, Mathf.Atan2(p.y, p.x) * Mathf.Rad2Deg)) <= Half;
    }
}
