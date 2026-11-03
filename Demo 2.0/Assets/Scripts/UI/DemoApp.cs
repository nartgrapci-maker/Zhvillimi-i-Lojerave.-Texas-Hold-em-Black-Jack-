using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using PokerDemo.Core;

/// <summary>
/// Self-building UI for the PBT 4 prototype. It creates its own Canvas and EventSystem at runtime,
/// so you can press Play in any scene (even an empty one). The game rules live in PokerDemo.Core;
/// this class only draws state and forwards button presses.
/// </summary>
public class DemoApp : MonoBehaviour
{
    static DemoApp _instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (_instance != null) return;
        var go = new GameObject("PokerDemo");
        _instance = go.AddComponent<DemoApp>();
    }

    class UiButton
    {
        public Button Button;
        public Text Label;
    }

    // ---- shared
    Font _font;
    Canvas _canvas;
    GameObject _menuRoot, _gameRoot, _bjRoot;

    // ---- Hold'em screen
    Text _title, _info, _board, _seatsText, _feedText, _tipsText;
    GameObject _actionsRoot, _endRoot;
    UiButton _foldBtn, _ccBtn, _bet5, _bet10, _bet20, _betHalf, _betPot, _allIn, _hintBtn, _retryBtn, _nextBtn;
    LevelDef _level;
    int _levelIndex;
    HoldemHand _hand;
    int _eventsShown;
    bool _botsActing;
    bool _hintOn;
    string _configError;
    readonly List<string> _feedLines = new List<string>();

    // ---- Blackjack screen
    Text _bjTitle, _bjTable, _bjMessage;
    UiButton _bjHit, _bjStand, _bjHint, _bjNext;
    BlackjackSession _bj;
    bool _bjHintOn;
    string _bjError;

    // =====================================================================================
    // setup
    // =====================================================================================

    void Awake()
    {
        _font = LoadFont();
        BuildEventSystem();
        BuildCanvas();
        BuildMenu();
        BuildGame();
        BuildBlackjack();
        ShowMenu();
    }

    static Font LoadFont()
    {
        Font f = null;
#if UNITY_2022_2_OR_NEWER
        f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#else
        f = Resources.GetBuiltinResource<Font>("Arial.ttf");
#endif
        if (f == null) f = Font.CreateDynamicFontFromOSFont("Arial", 16);
        return f;
    }

    void BuildEventSystem()
    {
        if (EventSystem.current != null) return;
        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        System.Type t = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
        if (t != null) go.AddComponent(t);
#else
        go.AddComponent<StandaloneInputModule>();
#endif
    }

    void BuildCanvas()
    {
        var go = new GameObject("Canvas");
        go.transform.SetParent(transform, false);
        _canvas = go.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();
        Image bg = Panel(go.transform, "Background", V(0, 0), V(1, 1), new Color(0.04f, 0.22f, 0.15f));
        bg.raycastTarget = false;
    }

    // ---- small UI kit -------------------------------------------------------------------

    static Vector2 V(float x, float y) { return new Vector2(x, y); }

    static RectTransform Rect(Transform parent, string name, Vector2 aMin, Vector2 aMax)
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

    static Image Panel(Transform parent, string name, Vector2 aMin, Vector2 aMax, Color color)
    {
        RectTransform rt = Rect(parent, name, aMin, aMax);
        Image img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        return img;
    }

    Text Label(Transform parent, string name, Vector2 aMin, Vector2 aMax, int size, TextAnchor anchor)
    {
        RectTransform rt = Rect(parent, name, aMin, aMax);
        rt.offsetMin = new Vector2(8, 6);
        rt.offsetMax = new Vector2(-8, -6);
        Text t = rt.gameObject.AddComponent<Text>();
        t.font = _font;
        t.fontSize = size;
        t.alignment = anchor;
        t.color = Color.white;
        t.supportRichText = true;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Truncate;
        t.raycastTarget = false;
        return t;
    }

    UiButton MakeButton(Transform parent, string label, Vector2 aMin, Vector2 aMax, UnityAction onClick, Color color, int size)
    {
        Image img = Panel(parent, "Button " + label, aMin, aMax, color);
        var b = img.gameObject.AddComponent<Button>();
        b.targetGraphic = img;
        b.onClick.AddListener(onClick);
        Text t = Label(img.transform, "Label", V(0, 0), V(1, 1), size, TextAnchor.MiddleCenter);
        t.text = label;
        return new UiButton { Button = b, Label = t };
    }

    // ---- screens: menu ---------------------------------------------------------------------

    void BuildMenu()
    {
        _menuRoot = Rect(_canvas.transform, "Menu", V(0, 0), V(1, 1)).gameObject;
        Text title = Label(_menuRoot.transform, "Title", V(0.08f, 0.86f), V(0.92f, 0.98f), 36, TextAnchor.MiddleCenter);
        title.text = "<b>Texas Hold'em & Blackjack</b>";
        Text sub = Label(_menuRoot.transform, "Subtitle", V(0.08f, 0.79f), V(0.92f, 0.86f), 18, TextAnchor.MiddleCenter);
        sub.text = "PBT 4 | Java 5 prototype - progression, information and feedback";

        MakeButton(_menuRoot.transform, "3D demo: full table, avatars, free play vs 3 bots",
            V(0.2f, 0.67f), V(0.8f, 0.76f), delegate { Open3D(); }, new Color(0.62f, 0.45f, 0.08f), 22);

        for (int i = 0; i < Levels.All.Length; i++)
        {
            int index = i;
            LevelDef l = Levels.All[i];
            float top = 0.62f - i * 0.10f;
            MakeButton(_menuRoot.transform, "Level " + l.Number + ": " + l.Title + "   [" + l.PhaseLabel + "]",
                V(0.2f, top - 0.085f), V(0.8f, top), delegate { StartLevel(index); }, new Color(0.12f, 0.45f, 0.30f), 20);
        }
        float bjTop = 0.62f - Levels.All.Length * 0.10f;
        MakeButton(_menuRoot.transform, "Blackjack: scripted 3-hand mode (parallel track)",
            V(0.2f, bjTop - 0.085f), V(0.8f, bjTop), delegate { StartBlackjack(); }, new Color(0.35f, 0.25f, 0.55f), 20);

        Text note = Label(_menuRoot.transform, "Note", V(0.08f, 0.04f), V(0.92f, 0.20f), 15, TextAnchor.UpperCenter);
        note.text = "Levels 1-3 use fixed cards so each solution on the Level Cards can be replayed and checked.\n" +
                    "Hold'em levels share one mechanic; difficulty rises through opponents and decisions, not speed.";
    }

    // ---- screens: Hold'em ---------------------------------------------------------------------

    void BuildGame()
    {
        _gameRoot = Rect(_canvas.transform, "Game", V(0, 0), V(1, 1)).gameObject;
        Transform g = _gameRoot.transform;

        _title = Label(g, "Title", V(0.02f, 0.91f), V(0.86f, 0.99f), 24, TextAnchor.MiddleLeft);
        MakeButton(g, "Menu", V(0.88f, 0.92f), V(0.98f, 0.99f), delegate { ShowMenu(); }, new Color(0.45f, 0.20f, 0.20f), 16);

        Panel(g, "InfoPanel", V(0.02f, 0.31f), V(0.34f, 0.90f), new Color(0, 0, 0, 0.35f)).raycastTarget = false;
        _info = Label(g, "Info", V(0.02f, 0.31f), V(0.34f, 0.90f), 14, TextAnchor.UpperLeft);

        Panel(g, "TipsPanel", V(0.02f, 0.03f), V(0.34f, 0.29f), new Color(0.25f, 0.20f, 0.02f, 0.65f)).raycastTarget = false;
        _tipsText = Label(g, "Tips", V(0.02f, 0.03f), V(0.34f, 0.29f), 14, TextAnchor.UpperLeft);

        _board = Label(g, "Board", V(0.36f, 0.78f), V(0.98f, 0.90f), 24, TextAnchor.MiddleLeft);
        Panel(g, "SeatsPanel", V(0.36f, 0.44f), V(0.98f, 0.77f), new Color(0, 0, 0, 0.25f)).raycastTarget = false;
        _seatsText = Label(g, "Seats", V(0.36f, 0.44f), V(0.98f, 0.77f), 17, TextAnchor.UpperLeft);
        Panel(g, "FeedPanel", V(0.36f, 0.18f), V(0.98f, 0.43f), new Color(0, 0, 0, 0.45f)).raycastTarget = false;
        _feedText = Label(g, "Feed", V(0.36f, 0.18f), V(0.98f, 0.43f), 15, TextAnchor.LowerLeft);

        // action buttons (9 across)
        _actionsRoot = Rect(g, "Actions", V(0, 0), V(1, 1)).gameObject;
        Transform a = _actionsRoot.transform;
        Color neutral = new Color(0.18f, 0.32f, 0.50f);
        UiButton[] row = new UiButton[9];
        string[] labels = { "Fold", "Check", "Bet 5", "Bet 10", "Bet 20", "Half pot", "Pot", "All-in", "Hint" };
        UnityAction[] actions =
        {
            OnFold, OnCheckCall, delegate { OnBet(5); }, delegate { OnBet(10); }, delegate { OnBet(20); },
            OnHalfPot, OnPot, OnAllIn, OnHint
        };
        for (int i = 0; i < 9; i++)
        {
            float x0 = 0.36f + i * 0.069f;
            Color c = i == 0 ? new Color(0.55f, 0.18f, 0.18f) : (i == 8 ? new Color(0.45f, 0.38f, 0.08f) : neutral);
            row[i] = MakeButton(a, labels[i], V(x0, 0.03f), V(x0 + 0.0655f, 0.16f), actions[i], c, 14);
        }
        _foldBtn = row[0]; _ccBtn = row[1]; _bet5 = row[2]; _bet10 = row[3]; _bet20 = row[4];
        _betHalf = row[5]; _betPot = row[6]; _allIn = row[7]; _hintBtn = row[8];

        // end-of-hand buttons
        _endRoot = Rect(g, "End", V(0, 0), V(1, 1)).gameObject;
        Transform e = _endRoot.transform;
        _retryBtn = MakeButton(e, "Retry this hand", V(0.36f, 0.03f), V(0.55f, 0.16f), delegate { StartLevel(_levelIndex); }, neutral, 18);
        _nextBtn = MakeButton(e, "Next level", V(0.57f, 0.03f), V(0.76f, 0.16f), delegate { StartLevel(_levelIndex + 1); }, new Color(0.12f, 0.45f, 0.30f), 18);
        MakeButton(e, "Menu", V(0.78f, 0.03f), V(0.98f, 0.16f), delegate { ShowMenu(); }, new Color(0.45f, 0.20f, 0.20f), 18);
    }

    // ---- screens: Blackjack ---------------------------------------------------------------------

    void BuildBlackjack()
    {
        _bjRoot = Rect(_canvas.transform, "Blackjack", V(0, 0), V(1, 1)).gameObject;
        Transform g = _bjRoot.transform;
        _bjTitle = Label(g, "Title", V(0.04f, 0.88f), V(0.84f, 0.98f), 26, TextAnchor.MiddleLeft);
        MakeButton(g, "Menu", V(0.88f, 0.90f), V(0.98f, 0.98f), delegate { ShowMenu(); }, new Color(0.45f, 0.20f, 0.20f), 16);
        Panel(g, "TablePanel", V(0.04f, 0.46f), V(0.96f, 0.86f), new Color(0, 0, 0, 0.3f)).raycastTarget = false;
        _bjTable = Label(g, "Table", V(0.04f, 0.46f), V(0.96f, 0.86f), 24, TextAnchor.UpperLeft);
        Panel(g, "MessagePanel", V(0.04f, 0.22f), V(0.96f, 0.44f), new Color(0, 0, 0, 0.45f)).raycastTarget = false;
        _bjMessage = Label(g, "Message", V(0.04f, 0.22f), V(0.96f, 0.44f), 18, TextAnchor.UpperLeft);

        Color neutral = new Color(0.18f, 0.32f, 0.50f);
        _bjHit = MakeButton(g, "Hit", V(0.04f, 0.04f), V(0.24f, 0.18f), delegate { OnBjHit(); }, neutral, 22);
        _bjStand = MakeButton(g, "Stand", V(0.26f, 0.04f), V(0.46f, 0.18f), delegate { OnBjStand(); }, neutral, 22);
        _bjHint = MakeButton(g, "Hint", V(0.48f, 0.04f), V(0.64f, 0.18f), delegate { _bjHintOn = true; RefreshBlackjack(); }, new Color(0.45f, 0.38f, 0.08f), 20);
        _bjNext = MakeButton(g, "Next hand", V(0.66f, 0.04f), V(0.96f, 0.18f), delegate { OnBjNext(); }, new Color(0.12f, 0.45f, 0.30f), 22);
    }

    // =====================================================================================
    // navigation
    // =====================================================================================

    void ShowMenu()
    {
        StopAllCoroutines();
        _botsActing = false;
        _menuRoot.SetActive(true);
        _gameRoot.SetActive(false);
        _bjRoot.SetActive(false);
    }

    /// <summary>Opens the 3D table. The 2D screen is hidden until the player leaves the 3D demo.</summary>
    void Open3D()
    {
        StopAllCoroutines();
        _canvas.gameObject.SetActive(false);
        var go = new GameObject("Game3D");
        go.AddComponent<Game3D>().Open(delegate
        {
            if (_canvas != null) _canvas.gameObject.SetActive(true);
            ShowMenu();
        });
    }

    void ShowGame()
    {
        _menuRoot.SetActive(false);
        _gameRoot.SetActive(true);
        _bjRoot.SetActive(false);
    }

    // =====================================================================================
    // Hold'em flow
    // =====================================================================================

    void StartLevel(int index)
    {
        if (index < 0 || index >= Levels.All.Length) return;
        StopAllCoroutines();
        _levelIndex = index;
        _level = Levels.All[index];
        _feedLines.Clear();
        _eventsShown = 0;
        _hintOn = false;
        _botsActing = false;
        _configError = null;
        ShowGame();

        try
        {
            _hand = _level.CreateHand();
        }
        catch (LevelConfigException e)
        {
            _hand = null;
            _configError = e.Message;
            RefreshGame();
            return;
        }

        AddFeed("<b>Level " + _level.Number + ": " + _level.Title + "</b> - " + _level.Objective);
        SyncEvents();
        StartCoroutine(RunBots());
    }

    IEnumerator RunBots()
    {
        _botsActing = true;
        RefreshGame();
        while (_hand != null && !_hand.IsComplete && !_hand.Seats[_hand.Actor].IsHuman)
        {
            yield return new WaitForSeconds(0.7f);
            bool failed = false;
            try
            {
                _hand.Apply(BotPolicy.Decide(_hand, _hand.Actor));
            }
            catch (System.Exception ex)
            {
                AddFeed("<color=#ff6b6b>Internal error: " + ex.Message + "</color>");
                failed = true;
            }
            SyncEvents();
            RefreshGame();
            if (failed) break;
        }
        _botsActing = false;
        if (_hand != null && _hand.IsComplete) OnHandComplete();
        RefreshGame();
    }

    void OnHandComplete()
    {
        AddFeed("<b>" + Feedback.HandSummary(_hand) + "</b>");
        AddFeed(Feedback.RevealOpponents(_hand));
        bool passed = _level.IsPassed(_hand.Seats[0].Stack);
        string color = passed ? "#7CFC9A" : "#ffb347";
        AddFeed("<color=" + color + "><b>" + Feedback.PassMessage(_level, _hand) + "</b></color>");
    }

    void Act(PlayerAction action)
    {
        if (_hand == null || _hand.IsComplete || _botsActing) return;
        if (!_hand.Seats[_hand.Actor].IsHuman) return;

        string error = _hand.Validate(action);
        if (error != null)
        {
            AddFeed("<color=#ffb347>Refused: " + error + "</color>");
            RefreshGame();
            return;
        }
        _hand.Apply(action);
        SyncEvents();
        StartCoroutine(RunBots());
    }

    void OnFold() { Act(PlayerAction.Fold()); }

    void OnCheckCall()
    {
        if (_hand == null || _hand.IsComplete) return;
        Act(_hand.ToCall(0) > 0 ? PlayerAction.Call() : PlayerAction.Check());
    }

    void OnBet(int increment)
    {
        if (_hand == null || _hand.IsComplete) return;
        Act(PlayerAction.BetTo(_hand.CurrentBet + increment));
    }

    void OnHalfPot() { if (_hand != null) OnBet(Mathf.Max(1, _hand.Pot / 2)); }
    void OnPot() { if (_hand != null) OnBet(Mathf.Max(1, _hand.Pot)); }

    void OnAllIn()
    {
        if (_hand == null || _hand.IsComplete) return;
        Seat me = _hand.Seats[0];
        int total = me.StreetBet + me.Stack;
        if (total <= _hand.CurrentBet) Act(PlayerAction.Call());   // cannot raise: all-in call
        else Act(PlayerAction.BetTo(total));
    }

    void OnHint()
    {
        _hintOn = true;
        RefreshGame();
    }

    void SyncEvents()
    {
        if (_hand == null) return;
        while (_eventsShown < _hand.Events.Count)
        {
            AddFeed(_hand.Events[_eventsShown]);
            _eventsShown++;
        }
    }

    void AddFeed(string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        string[] lines = text.Split('\n');
        for (int i = 0; i < lines.Length; i++) _feedLines.Add(lines[i]);
        while (_feedLines.Count > 60) _feedLines.RemoveAt(0);
    }

    // ---- drawing -------------------------------------------------------------------------------

    static string CardText(Card c)
    {
        string s = Card.RankLabel(c.Rank) + Card.SuitLetter(c.Suit);
        return c.IsRed ? "<color=#ff7b7b>" + s + "</color>" : s;
    }

    void RefreshGame()
    {
        if (_level == null) return;
        _title.text = "<b>Level " + _level.Number + ": " + _level.Title + "</b>   [" + _level.PhaseLabel + "]";

        if (_hand == null)
        {
            _info.text = "<b>Level configuration error</b>\n" + _configError +
                         "\n\nThis is a defect in the level setup, not a mistake by the player. Go back to the menu and reload the level.";
            _board.text = "";
            _seatsText.text = "";
            _feedText.text = "";
            _tipsText.text = "";
            _actionsRoot.SetActive(false);
            _endRoot.SetActive(false);
            return;
        }

        _info.text = BuildInfoText();
        _board.text = BuildBoardText();
        _seatsText.text = BuildSeatsText();

        int first = Mathf.Max(0, _feedLines.Count - 9);
        var feed = new StringBuilder();
        for (int i = first; i < _feedLines.Count; i++)
        {
            if (feed.Length > 0) feed.Append("\n");
            feed.Append(_feedLines[i]);
        }
        _feedText.text = feed.ToString();

        bool myTurn = !_hand.IsComplete && !_botsActing && _hand.Seats[_hand.Actor].IsHuman;
        _tipsText.text = BuildTipsText(myTurn);

        _actionsRoot.SetActive(!_hand.IsComplete);
        _endRoot.SetActive(_hand.IsComplete);

        if (!_hand.IsComplete)
        {
            UiButton[] all = { _foldBtn, _ccBtn, _bet5, _bet10, _bet20, _betHalf, _betPot, _allIn };
            for (int i = 0; i < all.Length; i++) all[i].Button.interactable = myTurn;
            _hintBtn.Button.interactable = true;

            int toCall = _hand.ToCall(0);
            _ccBtn.Label.text = toCall > 0 ? "Call " + toCall : "Check";
            string prefix = _hand.CurrentBet > 0 ? "Raise +" : "Bet ";
            _bet5.Label.text = prefix + "5";
            _bet10.Label.text = prefix + "10";
            _bet20.Label.text = prefix + "20";
        }
        else
        {
            bool passed = _level.IsPassed(_hand.Seats[0].Stack);
            _nextBtn.Button.interactable = passed && _levelIndex + 1 < Levels.All.Length;
        }
    }

    string BuildInfoText()
    {
        var sb = new StringBuilder();
        sb.Append("<b>Objective</b>\n" + _level.Objective + "\n\n");
        sb.Append("<b>Skill</b>\n" + _level.TargetSkill + "\n\n");
        sb.Append("<b>Opponents</b>\n" + _level.Constraints + "\n\n");
        sb.Append("<b>To pass</b>\n" + _level.PassText + "\n\n");
        sb.Append("<b>Rules</b>\nBlinds " + _level.SmallBlind + "/" + _level.BigBlind + ", stacks " + _level.StartStack +
                  ". Best 5 of 7 cards wins. You act first after the flop. Minimum bet or raise = " + _level.BigBlind + ".\n\n");
        sb.Append("<b>Hand ranks (best first)</b>\nStraight flush, Four of a kind, Full house, Flush, Straight, Three of a kind, Two pair, Pair, High card.\n\n");
        sb.Append("<b>Cards</b>\nc clubs, s spades, <color=#ff7b7b>h hearts, d diamonds</color>. A K Q J = face cards.");
        return sb.ToString();
    }

    string BuildBoardText()
    {
        string[] names = { "Preflop", "Flop", "Turn", "River", "Hand over" };
        var sb = new StringBuilder();
        sb.Append("<b>" + names[(int)_hand.Street] + "</b>     Pot: " + _hand.Pot + "     Board:  ");
        List<Card> vis = _hand.VisibleBoard;
        for (int i = 0; i < 5; i++)
        {
            sb.Append(i < vis.Count ? CardText(vis[i]) : "[ ]");
            sb.Append("  ");
        }
        return sb.ToString();
    }

    string BuildSeatsText()
    {
        var sb = new StringBuilder();
        for (int i = 0; i < _hand.Seats.Count; i++)
        {
            Seat s = _hand.Seats[i];
            bool showCards = s.IsHuman || _hand.IsComplete;
            string cards = showCards ? CardText(s.Hole[0]) + " " + CardText(s.Hole[1]) : "?? ??";
            string status = s.Folded ? "   (folded)" : (s.AllIn ? "   (all-in)" : "");
            string bet = s.StreetBet > 0 ? "   bet " + s.StreetBet : "";
            string marker = (!_hand.IsComplete && _hand.Actor == i) ? "<color=#ffe066>> </color>" : "   ";
            sb.Append(marker + "<b>" + s.Name + "</b>   " + cards + "   stack " + s.Stack + bet + status + "\n");
        }
        return sb.ToString();
    }

    string BuildTipsText(bool myTurn)
    {
        var sb = new StringBuilder();
        sb.Append("<b>Tips</b>\n");
        if (_hintOn) sb.Append(_level.Hint + "\n");
        if (myTurn)
        {
            int toCall = _hand.ToCall(0);
            if (_level.ShowFirstActionPrompt && _hand.Street == Phase.Preflop && toCall > 0)
                sb.Append(Feedback.FirstActionPrompt(toCall) + "\n");
            if (_level.ShowRiverHint)
            {
                string rh = Feedback.RiverHint(_hand);
                if (rh != null) sb.Append(rh + "\n");
            }
            if (_level.ShowOutsHelper)
            {
                string oh = Feedback.OutsHelper(_hand, 0);
                if (oh != null) sb.Append(oh + "\n");
            }
        }
        return sb.ToString();
    }

    // =====================================================================================
    // Blackjack flow
    // =====================================================================================

    void StartBlackjack()
    {
        StopAllCoroutines();
        _menuRoot.SetActive(false);
        _gameRoot.SetActive(false);
        _bjRoot.SetActive(true);
        _bjHintOn = false;
        _bjError = null;
        _bj = new BlackjackSession();
        try { _bj.StartHand(); }
        catch (LevelConfigException e) { _bjError = e.Message; }
        RefreshBlackjack();
    }

    void OnBjHit()
    {
        if (_bj == null || _bj.Current == null || _bj.Current.IsFinished) return;
        try { _bj.Current.Hit(); }
        catch (LevelConfigException e) { _bjError = e.Message; }
        RefreshBlackjack();
    }

    void OnBjStand()
    {
        if (_bj == null || _bj.Current == null || _bj.Current.IsFinished) return;
        try { _bj.Current.Stand(); }
        catch (LevelConfigException e) { _bjError = e.Message; }
        RefreshBlackjack();
    }

    void OnBjNext()
    {
        if (_bj == null) return;
        _bjHintOn = false;
        try
        {
            if (_bj.IsOver)
            {
                _bj.Restart();
                _bj.StartHand();
            }
            else if (_bj.Current != null && _bj.Current.IsFinished)
            {
                _bj.FinishHand();
                if (!_bj.IsOver) _bj.StartHand();
            }
        }
        catch (LevelConfigException e) { _bjError = e.Message; }
        RefreshBlackjack();
    }

    static string BjCards(List<Card> cards, bool hideSecond)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < cards.Count; i++)
        {
            if (i > 0) sb.Append("  ");
            sb.Append(hideSecond && i == 1 ? "??" : CardText(cards[i]));
        }
        return sb.ToString();
    }

    void RefreshBlackjack()
    {
        if (_bj == null) return;
        _bjTitle.text = "<b>Blackjack</b>   (dealer stands on 17, Blackjack pays 3:2, bet " + BlackjackSession.BetSize + ")";

        if (_bjError != null)
        {
            _bjTable.text = "<b>Level configuration error</b>\n" + _bjError;
            _bjMessage.text = "This is a defect in the scripted shoe, not a player mistake. Go back to the menu and reload.";
            _bjHit.Button.interactable = false;
            _bjStand.Button.interactable = false;
            _bjHint.Button.interactable = false;
            _bjNext.Button.interactable = false;
            return;
        }

        if (_bj.IsOver)
        {
            _bjTable.text = "<b>Session over</b>\nChips: " + _bj.Chips + "   (perfect run = 135, pass = " + BlackjackSession.PassChips + ")";
            _bjMessage.text = _bj.Passed
                ? "<color=#7CFC9A><b>Passed.</b></color> You finished with " + _bj.Chips + " chips."
                : "<color=#ffb347><b>Not passed.</b></color> You needed at least " + BlackjackSession.PassChips + " chips and finished with " + _bj.Chips + ". Play again to retry.";
            _bjHit.Button.interactable = false;
            _bjStand.Button.interactable = false;
            _bjHint.Button.interactable = false;
            _bjNext.Button.interactable = true;
            _bjNext.Label.text = "Play again";
            return;
        }

        BlackjackHand h = _bj.Current;
        var sb = new StringBuilder();
        sb.Append("Hand " + (_bj.HandIndex + 1) + " of " + _bj.HandCount + "      Chips: " + _bj.Chips + "\n\n");
        bool hide = h.HoleHidden;
        string dealerTotal = hide
            ? "showing " + BlackjackHand.Total(h.Dealer.GetRange(0, 1))
            : "total " + BlackjackHand.Total(h.Dealer);
        sb.Append("<b>Dealer</b>   " + BjCards(h.Dealer, hide) + "    (" + dealerTotal + ")\n\n");
        sb.Append("<b>You</b>      " + BjCards(h.Player, false) + "    (total " + BlackjackHand.Total(h.Player) + ")");
        _bjTable.text = sb.ToString();

        var msg = new StringBuilder();
        if (h.IsFinished)
        {
            switch (h.Outcome)
            {
                case BlackjackOutcome.PlayerBlackjack: msg.Append("<b>Natural Blackjack!</b> Ace + 10-value card = 21 with two cards, so the hand ends immediately (no Hit or Stand) and pays 3:2: +" + h.Net + " chips."); break;
                case BlackjackOutcome.PlayerWin: msg.Append("<b>You win</b> +" + h.Net + " chips."); break;
                case BlackjackOutcome.Push: msg.Append("<b>Push.</b> Your bet is returned."); break;
                case BlackjackOutcome.PlayerBust: msg.Append("<b>Bust</b> with " + BlackjackHand.Total(h.Player) + ". You lose " + (-h.Net) + " chips."); break;
                case BlackjackOutcome.DealerBlackjack: msg.Append("<b>Dealer has Blackjack.</b> You lose " + (-h.Net) + " chips."); break;
                default: msg.Append("<b>Dealer wins</b> with " + BlackjackHand.Total(h.Dealer) + " against your " + BlackjackHand.Total(h.Player) + ". You lose " + (-h.Net) + " chips."); break;
            }
            msg.Append("\nPress Next hand.");
        }
        else
        {
            msg.Append("Hit takes a card. Stand ends your turn and the dealer plays.");
            if (_bjHintOn) msg.Append("\n<color=#ffe066>" + BlackjackSession.HintFor(_bj.HandIndex) + "</color>");
        }
        _bjMessage.text = msg.ToString();

        _bjHit.Button.interactable = !h.IsFinished;
        _bjStand.Button.interactable = !h.IsFinished;
        _bjHint.Button.interactable = !h.IsFinished;
        _bjNext.Button.interactable = h.IsFinished;
        _bjNext.Label.text = _bj.HandIndex == _bj.HandCount - 1 ? "Finish" : "Next hand";
    }
}
