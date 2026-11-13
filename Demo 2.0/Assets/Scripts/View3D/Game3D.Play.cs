using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using PokerDemo.Core;

public partial class Game3D
{
    GameObject _tableRoot;      // cards, chips, labels of the current hand (destroyed every hand)
    bool _tableSettled;         // false while a finished hand is still shown before the pot is paid out

    int _stage;                 // tournament: 0 Shkodër .. 3 Tiranë
    bool _cardUp;               // the city card is shown before the first hand of a stage
    readonly System.Random _eqRng = new System.Random(77);
    double _eq;
    int _eqKey = -1;

    /// <summary>What the player did in this free game or tournament stage.</summary>
    class Stats
    {
        public int Hands, Won, Biggest, Hints, BestScore = -1;
    }
    Stats _stats = new Stats();

    // =====================================================================================
    // starting a game
    // =====================================================================================

    void StartLevel(int index)
    {
        if (index < 0 || index >= Levels.All.Length) return;
        StopAllCoroutines();
        ClearDynamic();
        SetCity(_map);
        _mode = Mode.Level;
        _levelIndex = index;
        _level = Levels.All[index];
        _fp = null;
        _log.Clear();
        _banner = "";
        _configError = null;
        _hintOn = false;
        try
        {
            _hand = _level.CreateHand();
        }
        catch (LevelConfigException e)
        {
            _hand = null;
            _configError = e.Message;
            RefreshHud();
            return;
        }

        var names = new string[_level.Names.Length];
        _seatSlot = new int[names.Length];
        for (int i = 0; i < names.Length; i++)
        {
            names[i] = Sq.Name(ShortName(_level.Names[i]));
            _seatSlot[i] = i;
        }
        BuildSeats(names);
        AddLog("<b>Niveli " + _level.Number + ": " + Sq.Level(_level.Number).Title + "</b>");
        BeginHand();
    }

    string[] SessionNames()
    {
        var names = new string[_fp.Players.Count];
        for (int i = 0; i < names.Length; i++) names[i] = Sq.Name(_fp.Players[i].Name);
        return names;
    }

    void StartFreePlay()
    {
        StopAllCoroutines();
        ClearDynamic();
        SetCity(_map);
        _mode = Mode.FreePlay;
        _level = null;
        _log.Clear();
        _banner = "";
        _configError = null;
        _stats = new Stats();
        _fp = new FreePlaySession(NewSeed(), new string[] { "Agim", "Elira", "Kujtim" }, null, FreePlaySession.StartStack);
        BuildSeats(SessionNames());
        AddLog("Loja e lirë: të gjithë fillojnë me " + FreePlaySession.StartStack + " " + Sq.Currency + ". Bllindat rriten çdo " +
               FreePlaySession.HandsPerBlindLevel + " dora. Çdo bot ka stilin e vet dhe ndryshon humor çdo dorë.");
        NextFreePlayHand();
    }

    void StartTournament() { StartStage(0); }

    /// <summary>One city of the tournament: a new table, three named opponents, and a city card before the first hand.</summary>
    void StartStage(int stage)
    {
        StopAllCoroutines();
        ClearDynamic();
        _mode = Mode.Tournament;
        _stage = Mathf.Clamp(stage, 0, Tournament.Stages.Length - 1);
        SetCity(_stage - 1);               // level 0 is the oda (map -1), then the cities clockwise round the wheel
        _level = null;
        _log.Clear();
        _banner = "";
        _configError = null;
        _stats = new Stats();
        _fp = Tournament.StartStage(_stage, NewSeed());
        BuildSeats(SessionNames());
        _cardUp = true;
        Play(Sfx.Turn, 0.5f);
        RefreshHud();
    }

    void NextFreePlayHand()
    {
        if (_fp == null || _fp.IsOver) return;
        _hand = _fp.NextHand();
        _seatSlot = (int[])_fp.ActivePlayers.Clone();
        AddLog("<b>Dora " + _fp.HandNumber + "</b>  (bllindat " + _fp.SmallBlind + "/" + _fp.BigBlind + ")");
        BeginHand();
    }

