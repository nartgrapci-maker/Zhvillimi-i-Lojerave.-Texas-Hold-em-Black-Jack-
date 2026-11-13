using System;
using System.Collections.Generic;
using PokerDemo.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// The main menu: the title banner, the two game cards (Blackjack / Poker), the help and sound buttons,
/// and Tab / Shift+Tab keyboard navigation over whatever menu card is showing.
/// (Recovered from the last good build after the source file went missing.)
/// </summary>
public partial class Game3D
{
    static readonly Color MGold = new Color(0.86f, 0.68f, 0.3f);
    static readonly Color MGoldText = new Color(0.98f, 0.84f, 0.48f);
    static readonly Color MCream = new Color(0.96f, 0.9f, 0.78f);
    static readonly Color MRed = new Color(0.47f, 0.06f, 0.1f);
    static readonly Color MGreen = new Color(0.035f, 0.15f, 0.1f, 0.94f);
    static readonly Color MInk = new Color(0.08f, 0.07f, 0.09f);
    static readonly Color MInkRed = new Color(0.8f, 0.08f, 0.12f);

    static Font _serif;
    static readonly Dictionary<int, Sprite> _rounded = new Dictionary<int, Sprite>();
    static Texture2D _disc, _chipTop, _glow;
    readonly List<Button> _menuButtons = new List<Button>();
    readonly List<Button> _tabTmp = new List<Button>();
    Transform _menuRootT;

    static Font Serif
    {
        get
        {
            if (_serif == null)
            {
                try
                {
                    _serif = Font.CreateDynamicFontFromOSFont(new[] { "Georgia", "Cambria", "Palatino Linotype", "Book Antiqua", "Times New Roman", "DejaVu Serif" }, 48);
                }
                catch (Exception)
                {
                    _serif = null;
                }
                if (_serif == null) _serif = Mats.Font;
            }
            return _serif;
        }
    }

    // ---------- small drawing helpers ----------

