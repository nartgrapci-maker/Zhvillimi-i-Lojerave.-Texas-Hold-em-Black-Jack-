using UnityEngine;
using UnityEngine.UI;
using PokerDemo.Core;

/// <summary>
/// Two clear on-screen card displays (the 3D cards are small when you look with a wide field of view):
/// a strip with the cards on the table while a hand is played, and, once the cards are turned over at the end,
/// a panel with the table cards and everybody's cards, their hand names and what they won.
/// </summary>
public partial class Game3D
{
    class CardTile
    {
        public Image Bg;
        public Text Rank;
        public RawImage Suit;

        public void Show(Card c)
        {
            Color ink = c.IsRed ? new Color(0.78f, 0.07f, 0.10f) : new Color(0.07f, 0.07f, 0.11f);
            Bg.color = new Color(0.97f, 0.96f, 0.92f, 1f);
            Rank.text = "<b>" + Card.RankLabel(c.Rank) + "</b>";
            Rank.color = ink;
            Suit.enabled = true;
            Suit.texture = SuitTextures.Get(c.Suit);
            Suit.color = ink;
        }

        public void ShowBack()
        {
            Bg.color = new Color(0.62f, 0.10f, 0.17f, 1f);
            Rank.text = "";
            Suit.enabled = true;
            Suit.texture = Theme.Eagle;
            Suit.color = new Color(0.1f, 0.05f, 0.05f, 0.9f);
        }

        public void ShowEmpty()
        {
            Bg.color = new Color(1f, 1f, 1f, 0.10f);
            Rank.text = "";
            Suit.enabled = false;
        }
    }

    GameObject _boardStrip, _showPanel;
    CardTile[] _stripTiles = new CardTile[5];
    CardTile[] _showBoardTiles = new CardTile[5];
    CardTile[,] _showHoleTiles = new CardTile[4, 2];
    Text[] _showName = new Text[4], _showHand = new Text[4];
    bool _revealOn, _revealHidden;

    /// <summary>A rectangle given in pixels (of a panel pw x ph, measured from its top left corner).</summary>
    static RectTransform Px(Transform parent, string name, float pw, float ph, float x, float yTop, float w, float h)
    {
        return NewRect(parent, name, V(x / pw, 1f - (yTop + h) / ph), V((x + w) / pw, 1f - yTop / ph));
    }

    static Text PxLabel(Transform parent, string name, float pw, float ph, float x, float yTop, float w, float h, int size, TextAnchor anchor)
    {
        RectTransform rt = Px(parent, name, pw, ph, x, yTop, w, h);
        Text t = rt.gameObject.AddComponent<Text>();
        t.font = Mats.Font;
        t.fontSize = size;
        t.alignment = anchor;
        t.color = Color.white;
        t.supportRichText = true;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }

    static CardTile PxTile(Transform parent, float pw, float ph, float x, float yTop, float w, float h, int rankSize)
    {
        RectTransform rt = Px(parent, "Tile", pw, ph, x, yTop, w, h);
        var tile = new CardTile();
        tile.Bg = rt.gameObject.AddComponent<Image>();
        tile.Bg.raycastTarget = false;
        tile.Rank = PxLabel(rt, "Rank", w, h, 0f, 1f, w, h * 0.46f, rankSize, TextAnchor.MiddleCenter);
        RectTransform sr = Px(rt, "Suit", w, h, w * 0.2f, h * 0.46f, w * 0.6f, h * 0.46f);
        tile.Suit = sr.gameObject.AddComponent<RawImage>();
        tile.Suit.raycastTarget = false;
        tile.ShowEmpty();
        return tile;
    }