    // =====================================================================================
    // one hand: setup, dealing, the action loop
    // =====================================================================================

    void BeginHand()
    {
        StopAllCoroutines();
        if (_tableRoot != null) Destroy(_tableRoot);
        _tableRoot = new GameObject("Table objects");
        _tableRoot.transform.SetParent(_dyn.transform, false);

        int n = _angles.Length;
        for (int i = 0; i < n; i++)
        {
            _hole[i].Clear();
            _betPile[i] = null;
            _stackPile[i] = null;
            _betShown[i] = -1;
            _stackShown[i] = -1;
        }
        _board.Clear();
        _potPile = null;
        _potShown = -1;
        _billboards.Clear();
        _eventsShown = 0;
        _boardShown = 0;
        _handOver = false;
        _tableSettled = false;
        _revealOn = false;
        _revealHidden = false;
        _hintOn = false;
        _eqKey = -1;
        _betAmount = 0;
        _banner = "";

        _potText = Mats.Text(_tableRoot.transform, "", 0.22f, new Color(1f, 0.92f, 0.5f), PotPos + new Vector3(0f, 0.3f, -0.7f));
        _billboards.Add(_potText.transform);

        BuildDealerButton();

        for (int slot = 0; slot < n; slot++)
        {
            AvatarView av = _avatars[slot];
            av.SetTurn(false);
            av.Say("", 0f);
            av.SetMood(SeatOfSlot(slot) >= 0 ? AvatarView.Mood.Idle : AvatarView.Mood.Out);
        }
        StartCoroutine(DealRoutine());
    }

    void BuildDealerButton()
    {
        _dealerButton = null;
        if (_fp == null || _fp.DealerPlayer < 0) return;
        int slot = _fp.DealerPlayer;
        Vector3 pos = DealerPos(slot);
        var btn = Prim.Make(PrimitiveType.Cylinder, _tableRoot.transform, pos, new Vector3(0.32f, 0.018f, 0.32f), Mats.Lit(Theme.Cream, 0.5f), "DealerButton");
        TextMesh d = Mats.Text(btn.transform.parent, "D", 0.2f, Theme.Black, pos + new Vector3(0f, 0.025f, 0f));
        d.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        _dealerButton = btn;
    }

    int SeatOfSlot(int slot)
    {
        for (int i = 0; i < _seatSlot.Length; i++) if (_seatSlot[i] == slot) return i;
        return -1;
    }

    AvatarView AvatarAt(int seat)
    {
        if (seat < 0 || seat >= _seatSlot.Length) return null;
        int slot = _seatSlot[seat];
        return slot >= 0 && slot < _avatars.Count ? _avatars[slot] : null;
    }