    static Sprite Rounded(int radius)
    {
        if (_rounded.TryGetValue(radius, out Sprite s) && s != null) return s;
        int n = radius * 2 + 4;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float cx = Mathf.Clamp(x + 0.5f, radius, n - radius);
                float cy = Mathf.Clamp(y + 0.5f, radius, n - radius);
                float d = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy)) - radius;
                px[y * n + x] = new Color32(255, 255, 255, (byte)(255f * Mathf.Clamp01(0.5f - d)));
            }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        s = Sprite.Create(tex, new Rect(0f, 0f, n, n), new Vector2(0.5f, 0.5f), 100f, 0u, SpriteMeshType.FullRect,
            new Vector4(radius + 1, radius + 1, radius + 1, radius + 1));
        _rounded[radius] = s;
        return s;
    }

    static Texture2D Disc(float softness)
    {
        var tex = new Texture2D(64, 64, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        var px = new Color32[64 * 64];
        for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
            {
                float u = (x + 0.5f) / 64f * 2f - 1f;
                float v = (y + 0.5f) / 64f * 2f - 1f;
                float r = Mathf.Sqrt(u * u + v * v);
                float a = softness <= 0f ? Mathf.Clamp01((1f - r) * 64f * 0.5f) : Mathf.Clamp01((1f - r) / softness);
                px[y * 64 + x] = new Color32(255, 255, 255, (byte)(255f * a));
            }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        return tex;
    }

    static Texture2D ChipTop()
    {
        var tex = new Texture2D(96, 96, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        var px = new Color32[96 * 96];
        Color red = new Color(0.72f, 0.07f, 0.12f);
        Color inner = new Color(0.82f, 0.16f, 0.2f);
        Color cream = new Color(0.97f, 0.95f, 0.9f);
        for (int y = 0; y < 96; y++)
            for (int x = 0; x < 96; x++)
            {
                float u = (x + 0.5f) / 96f * 2f - 1f;
                float v = (y + 0.5f) / 96f * 2f - 1f;
                float r = Mathf.Sqrt(u * u + v * v);
                float ang = Mathf.Atan2(v, u) / (Mathf.PI * 2f) + 0.5f;
                Color c = red;
                if (r > 0.72f && Mathf.Repeat(ang * 6f, 1f) < 0.32f) c = cream;
                else if (r < 0.58f) c = inner;
                if (r > 0.555f && r < 0.6f) c = cream;
                c.a = Mathf.Clamp01((1f - r) * 96f * 0.5f);
                px[y * 96 + x] = c;
            }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        return tex;
    }

    /// <summary>A rect of a fixed pixel size centred on an anchor point (fx, fy) of its parent.</summary>
    static RectTransform At(Transform parent, string name, float fx, float fy, float w, float h, float rot = 0f)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(fx, fy);
        rt.sizeDelta = new Vector2(w, h);
        rt.localRotation = Quaternion.Euler(0f, 0f, rot);
        return rt;
    }

    static Image RoundRect(Transform parent, string name, Vector2 aMin, Vector2 aMax, float inset, Color color, int radius, float cornerPx)
    {
        RectTransform rt = NewRect(parent, name, aMin, aMax);
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
        Image img = rt.gameObject.AddComponent<Image>();
        img.sprite = Rounded(radius);
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = radius / Mathf.Max(0.5f, cornerPx);
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    static Text MText(Transform parent, string name, Vector2 aMin, Vector2 aMax, string text, Font font, int size, FontStyle style, Color color, TextAnchor anchor)
    {
        Text t = NewRect(parent, name, aMin, aMax).gameObject.AddComponent<Text>();
        t.font = font;
        t.fontSize = size;
        t.fontStyle = style;
        t.color = color;
        t.alignment = anchor;
        t.supportRichText = true;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        t.text = text;
        return t;
    }

    static void DropShadow(Graphic g, float dist, float alpha)
    {
        Shadow s = g.gameObject.AddComponent<Shadow>();
        s.effectColor = new Color(0f, 0f, 0f, alpha);
        s.effectDistance = new Vector2(dist, -dist);
    }

    // ---------- the menu ----------

    GameObject BuildMainMenu(Transform h)
    {
        GameObject root = Group(h, "MainMenu");
        Transform m = root.transform;
        if (_glow == null) _glow = Disc(1f);
        if (_disc == null) _disc = Disc(0f);
        if (_chipTop == null) _chipTop = ChipTop();
        NewRaw(m, "Vignette", V(0.1f, 0.08f), V(0.9f, 0.56f), _glow, new Color(0f, 0f, 0f, 0.45f), new Rect(0f, 0f, 1f, 1f));
        BuildBanner(m);
        BuildGameCard(m, V(0.202f, 0.185f), V(0.488f, 0.455f), "<b>BLACKJACK</b>", 27, "Arrij 21 pa e kaluar, kundër dilerit.", false, delegate { OpenWheel(false); });
        BuildGameCard(m, V(0.519f, 0.185f), V(0.802f, 0.455f), "<b>POKER\nTEXAS\nHOLD'EM</b>", 23, "Ulu në tavolinë me tre lojtarë.", true, delegate { OpenWheel(true); });
        _helpButton = BuildHelpButton(m);
        _menuButtons.Add(_helpButton);
        _menuButtons.Add(BuildSoundButton(m));
        _menuRootT = m;
        BuildHelp(m);
        return root;
    }

    void BuildBanner(Transform m)
    {
        Vector2 a = V(0.146f, 0.578f), b = V(0.857f, 0.94f);
        ((RectTransform)RoundRect(m, "BannerShadow", a, b, -6f, new Color(0f, 0f, 0f, 0.45f), 12, 10f).transform).anchoredPosition = new Vector2(4f, -6f);
        RoundRect(m, "BannerFrame", a, b, 0f, MGold, 6, 3f);
        Transform ban = RoundRect(m, "Banner", a, b, 3f, MRed, 6, 2f).transform;
        RoundRect(ban, "InnerLine", V(0f, 0f), V(1f, 1f), 6f, Color.Lerp(MRed, MGold, 0.6f), 4, 2f);
        RoundRect(ban, "InnerFill", V(0f, 0f), V(1f, 1f), 7f, MRed, 4, 2f);
        NewRaw(ban, "Glow", V(0.05f, -0.2f), V(0.95f, 1.2f), _glow, new Color(0.85f, 0.15f, 0.2f, 0.45f), new Rect(0f, 0f, 1f, 1f));
        NewPanel(ban, "RuleL", V(0.375f, 0.888f), V(0.465f, 0.892f), MGold);
        NewPanel(ban, "RuleR", V(0.535f, 0.888f), V(0.625f, 0.892f), MGold);
        MText(ban, "Suits", V(0.465f, 0.84f), V(0.535f, 0.94f), "<color=#140a0c>♠</color><color=#e0313d>♥</color><color=#140a0c>♣</color>", Mats.Font, 17, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
        Text title = MText(ban, "Title", V(0.02f, 0.38f), V(0.98f, 0.86f), "POKER TEXAS HOLD'EM\n& BLACKJACK", Serif, 54, FontStyle.Bold, MGoldText, TextAnchor.MiddleCenter);
        title.lineSpacing = 0.92f;
        DropShadow(title, 2.5f, 0.6f);
        Outline o = title.gameObject.AddComponent<Outline>();
        o.effectColor = new Color(0.35f, 0.18f, 0.02f, 0.55f);
        o.effectDistance = new Vector2(1f, -1f);
        NewPanel(ban, "Rule2L", V(0.3f, 0.302f), V(0.488f, 0.306f), new Color(MGold.r, MGold.g, MGold.b, 0.7f));
        NewPanel(ban, "Rule2R", V(0.512f, 0.302f), V(0.7f, 0.306f), new Color(MGold.r, MGold.g, MGold.b, 0.7f));
        MText(ban, "Diamond", V(0.48f, 0.25f), V(0.52f, 0.36f), "◆", Mats.Font, 11, FontStyle.Normal, MGold, TextAnchor.MiddleCenter);
        DropShadow(MText(ban, "Sub", V(0.05f, 0.07f), V(0.95f, 0.27f), "but in Albania", Serif, 22, FontStyle.Italic, MCream, TextAnchor.MiddleCenter), 1.5f, 0.5f);
    }

    void BuildGameCard(Transform m, Vector2 a, Vector2 b, string title, int titleSize, string line, bool poker, UnityAction onClick)
    {
        RectTransform card = NewRect(m, poker ? "Card Poker" : "Card Blackjack", a, b);
        ((RectTransform)RoundRect(card, "Shadow", V(0f, 0f), V(1f, 1f), -5f, new Color(0f, 0f, 0f, 0.4f), 12, 12f).transform).anchoredPosition = new Vector2(3f, -5f);
        Image frame = RoundRect(card, "Frame", V(0f, 0f), V(1f, 1f), 0f, MGold, 10, 9f);
        frame.raycastTarget = true;
        NewRaw(RoundRect(card, "Fill", V(0f, 0f), V(1f, 1f), 1.5f, MGreen, 10, 8f).transform, "Sheen", V(-0.2f, 0.2f), V(0.7f, 1.4f), _glow, new Color(0.25f, 0.55f, 0.38f, 0.16f), new Rect(0f, 0f, 1f, 1f));
        Button btn = frame.gameObject.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;
        btn.onClick.AddListener(onClick);
        btn.onClick.AddListener(delegate { Play(Sfx.Click, 0.6f); });

        Transform art = NewRect(card, "Art", V(0f, 0f), V(0.44f, 1f));
        NewRaw(art, "TableShadow", V(0.08f, 0.1f), V(0.92f, 0.3f), _glow, new Color(0f, 0f, 0f, 0.55f), new Rect(0f, 0f, 1f, 1f));
        if (!poker)
        {
            PlayingCard(art, 0.4f, 0.55f, -11f, "A", CardSuit.Spades);
            PlayingCard(art, 0.62f, 0.57f, 8f, "K", CardSuit.Hearts);
        }
        else
        {
            PlayingCard(art, 0.36f, 0.58f, -10f, "A", CardSuit.Hearts);
            PlayingCard(art, 0.56f, 0.61f, 5f, "A", CardSuit.Clubs);
            ChipStack(art, 0.68f, 0.17f, 7);
        }

        RectTransform words = NewRect(card, "Words", V(0.47f, 0f), V(0.97f, 1f));
        Text t = MText(words, "Title", V(0f, poker ? 0.5f : 0.56f), V(1f, poker ? 0.93f : 0.84f), title, Serif, titleSize, FontStyle.Normal, MGoldText, TextAnchor.LowerLeft);
        t.lineSpacing = 0.88f;
        DropShadow(t, 1.5f, 0.6f);
        MText(words, "Line", V(0f, poker ? 0.28f : 0.3f), V(1f, poker ? 0.48f : 0.54f), line, Mats.Font, 14, FontStyle.Normal, MCream, TextAnchor.UpperLeft).horizontalOverflow = HorizontalWrapMode.Wrap;
        Text play = MText(words, "Play", V(0f, 0.1f), V(1f, 0.26f), "<b>LUAJ  ›</b>", Mats.Font, 13, FontStyle.Normal, MGoldText, TextAnchor.MiddleLeft);

        MenuCardHover hover = frame.gameObject.AddComponent<MenuCardHover>();
        hover.Target = card;
        hover.Frame = frame;
        hover.Play = play;
        hover.FrameNormal = MGold;
        hover.FrameHover = new Color(1f, 0.88f, 0.52f);
        hover.PlayNormal = MGoldText;
        hover.PlayHover = Color.white;
        _menuButtons.Add(btn);
    }

    // ---------- keyboard: Tab / Shift+Tab walks the buttons of the card that is showing ----------

    static bool TabPressed(out bool back)
    {
        back = false;
#if ENABLE_INPUT_SYSTEM
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb == null || !kb.tabKey.wasPressedThisFrame) return false;
        back = kb.shiftKey.isPressed;
        return true;
#elif ENABLE_LEGACY_INPUT_MANAGER
        if (!Input.GetKeyDown(KeyCode.Tab)) return false;
        back = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        return true;
#else
        return false;
#endif
    }

    void TabButtons(List<Button> into)
    {
        into.Clear();
        if (_paused)
        {
            foreach (UiBtn b in new[] { _pauseContinue, _pauseMenu, _pauseQuit })
                if (b != null && b.Go.activeInHierarchy) into.Add(b.B);
        }
        else if (_mode == Mode.Menu)
        {
            if (WheelOpen) into.AddRange(_wheelButtons);
            else if (HelpOpen) into.AddRange(_helpButtons);
            else into.AddRange(_menuButtons);
        }
    }

    void UpdateMenuKeys()
    {
        if (!TabPressed(out bool back) || EventSystem.current == null) return;
        TabButtons(_tabTmp);
        if (_tabTmp.Count == 0) return;
        GameObject cur = EventSystem.current.currentSelectedGameObject;
        int at = -1;
        for (int i = 0; i < _tabTmp.Count; i++)
            if (_tabTmp[i].gameObject == cur) at = i;
        int next = at >= 0
            ? (at + (back ? -1 : 1) + _tabTmp.Count) % _tabTmp.Count
            : (back ? _tabTmp.Count - 1 : 0);
        EventSystem.current.SetSelectedGameObject(_tabTmp[next].gameObject);
        Play(Sfx.Click, 0.3f);
    }

    static void ClearSelection()
    {
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
    }

    // ---------- the little card / chip art on the game cards ----------

    static void PlayingCard(Transform parent, float fx, float fy, float rot, string rank, CardSuit suit)
    {
        Color ink = (suit == CardSuit.Hearts || suit == CardSuit.Diamonds) ? MInkRed : MInk;
        RectTransform c = At(parent, "PlayingCard " + rank, fx, fy, 74f, 102f, rot);
        DropShadow(RoundRect(c, "Edge", V(0f, 0f), V(1f, 1f), 0f, new Color(0.55f, 0.52f, 0.48f), 8, 6f), 3f, 0.45f);
        RoundRect(c, "Face", V(0f, 0f), V(1f, 1f), 1f, new Color(0.98f, 0.97f, 0.93f), 8, 5.5f);
        MText(c, "Rank", V(0.07f, 0.7f), V(0.45f, 0.97f), rank, Serif, 21, FontStyle.Bold, ink, TextAnchor.MiddleCenter);
        NewRaw(c, "SmallSuit", V(0.13f, 0.58f), V(0.37f, 0.72f), SuitTextures.Get(suit), ink, new Rect(0f, 0f, 1f, 1f));
        NewRaw(c, "BigSuit", V(0.24f, 0.18f), V(0.86f, 0.62f), SuitTextures.Get(suit), ink, new Rect(0f, 0f, 1f, 1f));
    }

    static void ChipStack(Transform parent, float fx, float fy, int count)
    {
        float hgt = 20f + count * 5f;
        RectTransform s = At(parent, "Chips", fx, fy, 50f, hgt);
        NewRaw(s, "Shadow", V(-0.2f, -0.25f), V(1.2f, 0.35f), _glow, new Color(0f, 0f, 0f, 0.6f), new Rect(0f, 0f, 1f, 1f));
        float disc = 20f / hgt;
        float step = 5f / hgt;
        for (int i = 0; i < count; i++)
        {
            float y = i * step;
            Color tint = i % 2 == 0 ? new Color(0.62f, 0.05f, 0.1f) : new Color(0.7f, 0.08f, 0.12f);
            NewRaw(s, "Edge" + i, V(0f, y), V(1f, y + disc), _disc, tint, new Rect(0f, 0f, 1f, 1f));
            if (i % 2 == 1)
                NewRaw(s, "Stripe" + i, V(0.2f, y + disc * 0.05f), V(0.8f, y + disc * 0.55f), _disc, new Color(0.95f, 0.92f, 0.86f, 0.9f), new Rect(0f, 0f, 1f, 0.5f));
        }
        float top = (count - 1) * step + step * 0.6f;
        NewRaw(s, "Top", V(0f, top), V(1f, top + disc), _chipTop, Color.white, new Rect(0f, 0f, 1f, 1f));
    }
}

/// <summary>Hover / keyboard-select highlight for the menu cards and round buttons: a slight grow, a brighter frame and label.</summary>
public class MenuCardHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    public Transform Target;
    public Graphic Frame;
    public Text Play;
    public Color FrameNormal, FrameHover, PlayNormal, PlayHover;
    public Vector2 Push;

    bool _over, _selected, _baseSet;
    float _k;
    Vector2 _base;

    public void OnPointerEnter(PointerEventData e) { _over = true; }
    public void OnPointerExit(PointerEventData e) { _over = false; }
    public void OnSelect(BaseEventData e) { _selected = true; }
    public void OnDeselect(BaseEventData e) { _selected = false; }

    void OnDisable()
    {
        _over = false;
        _selected = false;
        _k = 0f;
        Apply();
    }

    void Update()
    {
        float goal = (_over || _selected) ? 1f : 0f;
        if (Mathf.Approximately(_k, goal)) return;
        _k = Mathf.MoveTowards(_k, goal, Time.unscaledDeltaTime * 7f);
        Apply();
    }

    void Apply()
    {
        Transform t = Target != null ? Target : transform;
        t.localScale = Vector3.one * (1f + 0.035f * _k);
        if (t is RectTransform rt && Push != Vector2.zero)
        {
            if (!_baseSet) { _base = rt.anchoredPosition; _baseSet = true; }
            rt.anchoredPosition = _base + Push * _k;
        }
        if (Frame != null) Frame.color = Color.Lerp(FrameNormal, FrameHover, _k);
        if (Play != null) Play.color = Color.Lerp(PlayNormal, PlayHover, _k);
    }
}
