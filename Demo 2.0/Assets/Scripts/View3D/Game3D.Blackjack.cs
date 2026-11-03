using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using PokerDemo.Core;

/// <summary>
/// Blackjack at the 3D table: the free game (you choose the bet, double down, 4-deck shoe, basic-strategy advice)
/// and the three scripted teaching hands. Same rules and cards as the Core; the text comes from Sq.
/// </summary>
public partial class Game3D
{
    // shared by both kinds of Blackjack
    readonly List<CardView> _bjPlayer = new List<CardView>();
    readonly List<CardView> _bjDealer = new List<CardView>();
    GameObject _bjBetPile, _bjStackPile;
    int _bjBetShown = -1, _bjStackShown = -1;
    bool _bjBusy, _bjHoleRevealed;

    // scripted lesson
    BlackjackSession _bj;
    bool _bjSummary, _bjHint;

    // free game
    BlackjackTable _bt;
    BlackjackRound _br;
    bool _bfHint;

    static Vector3 BjCardPos(bool player, int i)
    {
        // up to five cards in a row, then a second row
        int row = i / 5, col = i % 5;
        float z = player ? -1.15f + row * 0.5f : 1.7f - row * 0.5f;
        return new Vector3(-0.85f + col * 0.56f + row * 0.28f, TableTop + 0.03f + row * 0.004f, z);
    }

    void BjResetTable()
    {
        StopAllCoroutines();
        if (_tableRoot != null) Destroy(_tableRoot);
        _tableRoot = new GameObject("Table objects");
        _tableRoot.transform.SetParent(_dyn.transform, false);
        _bjPlayer.Clear();
        _bjDealer.Clear();
        _bjBetPile = null;
        _bjStackPile = null;
        _bjBetShown = -1;
        _bjStackShown = -1;
        _bjHoleRevealed = false;
        _banner = "";
        for (int i = 0; i < _avatars.Count; i++)
        {
            _avatars[i].SetMood(AvatarView.Mood.Idle);
            _avatars[i].SetTurn(false);
        }
    }

    CardView BjAddCard(Card c, bool player, int index, bool faceUp)
    {
        CardView cv = CardView.Create(_tableRoot.transform, c, DeckPos + Vector3.up * 0.02f, CardView.FaceDown(0f));
        cv.Tween.Go(BjCardPos(player, index), faceUp ? CardView.FaceUp(0f) : CardView.FaceDown(0f), 0.45f, 0f, 0.5f);
        (player ? _bjPlayer : _bjDealer).Add(cv);
        Play(Sfx.Card, 0.8f);
        return cv;
    }

    void BjSetPiles(int bet, int stack)
    {
        if (_bjBetShown != bet || _bjBetPile == null)
        {
            if (_bjBetPile != null) Destroy(_bjBetPile);
            _bjBetPile = bet > 0 ? ChipPile.Build(_tableRoot.transform, bet, new Vector3(1.75f, TableTop + 0.02f, -0.6f), true) : null;
            _bjBetShown = bet;
        }
        if (_bjStackShown != stack || _bjStackPile == null)
        {
            if (_bjStackPile != null) Destroy(_bjStackPile);
            _bjStackPile = stack > 0 ? ChipPile.Build(_tableRoot.transform, stack, StackPos(0), true) : null;
            _bjStackShown = stack;
        }
    }

    void BjInfo(List<Card> player, List<Card> dealer, bool holeHidden)
    {
        _avatars[0].SetInfo("Totali " + BlackjackHand.Total(player));
        _avatars[1].SetInfo(holeHidden ? "Tregon " + BlackjackHand.Total(dealer.GetRange(0, 1)) : "Totali " + BlackjackHand.Total(dealer));
    }

