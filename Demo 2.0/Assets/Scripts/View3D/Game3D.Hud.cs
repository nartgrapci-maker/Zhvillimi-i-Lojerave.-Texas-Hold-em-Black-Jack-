using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using PokerDemo.Core;

/// <summary>Receives drags on the empty part of the screen.</summary>
public class LookPad : MonoBehaviour, UnityEngine.EventSystems.IDragHandler
{
    public System.Action<Vector2> OnDelta;
    public void OnDrag(UnityEngine.EventSystems.PointerEventData e) { if (OnDelta != null) OnDelta(e.delta); }
}

public partial class Game3D
{
    class UiBtn
    {
        public GameObject Go;
        public Button B;
        public Text T;
    }

    GameObject _hudRoot;
    Text _infoT, _logT, _tipsT, _bannerT, _amountT, _bfAmountT, _eqT, _cityT;
    GameObject _menuPanel, _infoPanel, _logPanel, _tipsPanel, _bannerPanel, _eqPanel, _cityPanel;
    GameObject _topBar, _barPlay, _betPanel, _barOver, _barBj, _barBf, _gBfBet, _gBfPlay;
    RectTransform _eqFill;
    UiBtn _topMenu, _topMusic, _topSpeed, _topView, _topMap, _topFov, _topCenter;
    Text _lookHint;
    UiBtn _bTea, _bCig, _bLog;
    bool _logOpen;
    UiBtn _bFold, _bCC, _bBet, _bHint, _bMinus, _bPlus, _bMin, _bHalf, _bPotQ, _bAll;
    UiBtn _bOverA, _bOverB, _bOverC, _bCityGo;
    UiBtn _bBjHit, _bBjStand, _bBjHint, _bBjNext;
    UiBtn _bBfMinus, _bBfPlus, _bBfDeal, _bBfHit, _bBfStand, _bBfDouble, _bBfHint, _bBfRestart;

    static Vector2 V(float x, float y) { return new Vector2(x, y); }

    // ---- small UI kit ---------------------------------------------------------------------