    Vector3 HolePos(int slot, int k)
    {
        float a = _angles[slot] * Mathf.Deg2Rad;
        var tangent = new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a));
        return Polar(_angles[slot], CardR, TableTop + 0.03f) + tangent * ((k - 0.5f) * 0.56f);
    }

    CardView MakeHoleCard(int seat, int k)
    {
        int slot = _seatSlot[seat];
        Seat s = _hand.Seats[seat];
        float yaw = YawToCenter(_angles[slot]);
        CardView cv = CardView.Create(_tableRoot.transform, s.Hole[k], DeckPos + Vector3.up * 0.02f, CardView.FaceDown(0f));
        cv.Tween.Go(HolePos(slot, k), s.IsHuman ? CardView.FaceUp(yaw) : CardView.FaceDown(yaw), 0.45f, 0f, 0.5f);
        _hole[slot].Add(cv);
        Play(Sfx.Card, 0.7f);
        return cv;
    }

    IEnumerator DealRoutine()
    {
        _botsActing = true;
        UpdateAvatars();
        RefreshHud();
        yield return Wait(0.35f);

        int seats = _hand.Seats.Count;
        for (int round = 0; round < 2; round++)
        {
            for (int s = 0; s < seats; s++)
            {
                MakeHoleCard(s, round);
                yield return Wait(0.13f);
            }
        }
        yield return Wait(0.35f);
        SyncEvents();
        RefreshChips();
        Play(Sfx.Chip, 0.7f);
        yield return StartCoroutine(Proceed());
    }

    void SyncEvents(bool force = false)
    {
        if (_hand == null) return;
        while (_eventsShown < _hand.Events.Count)
        {
            string e = _hand.Events[_eventsShown];
            // do not announce a street in the log before its cards are on the table
            int needed = e.StartsWith("Flop:") ? 3 : e.StartsWith("Turn:") ? 4 : e.StartsWith("River:") ? 5 : 0;
            if (!force && needed > _boardShown) break;
            AddLog(Sq.Event(e));
            _eventsShown++;
        }
    }

    void ShowBoardTo(int count, float stagger)
    {
        int first = _boardShown;
        for (int i = first; i < count && i < 5; i++)
        {
            Card c = _hand.Board[i];
            CardView cv = CardView.Create(_tableRoot.transform, c, DeckPos + Vector3.up * 0.02f, CardView.FaceDown(0f));
            cv.Tween.Go(BoardPos(i), CardView.FaceUp(0f), 0.5f, (i - first) * stagger, 0.6f);
            _board.Add(cv);
            Play(Sfx.Card, 0.8f);
        }
        if (count > _boardShown) _boardShown = Mathf.Min(count, 5);
    }

    /// <summary>The action loop: show what happened, then either wait for the human or let the next bot act.</summary>
    IEnumerator Proceed()
    {
        while (true)
        {
            ShowBoardTo(_hand.IsComplete ? _boardShown : _hand.BoardCount, 0.22f);
            SyncEvents();
            RefreshChips();
            UpdateAvatars();

            if (_hand.IsComplete)
            {
                yield return StartCoroutine(FinishRoutine());
                yield break;
            }

            int actor = _hand.Actor;
            Seat seat = _hand.Seats[actor];
            AvatarView av = AvatarAt(actor);
            if (seat.IsHuman)
            {
                _botsActing = false;
                _betAmount = 0;
                if (av != null) av.SetTurn(true);
                Play(Sfx.Turn, 0.5f);
                RefreshHud();
                yield break;
            }

            _botsActing = true;
            RefreshHud();
            if (av != null)
            {
                av.SetTurn(true);
                av.SetMood(AvatarView.Mood.Thinking);
            }
            yield return Wait(0.7f + (float)_fxRng.NextDouble() * 0.9f);
            DoAction(BotDecision(actor));
            yield return Wait(0.5f);
        }
    }

    PlayerAction BotDecision(int actor)
    {
        PlayerAction a;
        try
        {
            a = _mode == Mode.Level ? BotPolicy.Decide(_hand, actor) : SmartBot.Decide(_hand, actor, _fp.Rng);
        }
        catch (System.Exception ex)
        {
            AddLog("<color=#ff6b6b>Gabim i botit: " + ex.Message + "</color>");
            a = _hand.ToCall(actor) > 0 ? PlayerAction.Call() : PlayerAction.Check();
        }
        if (_hand.Validate(a) != null) a = _hand.ToCall(actor) > 0 ? PlayerAction.Call() : PlayerAction.Check();
        return a;
    }

    /// <summary>Applies an already validated action for the current actor and shows it on the table.</summary>
    void DoAction(PlayerAction a)
    {
        int actor = _hand.Actor;
        Seat s = _hand.Seats[actor];
        AvatarView av = AvatarAt(actor);
        bool opening = _hand.CurrentBet == 0;
        int toCall = _hand.ToCall(actor);

        _hand.Apply(a);

        string say;
        switch (a.Type)
        {
            case ActionType.Fold: say = "Heq dorë"; break;
            case ActionType.Check: say = "Kalo"; break;
            case ActionType.Call: say = "Barazo " + toCall + (s.AllIn ? " (all-in)" : ""); break;
            default: say = (opening ? "Vë " : "Ngre në ") + a.Total + (s.AllIn ? "  ALL-IN!" : ""); break;
        }
        // the bots in free play and in the tournament sometimes add a line of talk
        if (!s.IsHuman && _mode != Mode.Level && _fxRng.NextDouble() < 0.30)
        {
            Sq.Taunt kind = a.Type == ActionType.Fold ? Sq.Taunt.Fold : (s.AllIn ? Sq.Taunt.AllIn : Sq.Taunt.Raise);
            if (a.Type == ActionType.Fold || a.Type == ActionType.Bet) say += "\n" + Sq.Say(kind, _fxRng);
        }
        if (av != null)
        {
            av.Say(say, 2.4f);
            av.SetTurn(false);
            av.SetMood(a.Type == ActionType.Fold ? AvatarView.Mood.Folded : AvatarView.Mood.Idle);
        }
        if (a.Type == ActionType.Fold)
        {
            TossCards(_seatSlot[actor]);
            Play(Sfx.Click, 0.6f);
        }
        else if (a.Type != ActionType.Check) Play(Sfx.Chip, 0.8f);
        else Play(Sfx.Click, 0.4f);

        SyncEvents();
        RefreshChips();
        UpdateAvatars();
        RefreshHud();
    }

    void TossCards(int slot)
    {
        for (int i = 0; i < _hole[slot].Count; i++)
        {
            CardView cv = _hole[slot][i];
            if (cv == null) continue;
            cv.Tween.Go(DeckPos + Vector3.up * 0.02f, CardView.FaceDown(0f), 0.4f, 0f, 0.3f);
            Destroy(cv.gameObject, 0.6f);
        }
        _hole[slot].Clear();
    }

    /// <summary>The human pressed a button.</summary>
    void Act(PlayerAction a)
    {
        if (_hand == null || _hand.IsComplete || _handOver || _botsActing) return;
        if (!_hand.Seats[_hand.Actor].IsHuman) return;
        string error = _hand.Validate(a);
        if (error != null)
        {
            _banner = Sq.Error(error);
            Play(Sfx.Lose, 0.4f);
            RefreshHud();
            return;
        }
        _banner = "";
        StartCoroutine(HumanAct(a));
    }

    IEnumerator HumanAct(PlayerAction a)
    {
        _botsActing = true;
        DoAction(a);
        yield return Wait(0.55f);
        yield return StartCoroutine(Proceed());
    }

    // =====================================================================================
    // chips and labels
    // =====================================================================================

    void SetPile(ref GameObject pile, ref int shown, int amount, Vector3 pos)
    {
        if (pile != null && shown == amount) return;
        if (pile != null) Destroy(pile);
        pile = null;
        shown = amount;
        if (amount > 0) pile = ChipPile.Build(_tableRoot.transform, amount, pos, true);
    }

    int DisplayStack(int seat)
    {
        int stack = _hand.Seats[seat].Stack;
        if (_hand.IsComplete && !_tableSettled && _hand.Result != null) stack -= _hand.Result.Won[seat];
        return stack;
    }

    void RefreshChips()
    {
        if (_hand == null || _tableRoot == null) return;
        bool done = _hand.IsComplete;
        int betSum = 0;
        for (int s = 0; s < _hand.Seats.Count; s++)
        {
            int slot = _seatSlot[s];
            int bet = done ? 0 : _hand.Seats[s].StreetBet;
            betSum += bet;
            SetPile(ref _betPile[slot], ref _betShown[slot], bet, BetPos(slot));
            SetPile(ref _stackPile[slot], ref _stackShown[slot], DisplayStack(s), StackPos(slot));
        }
        int pot = done ? (_tableSettled ? 0 : _hand.Pot) : _hand.Pot - betSum;
        SetPile(ref _potPile, ref _potShown, pot, PotPos);
        if (_potText != null) _potText.text = done && _tableSettled ? "" : "Poti " + _hand.Pot;
    }

    void UpdateAvatars()
    {
        if (_hand == null) return;
        for (int slot = 0; slot < _avatars.Count; slot++)
        {
            AvatarView av = _avatars[slot];
            int seat = SeatOfSlot(slot);
            if (seat < 0)
            {
                av.SetInfo("pa lekë");
                continue;
            }
            Seat s = _hand.Seats[seat];
            string info = s.Folded ? "hoqi dorë" : DisplayStack(seat) + " " + Sq.Currency;
            if (s.AllIn && !s.Folded) info += "  ALL-IN";
            if (_fp != null && _fp.DealerPlayer == slot) info += "  (shpërndan)";
            av.SetInfo(info);
            if (s.Folded && !_hand.IsComplete) av.SetMood(AvatarView.Mood.Folded);
        }
    }

    // =====================================================================================
    // end of a hand
    // =====================================================================================

    /// <summary>The pot flies to the winner's stack.</summary>
    IEnumerator PayoutRoutine(HandResult r)
    {
        if (_potPile != null) Destroy(_potPile);
        _potPile = null;
        _potShown = -1;
        if (_potText != null) _potText.text = "";
        var flying = new List<GameObject>();
        for (int s = 0; s < _hand.Seats.Count; s++)
        {
            if (r.Won[s] <= 0) continue;
            int slot = _seatSlot[s];
            GameObject fly = ChipPile.Build(_tableRoot.transform, r.Won[s], PotPos, false);
            fly.GetComponent<Tweener>().Go(StackPos(slot) + Vector3.up * 0.04f, Quaternion.identity, 0.8f, 0f, 0.7f);
            flying.Add(fly);
        }
        if (flying.Count > 0) Play(Sfx.Chip, 0.9f);
        yield return Wait(0.9f);
        for (int i = 0; i < flying.Count; i++) Destroy(flying[i]);
    }

    string StatsText()
    {
        var sb = new StringBuilder();
        sb.Append("Statistika: " + _stats.Hands + " dora, " + _stats.Won + " fitore");
        if (_stats.Biggest > 0) sb.Append(", fitorja më e madhe " + _stats.Biggest + " " + Sq.Currency);
        if (_stats.BestScore >= 0) sb.Append(", dora më e mirë: " + Sq.Describe(_stats.BestScore));
        sb.Append(", ndihma të përdorura: " + _stats.Hints + ".");
        return sb.ToString();
    }

    IEnumerator FinishRoutine()
    {
        _botsActing = true;
        for (int i = 0; i < _avatars.Count; i++) _avatars[i].SetTurn(false);
        RefreshHud();
        HandResult r = _hand.Result;
        yield return Wait(0.5f);

        if (!r.ByFold)
        {
            // run out the rest of the board, one card at a time
            while (_boardShown < 5)
            {
                ShowBoardTo(_boardShown + 1, 0f);
                SyncEvents();
                RefreshHud();
                yield return Wait(0.95f);
            }
            // turn the hole cards of everybody still in
            for (int s = 0; s < _hand.Seats.Count; s++)
            {
                Seat seat = _hand.Seats[s];
                if (seat.Folded) continue;
                List<CardView> cards = _hole[_seatSlot[s]];
                for (int k = 0; k < cards.Count; k++)
                {
                    if (cards[k] == null) continue;
                    cards[k].Tween.Go(cards[k].transform.position, CardView.FaceUp(0f), 0.5f, k * 0.15f, 0.35f);
                }
                AvatarView av = AvatarAt(s);
                string hn = Sq.HandName(_hand, s);
                if (av != null && hn != null) av.Say(hn, 4f);
            }
            Play(Sfx.Card, 0.8f);
            _revealOn = true;
            RefreshHud();
            yield return Wait(1.6f);
        }

        // pay out the pot
        SyncEvents(true);
        yield return StartCoroutine(PayoutRoutine(r));
        _tableSettled = true;
        RefreshChips();
        bool humanWon = r.Won[0] > 0 && r.Net[0] > 0;
        Play(humanWon ? Sfx.Win : Sfx.Lose, 0.9f);
        if (humanWon) Confetti.Burst(_tableRoot.transform, StackPos(_seatSlot[0]) + Vector3.up * 0.4f, 34, _fxRng);
        for (int s = 0; s < _hand.Seats.Count; s++)
        {
            AvatarView av = AvatarAt(s);
            if (av == null) continue;
            if (r.Won[s] > 0) av.SetMood(AvatarView.Mood.Win);
            else av.SetMood(_hand.Seats[s].Folded ? AvatarView.Mood.Folded : AvatarView.Mood.Lose);
            string bubble = r.Net[s] > 0 ? "+" + r.Net[s] : "";
            if (!_hand.Seats[s].IsHuman && _mode != Mode.Level && _fxRng.NextDouble() < 0.4)
            {
                if (r.Net[s] > 0) bubble += "\n" + Sq.Say(Sq.Taunt.Win, _fxRng);
                else if (!_hand.Seats[s].Folded) bubble = Sq.Say(Sq.Taunt.Lose, _fxRng);
            }
            if (bubble.Length > 0) av.Say(bubble, 3.5f);
        }
        UpdateAvatars();
        if (_fp != null) _fp.FinishHand();

        // statistics for free play and the tournament
        _stats.Hands++;
        if (humanWon)
        {
            _stats.Won++;
            if (r.Net[0] > _stats.Biggest) _stats.Biggest = r.Net[0];
        }
        if (!_hand.Seats[0].Folded && !r.ByFold)
        {
            var all = new List<Card>(_hand.Seats[0].Hole);
            all.AddRange(_hand.Board);
            int score = HandEvaluator.Evaluate(all);
            if (score > _stats.BestScore) _stats.BestScore = score;
        }

        string summary = Sq.HandSummary(_hand);
        AddLog("<b>" + summary + "</b>");
        AddLog(Sq.RevealOpponents(_hand));
        _banner = summary;

        if (_mode == Mode.Level)
        {
            bool passed = _level.IsPassed(_hand.Seats[0].Stack);
            string msg = Sq.PassMessage(_level, _hand);
            AddLog("<color=" + (passed ? "#7CFC9A" : "#ffb347") + "><b>" + msg + "</b></color>");
            _banner += "\n<color=" + (passed ? "#7CFC9A" : "#ffb347") + ">" + msg + "</color>";
            if (passed) Confetti.Burst(_tableRoot.transform, PotPos + Vector3.up * 1.0f, 40, _fxRng);
        }
        else if (_fp != null && _fp.IsOver)
        {
            string end;
            if (_mode == Mode.Tournament)
            {
                if (_fp.HumanIsOut) end = "Të mbaruan lekët në " + Sq.Cities[_stage].Name + ". Provo përsëri!";
                else if (_stage >= Tournament.Stages.Length - 1) end = "KAMPION I TROJEVE SHQIPTARE! I fitove të shtatë nivelet, nga oda deri në Ulqin.";
                else end = "Fitove në " + Sq.Cities[_stage].Name + "! Rruga vazhdon drejt " + Sq.Cities[_stage + 1].Name + ".";
            }
            else end = _fp.HumanIsOut ? "Të mbaruan lekët. Loja mbaroi." : "I nxore të tre kundërshtarët nga loja. Fitove lojën!";
            AddLog("<b>" + end + "</b>");
            AddLog(StatsText());
            for (int i = 1; i < _fp.Players.Count; i++)
                AddLog(Sq.Name(_fp.Players[i].Name) + " luajti si " + Sq.Style(_fp.Players[i].Persona.Label) + ".");
            _banner += "\n<b>" + end + "</b>";
            Play(_fp.HumanIsOut ? Sfx.Lose : Sfx.Win, 0.9f);
            if (!_fp.HumanIsOut)
            {
                bool champion = _mode == Mode.Tournament && _stage >= Tournament.Stages.Length - 1;
                Confetti.Burst(_tableRoot.transform, PotPos + Vector3.up * 1.2f, champion ? 90 : 50, _fxRng);
                if (champion)
                {
                    if (Music.Anthem != null && _musicOn) { _music.Stop(); Play(Music.Anthem, 0.9f); }     // Himni i Flamurit for the champion
                    Confetti.Burst(_tableRoot.transform, PotPos + new Vector3(-2f, 1.2f, 0.5f), 60, _fxRng);
                    Confetti.Burst(_tableRoot.transform, PotPos + new Vector3(2f, 1.2f, 0.5f), 60, _fxRng);
                }
            }
        }

        _handOver = true;
        _botsActing = false;
        RefreshHud();
    }
}