    /// <summary>Brings the 3D cards in line with the hand: new player cards, the dealer's hole card, dealer draws.</summary>
    IEnumerator BjSync(List<Card> player, List<Card> dealer, bool holeHidden)
    {
        for (int i = _bjPlayer.Count; i < player.Count; i++)
        {
            BjAddCard(player[i], true, i, true);
            BjInfo(player, dealer, holeHidden);
            yield return Wait(0.5f);
        }
        if (!holeHidden && !_bjHoleRevealed && _bjDealer.Count >= 2)
        {
            _bjHoleRevealed = true;
            CardView hole = _bjDealer[1];
            hole.Tween.Go(hole.transform.position, CardView.FaceUp(0f), 0.5f, 0f, 0.35f);
            Play(Sfx.Card, 0.8f);
            BjInfo(player, dealer, false);
            yield return Wait(0.8f);
        }
        for (int i = _bjDealer.Count; i < dealer.Count; i++)
        {
            BjAddCard(dealer[i], false, i, true);
            BjInfo(player, dealer, holeHidden);
            yield return Wait(0.8f);
        }
    }

    /// <summary>The first four cards: player, dealer, player, dealer (the last one face down).</summary>
    IEnumerator BjDealFour(List<Card> player, List<Card> dealer)
    {
        yield return Wait(0.3f);
        Play(Sfx.Chip, 0.7f);
        yield return Wait(0.3f);
        BjAddCard(player[0], true, 0, true);
        yield return Wait(0.3f);
        BjAddCard(dealer[0], false, 0, true);
        yield return Wait(0.3f);
        BjAddCard(player[1], true, 1, true);
        yield return Wait(0.3f);
        BjAddCard(dealer[1], false, 1, false);
        yield return Wait(0.5f);
    }

    void BjMoods(int net)
    {
        if (net > 0)
        {
            _avatars[0].SetMood(AvatarView.Mood.Win);
            _avatars[1].SetMood(AvatarView.Mood.Lose);
            _avatars[0].Say("+" + net, 3f);
            Play(Sfx.Win, 0.9f);
            Confetti.Burst(_tableRoot.transform, BjCardPos(true, 1) + Vector3.up * 0.4f, net >= 20 ? 40 : 24, _fxRng);
        }
        else if (net < 0)
        {
            _avatars[0].SetMood(AvatarView.Mood.Lose);
            _avatars[1].SetMood(AvatarView.Mood.Win);
            _avatars[0].Say(net.ToString(), 3f);
            Play(Sfx.Lose, 0.9f);
        }
    }

    // =====================================================================================
    // scripted lesson: 3 hands, same as the 2D version
    // =====================================================================================

    void StartBlackjack()
    {
        StopAllCoroutines();
        ClearDynamic();
        SetCity(_map);
        _mode = Mode.Blackjack;
        _level = null;
        _fp = null;
        _log.Clear();
        _banner = "";
        _bj = new BlackjackSession();
        _bjSummary = false;
        BuildSeats(new string[] { "Ti", "Krupieri" });
        AddLog("<b>Blackjack</b>: 3 duar të programuara, baste prej " + BlackjackSession.BetSize + " " + Sq.Currency +
               ". Krupieri qëndron në të gjitha 17-at. Blackjack paguan 3:2.");
        BjDeal();
    }

    void BjDeal()
    {
        BjResetTable();
        _bjHint = false;
        _bj.StartHand();
        StartCoroutine(BjDealRoutine());
    }

    void BjRefreshChips()
    {
        BlackjackHand h = _bj.Current;
        bool live = h != null && !h.IsFinished;
        int bet = live ? h.Bet : 0;
        int stack = _bj.Chips + (h != null && h.IsFinished ? h.Net : 0) - bet;
        BjSetPiles(bet, stack);
    }