    void BuildCardPanels(Transform h)
    {
        // the cards on the table, while the hand is played
        float sw = 290f, sh = 80f;
        _boardStrip = NewPanel(h, "BoardStrip", V(0.5f - sw / 2560f, 0.775f), V(0.5f + sw / 2560f, 0.775f + sh / 720f), new Color(0.04f, 0.01f, 0.03f, 0.55f)).gameObject;
        for (int i = 0; i < 5; i++) _stripTiles[i] = PxTile(_boardStrip.transform, sw, sh, 14f + i * 54f, 8f, 46f, 64f, 24);

        // the reveal: table cards, then one column for each player
        float pw = 600f, ph = 274f;
        _showPanel = NewPanel(h, "Showdown", V(0.5f - pw / 2560f, 0.88f - ph / 720f), V(0.5f + pw / 2560f, 0.88f), new Color(0.04f, 0.01f, 0.03f, 0.84f)).gameObject;
        Transform sp = _showPanel.transform;
        Gold(sp, V(0f, 0f), V(1f, 0.008f)); Gold(sp, V(0f, 0.992f), V(1f, 1f)); Gold(sp, V(0f, 0f), V(0.0033f, 1f)); Gold(sp, V(0.9967f, 0f), V(1f, 1f));
        Text title = PxLabel(sp, "Title", pw, ph, 0f, 7f, pw, 20f, 13, TextAnchor.MiddleCenter);
        title.text = "<b>LETRAT NË TAVOLINË</b>";
        title.color = new Color(0.95f, 0.78f, 0.35f);
        for (int i = 0; i < 5; i++) _showBoardTiles[i] = PxTile(sp, pw, ph, 169f + i * 54f, 30f, 46f, 64f, 24);
        NewPanel(sp, "Rule", V(0.04f, 1f - 104f / ph), V(0.96f, 1f - 103f / ph), new Color(0.85f, 0.65f, 0.20f, 0.45f));
        for (int c = 0; c < 4; c++)
        {
            float cx = c * 150f;
            _showName[c] = PxLabel(sp, "Name" + c, pw, ph, cx, 110f, 150f, 20f, 14, TextAnchor.MiddleCenter);
            _showHoleTiles[c, 0] = PxTile(sp, pw, ph, cx + 31f, 134f, 40f, 56f, 20);
            _showHoleTiles[c, 1] = PxTile(sp, pw, ph, cx + 79f, 134f, 40f, 56f, 20);
            _showHand[c] = PxLabel(sp, "Hand" + c, pw, ph, cx + 4f, 196f, 142f, 70f, 12, TextAnchor.UpperCenter);
        }
        UiBtn close = NewButton(sp, "×", V(1f - 42f / pw, 1f - 38f / ph), V(1f - 6f / pw, 1f - 6f / ph), delegate { _revealHidden = true; RefreshHud(); }, new Color(0.3f, 0.2f, 0.25f), 14);
        close.Go.name = "Button Fshih letrat";
    }

    /// <summary>Shows the strip while a hand is played and the reveal panel when the cards are turned over.</summary>
    void RefreshCardPanels(bool menu, bool holdem, bool card)
    {
        bool inHand = !menu && holdem && _hand != null && !card;
        bool reveal = inHand && _revealOn && !_revealHidden;
        bool strip = inHand && !_revealOn && _boardShown > 0;
        _boardStrip.SetActive(strip);
        _showPanel.SetActive(reveal);
        if (strip)
        {
            Card[] board = _hand.Board;
            for (int i = 0; i < 5; i++)
            {
                if (i < _boardShown && i < board.Length) _stripTiles[i].Show(board[i]);
                else _stripTiles[i].ShowEmpty();
            }
        }
        if (!reveal) return;

        HandResult r = _hand.Result;
        Card[] b = _hand.Board;
        for (int i = 0; i < 5; i++)
        {
            if (i < b.Length) _showBoardTiles[i].Show(b[i]);
            else _showBoardTiles[i].ShowEmpty();
        }
        int n = Mathf.Min(4, _hand.Seats.Count);
        float shift = (4 - n) * 75f;            // fewer players: keep the columns in the middle
        for (int c = 0; c < 4; c++)
        {
            bool on = c < n;
            _showName[c].gameObject.SetActive(on);
            _showHand[c].gameObject.SetActive(on);
            _showHoleTiles[c, 0].Bg.gameObject.SetActive(on);
            _showHoleTiles[c, 1].Bg.gameObject.SetActive(on);
            if (!on) continue;
            Seat s = _hand.Seats[c];
            bool won = r != null && r.Won != null && r.Won[c] > 0;
            string nm = Sq.Name(s.Name);
            if (r != null && r.Net != null && r.Net[c] > 0) nm += "  <color=#7CFC9A>+" + r.Net[c] + "</color>";
            _showName[c].text = won ? "<color=#ffd34d><b>" + nm + "</b></color>" : "<b>" + nm + "</b>";
            for (int k = 0; k < 2; k++)
            {
                if (s.Folded) _showHoleTiles[c, k].ShowBack();
                else _showHoleTiles[c, k].Show(s.Hole[k]);
            }
            string hn = s.Folded ? "hoqi dorë" : Sq.HandName(_hand, c);
            _showHand[c].text = hn ?? "";
            _showHand[c].color = won ? new Color(1f, 0.85f, 0.4f) : new Color(0.85f, 0.85f, 0.88f);
            // re-centre the columns when fewer than four play
            ShiftColumn(c, shift);
        }
    }

    void ShiftColumn(int c, float shift)
    {
        float pw = 600f, ph = 274f;
        float cx = c * 150f + shift;
        SetPx(_showName[c], pw, ph, cx, 110f, 150f, 20f);
        SetPx(_showHoleTiles[c, 0].Bg, pw, ph, cx + 31f, 134f, 40f, 56f);
        SetPx(_showHoleTiles[c, 1].Bg, pw, ph, cx + 79f, 134f, 40f, 56f);
        SetPx(_showHand[c], pw, ph, cx + 4f, 196f, 142f, 70f);
    }

    static void SetPx(Graphic g, float pw, float ph, float x, float yTop, float w, float h)
    {
        var rt = (RectTransform)g.transform;
        rt.anchorMin = V(x / pw, 1f - (yTop + h) / ph);
        rt.anchorMax = V((x + w) / pw, 1f - yTop / ph);
    }
}