    static RectTransform NewRect(Transform parent, string name, Vector2 aMin, Vector2 aMax)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = aMin;
        rt.anchorMax = aMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return rt;
    }

    static Image NewPanel(Transform parent, string name, Vector2 aMin, Vector2 aMax, Color color)
    {
        RectTransform rt = NewRect(parent, name, aMin, aMax);
        Image img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    static RawImage NewRaw(Transform parent, string name, Vector2 aMin, Vector2 aMax, Texture tex, Color tint, Rect uv)
    {
        RectTransform rt = NewRect(parent, name, aMin, aMax);
        RawImage img = rt.gameObject.AddComponent<RawImage>();
        img.texture = tex;
        img.color = tint;
        img.uvRect = uv;
        img.raycastTarget = false;
        return img;
    }

    static Text NewLabel(Transform parent, string name, Vector2 aMin, Vector2 aMax, int size, TextAnchor anchor)
    {
        RectTransform rt = NewRect(parent, name, aMin, aMax);
        rt.offsetMin = new Vector2(8, 6);
        rt.offsetMax = new Vector2(-8, -6);
        Text t = rt.gameObject.AddComponent<Text>();
        t.font = Mats.Font;
        t.fontSize = size;
        t.alignment = anchor;
        t.color = Color.white;
        t.supportRichText = true;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Truncate;
        t.raycastTarget = false;
        return t;
    }

    UiBtn NewButton(Transform parent, string label, Vector2 aMin, Vector2 aMax, UnityAction onClick, Color color, int size)
    {
        Image img = NewPanel(parent, "Button " + label, aMin, aMax, color);
        img.raycastTarget = true;
        Button b = img.gameObject.AddComponent<Button>();
        b.targetGraphic = img;
        b.onClick.AddListener(onClick);
        b.onClick.AddListener(delegate { Play(Sfx.Click, 0.6f); });
        Text t = NewLabel(img.transform, "Label", V(0, 0), V(1, 1), size, TextAnchor.MiddleCenter);
        t.text = label;
        return new UiBtn { Go = img.gameObject, B = b, T = t };
    }

    static GameObject Group(Transform parent, string name)
    {
        return NewRect(parent, name, V(0, 0), V(1, 1)).gameObject;
    }

    // ---- HUD layout -----------------------------------------------------------------------

    void BuildHud()
    {
        _hudRoot = new GameObject("Game3D HUD");
        var canvas = _hudRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;
        var scaler = _hudRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.matchWidthOrHeight = 0.5f;
        _hudRoot.AddComponent<GraphicRaycaster>();
        Transform h = _hudRoot.transform;

        // an invisible layer behind every button: dragging on it turns your head
        RectTransform padRt = NewRect(h, "LookPad", V(0, 0), V(1, 1));
        Image padImg = padRt.gameObject.AddComponent<Image>();
        padImg.color = new Color(0f, 0f, 0f, 0f);
        padImg.raycastTarget = true;
        padRt.gameObject.AddComponent<LookPad>().OnDelta = delegate (Vector2 d) { if (FirstPersonNow) LookDrag(d); };

        Color glass = new Color(0.04f, 0.01f, 0.03f, 0.58f);
        Color blue = new Color(0.17f, 0.28f, 0.45f);
        Color green = new Color(0.12f, 0.42f, 0.28f);
        Color red = new Color(0.62f, 0.10f, 0.17f);
        Color dark = new Color(0.22f, 0.20f, 0.24f);
        Color gold = new Color(0.50f, 0.38f, 0.07f);
        Color violet = new Color(0.36f, 0.22f, 0.52f);

        // panels
        _infoPanel = NewPanel(h, "Info", V(0.005f, 0.56f), V(0.235f, 0.995f), glass).gameObject;
        _infoT = NewLabel(_infoPanel.transform, "Text", V(0, 0), V(1, 1), 13, TextAnchor.UpperLeft);
        _logPanel = NewPanel(h, "Log", V(0.005f, 0.085f), V(0.215f, 0.40f), glass).gameObject;
        _logT = NewLabel(_logPanel.transform, "Text", V(0, 0), V(1, 1), 12, TextAnchor.LowerLeft);
        _tipsPanel = NewPanel(h, "Tips", V(0.775f, 0.43f), V(0.995f, 0.995f), new Color(0.24f, 0.17f, 0.03f, 0.66f)).gameObject;
        _tipsT = NewLabel(_tipsPanel.transform, "Text", V(0, 0), V(1, 1), 13, TextAnchor.UpperLeft);
        _eqPanel = NewPanel(h, "Equity", V(0.775f, 0.335f), V(0.995f, 0.42f), new Color(0.04f, 0.01f, 0.03f, 0.66f)).gameObject;
        _eqT = NewLabel(_eqPanel.transform, "Text", V(0, 0.45f), V(1, 1), 13, TextAnchor.MiddleLeft);
        NewPanel(_eqPanel.transform, "BarBack", V(0.05f, 0.12f), V(0.95f, 0.40f), new Color(1f, 1f, 1f, 0.18f));
        Image fill = NewPanel(_eqPanel.transform, "BarFill", V(0.05f, 0.12f), V(0.5f, 0.40f), new Color(0.95f, 0.76f, 0.22f, 0.95f));
        _eqFill = (RectTransform)fill.transform;
        _bannerPanel = NewPanel(h, "Banner", V(0.24f, 0.135f), V(0.76f, 0.27f), new Color(0.03f, 0f, 0.02f, 0.70f)).gameObject;
        _bannerT = NewLabel(_bannerPanel.transform, "Text", V(0, 0), V(1, 1), 15, TextAnchor.MiddleCenter);

        // top row
        _topBar = Group(h, "TopBar");
        _topMenu = NewButton(_topBar.transform, "Menu", V(0.245f, 0.935f), V(0.295f, 0.99f), delegate { SetPaused(true); }, red, 14);
        _topMusic = NewButton(_topBar.transform, "Muzika", V(0.300f, 0.935f), V(0.375f, 0.99f), delegate { ToggleMusic(); }, dark, 12);
        _topSpeed = NewButton(_topBar.transform, "Shpejt", V(0.380f, 0.935f), V(0.450f, 0.99f), delegate { ToggleSpeed(); }, dark, 12);
        _topView = NewButton(_topBar.transform, "Kamera", V(0.455f, 0.935f), V(0.545f, 0.99f), delegate { ToggleView(); }, dark, 12);
        _topFov = NewButton(_topBar.transform, "Pamja", V(0.550f, 0.935f), V(0.620f, 0.99f), delegate { CycleFov(); }, dark, 12);
        _topCenter = NewButton(_topBar.transform, "Qendro", V(0.625f, 0.935f), V(0.690f, 0.99f), delegate { CenterLook(); }, dark, 12);
        _topMap = NewButton(_topBar.transform, "Harta", V(0.695f, 0.935f), V(0.775f, 0.99f), delegate { StepMap(1); }, dark, 12);
        _lookHint = NewLabel(h, "LookHint", V(0.245f, 0.885f), V(0.775f, 0.932f), 13, TextAnchor.MiddleCenter);
        _lookHint.text = "Tërhiq me miun për të parë përreth";
        _lookHint.color = new Color(1f, 0.92f, 0.65f);

        // small buttons at the bottom left: tea, cigarette, the log (closed until you ask for it)
        _bTea = NewButton(_topBar.transform, "Çaj rusi", V(0.005f, 0.02f), V(0.078f, 0.075f), delegate { OnTea(); }, new Color(0.45f, 0.27f, 0.08f), 12);
        _bCig = NewButton(_topBar.transform, "Cigare", V(0.083f, 0.02f), V(0.156f, 0.075f), delegate { OnCig(); }, new Color(0.30f, 0.30f, 0.34f), 12);
        _bLog = NewButton(_topBar.transform, "Ditari", V(0.161f, 0.02f), V(0.215f, 0.075f), delegate { _logOpen = !_logOpen; RefreshHud(); }, dark, 12);

        // hold'em action bar
        _barPlay = Group(h, "PlayBar");
        _bFold = NewButton(_barPlay.transform, "Hiq dorë", V(0.225f, 0.02f), V(0.345f, 0.12f), delegate { OnFold(); }, red, 18);
        _bCC = NewButton(_barPlay.transform, "Kalo", V(0.355f, 0.02f), V(0.495f, 0.12f), delegate { OnCheckCall(); }, blue, 18);
        _bBet = NewButton(_barPlay.transform, "Vë", V(0.505f, 0.02f), V(0.685f, 0.12f), delegate { OnBetRaise(); }, green, 18);
        _bHint = NewButton(_barPlay.transform, "Ndihmë", V(0.695f, 0.02f), V(0.785f, 0.12f), delegate { OnHint(); }, gold, 16);

        _betPanel = NewPanel(h, "BetPanel", V(0.80f, 0.02f), V(0.995f, 0.30f), glass).gameObject;
        Transform bp = _betPanel.transform;
        NewLabel(bp, "Title", V(0f, 0.80f), V(1f, 1f), 13, TextAnchor.MiddleCenter).text = "Vë ose ngre në";
        _bMinus = NewButton(bp, "-", V(0.04f, 0.52f), V(0.26f, 0.78f), delegate { OnAmount(-1); }, blue, 20);
        _amountT = NewLabel(bp, "Amount", V(0.28f, 0.52f), V(0.72f, 0.78f), 20, TextAnchor.MiddleCenter);
        _bPlus = NewButton(bp, "+", V(0.74f, 0.52f), V(0.96f, 0.78f), delegate { OnAmount(1); }, blue, 20);
        _bMin = NewButton(bp, "Min", V(0.04f, 0.27f), V(0.49f, 0.49f), delegate { _betAmount = 0; RefreshHud(); }, blue, 14);
        _bHalf = NewButton(bp, "½ poti", V(0.51f, 0.27f), V(0.96f, 0.49f), delegate { SetBetFraction(0.5f); }, blue, 14);
        _bPotQ = NewButton(bp, "Poti", V(0.04f, 0.03f), V(0.49f, 0.25f), delegate { SetBetFraction(1f); }, blue, 14);
        _bAll = NewButton(bp, "Gjithçka", V(0.51f, 0.03f), V(0.96f, 0.25f), delegate { OnAllIn(); }, red, 14);

        // end of hand bar
        _barOver = Group(h, "OverBar");
        _bOverA = NewButton(_barOver.transform, "Dora tjetër", V(0.235f, 0.02f), V(0.425f, 0.12f), delegate { OnOverA(); }, green, 17);
        _bOverB = NewButton(_barOver.transform, "Niveli tjetër", V(0.435f, 0.02f), V(0.635f, 0.12f), delegate { OnOverB(); }, blue, 17);
        _bOverC = NewButton(_barOver.transform, "Menu", V(0.645f, 0.02f), V(0.775f, 0.12f), delegate { ShowMenu(); }, red, 17);

        // scripted blackjack bar
        _barBj = Group(h, "BjBar");
        _bBjHit = NewButton(_barBj.transform, "Merr letër", V(0.27f, 0.02f), V(0.40f, 0.12f), delegate { OnBjHit(); }, blue, 18);
        _bBjStand = NewButton(_barBj.transform, "Qëndro", V(0.41f, 0.02f), V(0.54f, 0.12f), delegate { OnBjStand(); }, blue, 18);
        _bBjHint = NewButton(_barBj.transform, "Ndihmë", V(0.55f, 0.02f), V(0.64f, 0.12f), delegate { _bjHint = true; RefreshHud(); }, gold, 16);
        _bBjNext = NewButton(_barBj.transform, "Dora tjetër", V(0.65f, 0.02f), V(0.80f, 0.12f), delegate { OnBjNext(); }, green, 17);

        // free blackjack: choose the bet, then play the round
        _barBf = Group(h, "BfBar");
        _gBfBet = Group(_barBf.transform, "Bet");
        _bBfMinus = NewButton(_gBfBet.transform, "-", V(0.29f, 0.02f), V(0.345f, 0.12f), delegate { OnBfBet(-1); }, blue, 22);
        NewPanel(_gBfBet.transform, "AmountBack", V(0.355f, 0.025f), V(0.475f, 0.115f), glass);
        _bfAmountT = NewLabel(_gBfBet.transform, "Amount", V(0.355f, 0.02f), V(0.475f, 0.12f), 20, TextAnchor.MiddleCenter);
        _bBfPlus = NewButton(_gBfBet.transform, "+", V(0.485f, 0.02f), V(0.54f, 0.12f), delegate { OnBfBet(1); }, blue, 22);
        _bBfDeal = NewButton(_gBfBet.transform, "Ndaj letrat", V(0.55f, 0.02f), V(0.74f, 0.12f), delegate { OnBfDeal(); }, green, 18);
        _gBfPlay = Group(_barBf.transform, "Play");
        _bBfHit = NewButton(_gBfPlay.transform, "Merr letër", V(0.24f, 0.02f), V(0.37f, 0.12f), delegate { OnBfHit(); }, blue, 17);
        _bBfStand = NewButton(_gBfPlay.transform, "Qëndro", V(0.38f, 0.02f), V(0.50f, 0.12f), delegate { OnBfStand(); }, blue, 17);
        _bBfDouble = NewButton(_gBfPlay.transform, "Dyfisho", V(0.51f, 0.02f), V(0.64f, 0.12f), delegate { OnBfDouble(); }, violet, 17);
        _bBfHint = NewButton(_gBfPlay.transform, "Ndihmë", V(0.65f, 0.02f), V(0.74f, 0.12f), delegate { _bfHint = true; RefreshHud(); }, gold, 16);
        _bBfRestart = NewButton(_barBf.transform, "Fillo përsëri", V(0.36f, 0.02f), V(0.64f, 0.12f), delegate { OnBfRestart(); }, red, 18);

        // the card that introduces a city of the tournament
        _cityPanel = NewPanel(h, "CityCard", V(0.30f, 0.12f), V(0.70f, 0.88f), new Color(0.05f, 0.01f, 0.03f, 0.88f)).gameObject;
        Transform cp = _cityPanel.transform;
        NewRaw(cp, "Strip", V(0.03f, 0.89f), V(0.97f, 0.96f), Theme.Strip, Color.white, new Rect(0f, 0f, 0.42f, 1f));
        NewRaw(cp, "Eagle", V(0.41f, 0.68f), V(0.59f, 0.87f), Theme.Eagle, Theme.Gold, new Rect(0f, 0f, 1f, 1f));
        _cityT = NewLabel(cp, "Text", V(0.04f, 0.21f), V(0.96f, 0.66f), 16, TextAnchor.MiddleCenter);
        _bCityGo = NewButton(cp, "Fillo", V(0.25f, 0.04f), V(0.75f, 0.17f), delegate { OnCityStart(); }, red, 22);

        // the main menu (the only menu): a title banner and two games, over the 3D room
        _menuPanel = BuildMainMenu(h);
        BuildCardPanels(h);
        BuildPausePanel(h);
    }

    static void Gold(Transform parent, Vector2 a, Vector2 b) { NewPanel(parent, "Frame", a, b, new Color(0.85f, 0.65f, 0.20f, 0.9f)); }

    /// <summary>Chooses another map (menu: the room behind the menu changes; in a game: the backdrop changes right away).</summary>
    void StepMap(int direction)
    {
        _map = ((_map + 1 + direction) % Theme.MapCount + Theme.MapCount) % Theme.MapCount - 1;
        if (_mode != Mode.Tournament) SetCity(_map);
        Play(Sfx.Card, 0.6f);
        RefreshHud();
    }

    // =====================================================================================
    // screen state
    // =====================================================================================

    void ShowMenu()
    {
        SetPaused(false);
        CloseOverlay();
        StopAllCoroutines();
        _mode = Mode.Menu;
        ClearDynamic();
        SetCity(_map);
        _log.Clear();
        _banner = "";
        _fp = null;
        _level = null;
        _cardUp = false;
        RefreshHud();
    }

    void AddLog(string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        string[] lines = text.Split('\n');
        for (int i = 0; i < lines.Length; i++) _log.Add(lines[i]);
        while (_log.Count > 80) _log.RemoveAt(0);
    }

    void RefreshHud()
    {
        if (_menuPanel == null) return;
        bool menu = _mode == Mode.Menu;
        bool holdem = _mode == Mode.Level || _mode == Mode.FreePlay || _mode == Mode.Tournament;
        bool bjs = _mode == Mode.Blackjack;
        bool bjf = _mode == Mode.BjFree;
        bool card = holdem && _cardUp;
        bool inHand = holdem && _hand != null && !card;

        // the main menu shows nothing but its banner and its two games
        _topMusic.Go.SetActive(!menu);
        _topSpeed.Go.SetActive(!menu);
        _topMusic.T.text = _musicMode == 0 ? "Muzika: Himni" : (_musicMode == 1 ? "Muzika: folk" : "Muzika: jo");
        _topSpeed.T.text = "Shpejt";
        _topSpeed.Go.GetComponent<Image>().color = _spd < 0.99f ? new Color(0.50f, 0.38f, 0.07f) : new Color(0.22f, 0.20f, 0.24f);
        _topView.T.text = _fpv ? "Kamera" : "Nga lart";
        _topMap.T.text = "Harta";
        _topFov.T.text = "Pamja: " + (int)FovChoices[_fovIdx] + "°";
        _topView.Go.SetActive(!menu);
        _topFov.Go.SetActive(!menu && _fpv);
        _topCenter.Go.SetActive(!menu && _fpv);
        _lookHint.gameObject.SetActive(!menu && _fpv && !_lookUsed);
        _topMap.Go.SetActive(!menu && _mode != Mode.Tournament);

        RefreshHabitButtons();
        _menuPanel.SetActive(menu);
        _topMenu.Go.SetActive(!menu);
        _infoPanel.SetActive(!menu && !card);
        _logPanel.SetActive(!menu && !card && _logOpen);
        _tipsPanel.SetActive(!menu && !card);
        _bLog.Go.SetActive(!menu && !card);
        _bTea.Go.SetActive(!menu && !card);
        _bCig.Go.SetActive(!menu && !card);
        _bannerPanel.SetActive(!menu && !card && _banner.Length > 0);
        _cityPanel.SetActive(card);
        _eqPanel.SetActive(false);
        _barPlay.SetActive(inHand && !_handOver);
        _betPanel.SetActive(inHand && !_handOver);
        _barOver.SetActive(inHand && _handOver);
        _barBj.SetActive(bjs);
        _barBf.SetActive(bjf);
        RefreshCardPanels(menu, holdem, card);
        if (menu) return;
        if (card) { _cityT.text = CityCardText(); return; }

        _bannerT.text = _banner;
        int first = Mathf.Max(0, _log.Count - 9);
        var sb = new StringBuilder();
        for (int i = first; i < _log.Count; i++)
        {
            if (sb.Length > 0) sb.Append("\n");
            sb.Append(_log[i]);
        }
        _logT.text = sb.ToString();

        if (holdem) RefreshHoldemHud();
        else if (bjs) RefreshBjHud();
        else RefreshBfHud();
        FitPanels();
    }

    // ---- panels only as big as their text, and only there when they have something to say ----

    static string StripTags(string s)
    {
        var sb = new StringBuilder();
        bool inTag = false;
        foreach (char c in s)
        {
            if (c == '<') inTag = true;
            else if (c == '>' && inTag) inTag = false;
            else if (!inTag) sb.Append(c);
        }
        return sb.ToString();
    }

    static void FitPanel(GameObject panel, string text, float widthFrac, float maxHeight)
    {
        var rt = (RectTransform)panel.transform;
        float charsPerLine = widthFrac * 1280f / 7.4f;
        int lines = 0;
        foreach (string raw in StripTags(text).Split('\n')) lines += Mathf.Max(1, Mathf.CeilToInt(raw.Length / charsPerLine));
        float h = Mathf.Min(maxHeight, 0.027f * lines + 0.04f);
        rt.anchorMin = V(rt.anchorMin.x, rt.anchorMax.y - h);
    }

    void FitPanels()
    {
        FitPanel(_infoPanel, _infoT.text, 0.23f, 0.50f);
        bool tips = _tipsT.text.Trim().Length > 0;
        _tipsPanel.SetActive(tips);
        if (tips)
        {
            FitPanel(_tipsPanel, _tipsT.text, 0.22f, 0.565f);
            // the win-chance meter hangs under the tips
            var tr = (RectTransform)_tipsPanel.transform;
            var er = (RectTransform)_eqPanel.transform;
            er.anchorMax = V(er.anchorMax.x, tr.anchorMin.y - 0.01f);
            er.anchorMin = V(er.anchorMin.x, tr.anchorMin.y - 0.095f);
        }
    }

    // =====================================================================================
    // hold'em HUD
    // =====================================================================================

    void RefreshHoldemHud()
    {
        if (_hand == null)
        {
            if (_configError != null)
            {
                _infoT.text = "<b>Gabim në konfigurimin e nivelit</b>\n" + _configError +
                              "\n\nKy është një defekt i ngritjes së nivelit, jo gabim i lojtarit. Kthehu te menuja dhe ngarkoje nivelin përsëri.";
                _tipsT.text = "";
            }
            return;
        }

        _infoT.text = BuildInfoText();
        bool myTurn = !_hand.IsComplete && !_handOver && !_botsActing && _hand.Seats[_hand.Actor].IsHuman;
        _tipsT.text = BuildTips(myTurn);

        if (!_handOver)
        {
            Seat me = _hand.Seats[0];
            int maxTotal = me.StreetBet + me.Stack;
            int minTotal = _hand.CurrentBet == 0 ? _hand.BigBlind : _hand.CurrentBet + _hand.MinRaise;
            minTotal = Mathf.Min(minTotal, maxTotal);
            bool canRaise = maxTotal > _hand.CurrentBet;
            if (_betAmount < minTotal) _betAmount = minTotal;
            if (_betAmount > maxTotal) _betAmount = maxTotal;

            int toCall = _hand.ToCall(0);
            _bCC.T.text = toCall > 0 ? "Barazo " + toCall : "Kalo";
            _bBet.T.text = (_hand.CurrentBet > 0 ? "Ngre në " : "Vë ") + _betAmount + (_betAmount == maxTotal ? " (all-in)" : "");
            _amountT.text = _betAmount.ToString();

            _bFold.B.interactable = myTurn;
            _bCC.B.interactable = myTurn;
            _bBet.B.interactable = myTurn && canRaise;
            _bMinus.B.interactable = myTurn && canRaise;
            _bPlus.B.interactable = myTurn && canRaise;
            _bMin.B.interactable = myTurn && canRaise;
            _bHalf.B.interactable = myTurn && canRaise;
            _bPotQ.B.interactable = myTurn && canRaise;
            _bAll.B.interactable = myTurn;
            _bHint.B.interactable = true;

            // the win-chance meter appears with the hint
            if (myTurn && _hintOn && !me.Folded)
            {
                double eq = CurrentEquity();
                _eqPanel.SetActive(true);
                _eqT.text = "Gjasat për të fituar: <b>" + Mathf.RoundToInt((float)eq * 100f) + "%</b>";
                _eqFill.anchorMax = V(0.05f + 0.90f * Mathf.Clamp01((float)eq), 0.40f);
            }
        }
        else SetOverButtons();
    }

    void SetOverButtons()
    {
        _bOverB.Go.SetActive(false);
        if (_mode == Mode.Level)
        {
            bool passed = _level.IsPassed(_hand.Seats[0].Stack);
            bool hasNext = _levelIndex + 1 < Levels.All.Length;
            _bOverA.T.text = "Provo dorën përsëri";
            _bOverB.Go.SetActive(passed && hasNext);
            _bOverB.T.text = "Niveli tjetër";
        }
        else if (_mode == Mode.Tournament)
        {
            if (!_fp.IsOver) _bOverA.T.text = "Dora tjetër";
            else if (_fp.HumanIsOut) _bOverA.T.text = "Provo përsëri këtë qytet";
            else if (_stage >= Tournament.Stages.Length - 1) _bOverA.T.text = "Luaj turneun përsëri";
            else
            {
                _bOverA.T.text = "Luaj përsëri këtë qytet";
                _bOverB.Go.SetActive(true);
                _bOverB.T.text = "Qyteti tjetër: " + Sq.Cities[_stage + 1].Name;
            }
        }
        else _bOverA.T.text = _fp.IsOver ? "Lojë e re" : "Dora tjetër";
    }

    string CityCardText()
    {
        Sq.City c = Sq.Cities[Mathf.Clamp(_stage, 0, Sq.Cities.Length - 1)];
        StageDef d = Tournament.Stages[Mathf.Clamp(_stage, 0, Tournament.Stages.Length - 1)];
        var sb = new StringBuilder();
        sb.Append("<size=24><b>" + c.Title + "</b></size>\n");
        sb.Append("<i>" + c.Tagline + "</i>\n\n");
        sb.Append("Niveli " + (_stage + 1) + " nga " + Tournament.Stages.Length + "\n");
        sb.Append("<b>Kundërshtarët:</b>\n");
        for (int i = 0; i < d.BotNames.Length; i++)
            sb.Append(d.BotNames[i] + ", " + Sq.Style(d.Bots[i].Label) + "\n");
        sb.Append("\nQëllimi: fitoji të tre. Ti fillon me " + FreePlaySession.StartStack + " " + Sq.Currency +
                  (d.BotStack != FreePlaySession.StartStack ? ", ata me " + d.BotStack + "." : "."));
        return sb.ToString();
    }

    string BuildInfoText()
    {
        var sb = new StringBuilder();
        if (_mode == Mode.Level)
        {
            Sq.LevelText t = Sq.Level(_level.Number);
            sb.Append("<b>Niveli " + _level.Number + ": " + t.Title + "</b>\n");
            sb.Append("<b>Qëllimi:</b> " + t.Objective + "\n");
            sb.Append("<b>Për ta kaluar:</b> " + t.Pass);
        }
        else
        {
            sb.Append("<b>" + (_mode == Mode.Tournament ? Sq.Cities[_stage].Title : "Loja e lirë") + "</b>\n");
            sb.Append("Dora " + _fp.HandNumber + "  ·  bllindat " + _fp.SmallBlind + "/" + _fp.BigBlind);
        }
        return sb.ToString();
    }

    string BuildTips(bool myTurn)
    {
        var sb = new StringBuilder();
        Seat me = _hand.Seats[0];
        if (!me.Folded)
        {
            string now = Sq.HandNow(_hand, 0);
            if (now != null) sb.Append("Ke: " + now + "\n");
        }
        if (myTurn)
        {
            int toCall = _hand.ToCall(0);
            int need = toCall > 0 ? Mathf.RoundToInt(100f * toCall / (_hand.Pot + toCall)) : 0;
            if (toCall > 0)
                sb.Append("Barazo: " + toCall + " " + Sq.Currency + "  ·  duhen ~" + need + "% gjasa\n");
            if (_mode == Mode.Level)
            {
                if (_level.ShowFirstActionPrompt && _hand.Street == Phase.Preflop && toCall > 0)
                    sb.Append(Sq.FirstActionPrompt(toCall) + "\n");
                if (_level.ShowRiverHint)
                {
                    string rh = Sq.RiverHint(_hand);
                    if (rh != null) sb.Append(rh + "\n");
                }
                if (_level.ShowOutsHelper)
                {
                    string oh = Sq.OutsHelper(_hand, 0);
                    if (oh != null) sb.Append(oh + "\n");
                }
                if (_hintOn) sb.Append("<color=#ffe066>" + Sq.Level(_level.Number).Hint + "</color>\n");
            }
            else if (_hintOn)
            {
                int pct = Mathf.RoundToInt((float)SmartBot.Strength(_hand, 0) * 100f);
                sb.Append("<color=#ffe066>Fuqia e përafërt e dorës sate: " + pct + " nga 100.</color>\n");
                string oh = Sq.OutsHelper(_hand, 0);
                if (oh != null) sb.Append(oh + "\n");
            }
            if (_hintOn && !me.Folded)
            {
                int eq = Mathf.RoundToInt((float)CurrentEquity() * 100f);
                if (toCall > 0)
                    sb.Append("<color=#ffe066>" + (eq >= need
                        ? "Gjasat (" + eq + "%) janë mbi çmimin e barazimit (" + need + "%): barazimi ia vlen."
                        : "Gjasat (" + eq + "%) janë nën çmimin e barazimit (" + need + "%): zakonisht hiq dorë, përveç nëse mendon se mund ta bësh kundërshtarin të heqë dorë.") + "</color>\n");
            }
        }
        return sb.ToString();
    }

    /// <summary>Monte Carlo win chance of the human, cached until the board or the number of opponents changes.</summary>
    double CurrentEquity()
    {
        int opp = 0;
        for (int i = 1; i < _hand.Seats.Count; i++) if (!_hand.Seats[i].Folded) opp++;
        int key = _hand.BoardCount * 100 + opp;
        if (key != _eqKey)
        {
            _eq = Equity.WinChance(_hand, 0, 400, _eqRng);
            _eqKey = key;
        }
        return _eq;
    }

    // =====================================================================================
    // button handlers
    // =====================================================================================

    int BetStep() { return Mathf.Max(5, _hand.BigBlind / 2); }

    void OnHint()
    {
        if (!_hintOn && _mode != Mode.Level) _stats.Hints++;
        _hintOn = true;
        RefreshHud();
    }

    void OnAmount(int direction)
    {
        if (_hand == null) return;
        _betAmount += direction * BetStep();
        RefreshHud();
    }

    void SetBetFraction(float fraction)
    {
        if (_hand == null) return;
        int increase = Mathf.RoundToInt((_hand.Pot + _hand.ToCall(0)) * fraction);
        int total = _hand.CurrentBet + increase;
        _betAmount = Mathf.RoundToInt(total / 5f) * 5;
        RefreshHud();
    }

    void OnFold() { Act(PlayerAction.Fold()); }

    void OnCheckCall()
    {
        if (_hand == null || _hand.IsComplete) return;
        Act(_hand.ToCall(0) > 0 ? PlayerAction.Call() : PlayerAction.Check());
    }

    void OnBetRaise() { Act(PlayerAction.BetTo(_betAmount)); }

    void OnAllIn()
    {
        if (_hand == null || _hand.IsComplete) return;
        Seat me = _hand.Seats[0];
        int total = me.StreetBet + me.Stack;
        Act(total <= _hand.CurrentBet ? PlayerAction.Call() : PlayerAction.BetTo(total));
    }

    void OnOverA()
    {
        if (_mode == Mode.Level) StartLevel(_levelIndex);
        else if (_mode == Mode.Tournament)
        {
            if (!_fp.IsOver) NextFreePlayHand();
            else if (!_fp.HumanIsOut && _stage >= Tournament.Stages.Length - 1) StartTournament();
            else StartStage(_stage);
        }
        else if (_fp != null && _fp.IsOver) StartFreePlay();
        else NextFreePlayHand();
    }

    void OnOverB()
    {
        if (_mode == Mode.Level) StartLevel(_levelIndex + 1);
        else if (_mode == Mode.Tournament && _fp != null && _fp.IsOver && !_fp.HumanIsOut) StartStage(_stage + 1);
    }

    void OnCityStart()
    {
        if (!_cardUp) return;
        _cardUp = false;
        AddLog("<b>" + Sq.Cities[_stage].Title + "</b>");
        NextFreePlayHand();
    }
}