    IEnumerator BjDealRoutine()
    {
        _bjBusy = true;
        RefreshHud();
        BlackjackHand h = _bj.Current;
        AddLog("<b>Dora " + (_bj.HandIndex + 1) + " nga " + _bj.HandCount + "</b>  (basti " + h.Bet + ")");
        BjRefreshChips();
        yield return StartCoroutine(BjDealFour(h.Player, h.Dealer));
        BjInfo(h.Player, h.Dealer, h.HoleHidden);
        if (!h.HoleHidden) yield return StartCoroutine(BjSync(h.Player, h.Dealer, h.HoleHidden));
        yield return StartCoroutine(BjAfter());
    }

    IEnumerator BjAfter()
    {
        BlackjackHand h = _bj.Current;
        BjInfo(h.Player, h.Dealer, h.HoleHidden);
        if (h.IsFinished)
        {
            string msg = Sq.BjResult(h.Outcome, h.Net, BlackjackHand.Total(h.Player), BlackjackHand.Total(h.Dealer));
            AddLog("<b>" + msg + "</b>");
            _banner = msg;
            BjMoods(h.Net);
            BjRefreshChips();
        }
        _bjBusy = false;
        RefreshHud();
        yield break;
    }

    bool BjCanAct()
    {
        return _mode == Mode.Blackjack && _bj != null && _bj.Current != null && !_bj.Current.IsFinished && !_bjBusy && !_bjSummary;
    }

    void OnBjHit()
    {
        if (!BjCanAct()) return;
        StartCoroutine(BjAct(true));
    }

    void OnBjStand()
    {
        if (!BjCanAct()) return;
        StartCoroutine(BjAct(false));
    }

    IEnumerator BjAct(bool hit)
    {
        _bjBusy = true;
        RefreshHud();
        BlackjackHand h = _bj.Current;
        if (hit)
        {
            AddLog("Merr një letër.");
            h.Hit();
        }
        else
        {
            AddLog("Qëndron me " + BlackjackHand.Total(h.Player) + ".");
            h.Stand();
        }
        yield return StartCoroutine(BjSync(h.Player, h.Dealer, h.HoleHidden));
        yield return StartCoroutine(BjAfter());
    }

    void OnBjNext()
    {
        if (_mode != Mode.Blackjack || _bjBusy || _bj == null) return;
        if (_bjSummary)
        {
            _bj.Restart();
            _bjSummary = false;
            _log.Clear();
            BjDeal();
            return;
        }
        if (_bj.Current == null || !_bj.Current.IsFinished) return;
        _bj.FinishHand();
        if (_bj.IsOver)
        {
            _bjSummary = true;
            string msg = _bj.Passed
                ? "Seanca u kalua me " + _bj.Chips + " " + Sq.Currency + (_bj.Chips >= 135 ? " (seancë e përsosur)." : ".")
                : "Seanca mbaroi me " + _bj.Chips + " " + Sq.Currency + ". Të duheshin " + BlackjackSession.PassChips + " për ta kaluar.";
            AddLog("<b>" + msg + "</b>");
            _banner = msg;
            BjSetPiles(0, _bj.Chips);
            if (_bj.Passed) Confetti.Burst(_tableRoot.transform, PotPos + Vector3.up, 40, _fxRng);
            RefreshHud();
            return;
        }
        BjDeal();
    }

    void RefreshBjHud()
    {
        if (_bj == null) return;
        BlackjackHand h = _bj.Current;
        bool live = BjCanAct();

        var sb = new StringBuilder();
        sb.Append("<b>Blackjack · mësim</b>  dora " + Mathf.Min(_bj.HandIndex + 1, _bj.HandCount) + " nga " + _bj.HandCount + "\n");
        sb.Append("Lekë: " + _bj.Chips + "  ·  për ta kaluar " + BlackjackSession.PassChips + "+");
        _infoT.text = sb.ToString();

        var tips = new StringBuilder();
        if (h != null)
        {
            tips.Append("Totali yt: " + BlackjackHand.Total(h.Player) + "\n");
            tips.Append(h.HoleHidden ? "Krupieri tregon: " + Sq.Rank(h.Dealer[0].Rank) + "\n" : "Totali i krupierit: " + BlackjackHand.Total(h.Dealer) + "\n");
            if (_bjHint && live)
            {
                string hint = Sq.BjLessonHint(_bj.HandIndex);
                if (string.IsNullOrEmpty(hint)) hint = "Shiko dy letrat e para përpara se të vendosësh.";
                tips.Append("<color=#ffe066>" + hint + "</color>\n");
            }
        }
        _tipsT.text = tips.ToString();

        _bBjHit.B.interactable = live;
        _bBjStand.B.interactable = live;
        _bBjHint.B.interactable = live;
        bool finished = h != null && h.IsFinished;
        _bBjNext.B.interactable = !_bjBusy && (_bjSummary || finished);
        _bBjNext.T.text = _bjSummary ? "Luaj përsëri" : (_bj.HandIndex >= _bj.HandCount - 1 ? "Shiko rezultatin" : "Dora tjetër");
    }

    // =====================================================================================
    // free Blackjack: your own bet, doubling down, a 4-deck shoe, advice from basic strategy
    // =====================================================================================

    void StartBjFree()
    {
        StopAllCoroutines();
        ClearDynamic();
        SetCity(_map);
        _mode = Mode.BjFree;
        _level = null;
        _fp = null;
        _log.Clear();
        _banner = "";
        _bt = new BlackjackTable(NewSeed());
        _br = null;
        _bfHint = false;
        _bjBusy = false;
        BuildSeats(new string[] { "Ti", "Krupieri" });
        BjResetTable();
        BjSetPiles(0, _bt.Chips);
        AddLog("<b>Blackjack i lirë</b>: fillon me " + _bt.Chips + " " + Sq.Currency + ". Zgjidh bastin, pastaj ndaj letrat. " +
               "Çizme me " + BlackjackTable.Decks + " pako. Krupieri qëndron në të gjitha 17-at, Blackjack paguan 3:2.");
        RefreshHud();
    }

    void OnBfBet(int direction)
    {
        if (_mode != Mode.BjFree || _bt == null || _bjBusy || _bt.Current != null) return;
        _bt.AdjustBet(direction);
        RefreshHud();
    }

    void OnBfDeal()
    {
        if (_mode != Mode.BjFree || _bt == null || _bjBusy || !_bt.CanDeal) return;
        BjResetTable();
        _bfHint = false;
        _br = _bt.Deal();
        if (_bt.Reshuffled) AddLog("Çizmja u përzie.");
        StartCoroutine(BfDealRoutine());
    }

    void BfRefreshChips()
    {
        bool live = _br != null && !_br.IsFinished;
        int bet = live ? _br.Bet : 0;
        int stack = live ? _bt.Chips - bet : _bt.Chips;
        BjSetPiles(bet, stack);
    }

    IEnumerator BfDealRoutine()
    {
        _bjBusy = true;
        RefreshHud();
        AddLog("<b>Raundi " + (_bt.Rounds + 1) + "</b>  (basti " + _br.Bet + ")");
        BfRefreshChips();
        yield return StartCoroutine(BjDealFour(_br.Player, _br.Dealer));
        BjInfo(_br.Player, _br.Dealer, _br.HoleHidden);
        if (!_br.HoleHidden) yield return StartCoroutine(BjSync(_br.Player, _br.Dealer, _br.HoleHidden));
        yield return StartCoroutine(BfAfter());
    }

    IEnumerator BfAfter()
    {
        BjInfo(_br.Player, _br.Dealer, _br.HoleHidden);
        if (_br.IsFinished)
        {
            string msg = Sq.BjResult(_br.Outcome, _br.Net, BlackjackHand.Total(_br.Player), BlackjackHand.Total(_br.Dealer));
            int net = _br.Net;
            _bt.Finish();                       // books the result into the chips and the statistics
            AddLog("<b>" + msg + "</b>");
            if (_bt.IsBroke) msg += "\nTë mbaruan lekët. Fillo përsëri për një lojë të re.";
            _banner = msg;
            BjMoods(net);
            BfRefreshChips();
        }
        _bjBusy = false;
        RefreshHud();
        yield break;
    }

    bool BfCanAct()
    {
        return _mode == Mode.BjFree && _bt != null && _bt.Current != null && !_bt.Current.IsFinished && !_bjBusy;
    }

    void OnBfHit() { if (BfCanAct()) StartCoroutine(BfAct(0)); }
    void OnBfStand() { if (BfCanAct()) StartCoroutine(BfAct(1)); }
    void OnBfDouble() { if (BfCanAct() && _bt.Current.CanDouble) StartCoroutine(BfAct(2)); }

    IEnumerator BfAct(int kind)
    {
        _bjBusy = true;
        RefreshHud();
        if (kind == 0)
        {
            AddLog("Merr një letër.");
            _br.Hit();
        }
        else if (kind == 1)
        {
            AddLog("Qëndron me " + BlackjackHand.Total(_br.Player) + ".");
            _br.Stand();
        }
        else
        {
            AddLog("Dyfishon bastin në " + (_br.Bet * 2) + " dhe merr vetëm një letër.");
            _br.DoubleDown();
            BjSetPiles(_br.Bet, _bt.Chips - _br.Bet);        // the doubled bet is on the table
        }
        yield return StartCoroutine(BjSync(_br.Player, _br.Dealer, _br.HoleHidden));
        yield return StartCoroutine(BfAfter());
    }

    void RefreshBfHud()
    {
        if (_bt == null) return;
        bool broke = _bt.IsBroke;
        bool playing = _bt.Current != null;
        bool betting = !playing && !broke;
        bool live = BfCanAct();
        _gBfBet.SetActive(betting);
        _gBfPlay.SetActive(playing);
        _bBfRestart.Go.SetActive(broke);

        var sb = new StringBuilder();
        sb.Append("<b>Blackjack i lirë</b>\n");
        sb.Append("Lekë: <b>" + _bt.Chips + "</b>  ·  basti " + _bt.Bet + "\n");
        sb.Append("Fitore " + _bt.Wins + "  ·  humbje " + _bt.Losses);
        _infoT.text = sb.ToString();

        var tips = new StringBuilder();
        if (_br != null && playing)
        {
            tips.Append("Totali yt: " + BlackjackHand.Total(_br.Player) + (BlackjackRound.IsSoft(_br.Player) ? " (i butë)" : "") + "\n");
            tips.Append(_br.HoleHidden
                ? "Krupieri tregon: " + Sq.Rank(_br.Dealer[0].Rank) + "\n"
                : "Totali i krupierit: " + BlackjackHand.Total(_br.Dealer) + "\n");
            if (_bfHint && live)
            {
                BlackjackAdvice adv = _br.Suggest();
                tips.Append("<color=#ffe066>" + Sq.BjAdvice(adv, BlackjackHand.Total(_br.Player), BlackjackRound.IsSoft(_br.Player),
                            BlackjackRound.UpCardValue(_br.Dealer[0])) + "</color>\n");
            }
        }
        _tipsT.text = tips.ToString();

        _bfAmountT.text = _bt.Bet.ToString();
        _bBfMinus.B.interactable = betting && !_bjBusy && _bt.Bet > BlackjackTable.MinBet;
        _bBfPlus.B.interactable = betting && !_bjBusy && _bt.Bet < _bt.MaxAllowedBet;
        _bBfDeal.B.interactable = betting && !_bjBusy && _bt.CanDeal;
        _bBfHit.B.interactable = live;
        _bBfStand.B.interactable = live;
        _bBfDouble.B.interactable = live && _bt.Current.CanDouble;
        _bBfHint.B.interactable = live;
    }
}
