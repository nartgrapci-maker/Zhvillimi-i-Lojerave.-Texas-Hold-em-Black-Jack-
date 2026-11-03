using System;
using System.Collections.Generic;

namespace PokerDemo.Core
{
    public enum Phase { Preflop = 0, Flop = 1, Turn = 2, River = 3, Complete = 4 }
    public enum ActionType { Fold, Check, Call, Bet }
    public enum BotStyle { None, Passive, Level2Rule, Tight, Loose, Aggressive }

    /// <summary>A player action. For Bet, Total is the seat's total bet on this street (raise-to).</summary>
    public struct PlayerAction
    {
        public ActionType Type;
        public int Total;

        public static PlayerAction Fold() { return new PlayerAction { Type = ActionType.Fold }; }
        public static PlayerAction Check() { return new PlayerAction { Type = ActionType.Check }; }
        public static PlayerAction Call() { return new PlayerAction { Type = ActionType.Call }; }
        public static PlayerAction BetTo(int total) { return new PlayerAction { Type = ActionType.Bet, Total = total }; }
    }

    public class Seat
    {
        public string Name;
        public bool IsHuman;
        public BotStyle Style;
        public BotPersonality Persona;   // free-play bots (null for scripted level bots)
        public int StartStack;
        public int Stack;
        public Card[] Hole;
        public bool Folded;
        public bool AllIn;
        public int StreetBet;   // chips put in on the current street
        public int TotalIn;     // chips put in during the whole hand
    }

    public class HandResult
    {
        public bool ByFold;          // true when everybody else folded (no showdown)
        public int[] Won;            // chips won per seat (gross)
        public int[] Net;            // won minus put in, per seat
        public string[] HandNames;   // showdown hand names (null for folded seats / no showdown)
    }

    /// <summary>
    /// One scripted no-limit Texas Hold'em hand. The board and hole cards are fixed by the level,
    /// so every solution on a level card can be replayed and checked.
    /// House rules (document them in the GDD): the human (seat 0) acts first on every post-flop street;
    /// preflop order starts at a configured seat; a raise re-opens the betting for everybody.
    /// </summary>
    public class HoldemHand
    {
        public readonly List<Seat> Seats = new List<Seat>();
        public readonly List<string> Events = new List<string>();
        public readonly int SmallBlind;
        public readonly int BigBlind;

        readonly Card[] _board;
        readonly bool[] _needsAct;

        public Phase Street { get; private set; }
        public int Actor { get; private set; }
        public int CurrentBet { get; private set; }
        public int MinRaise { get; private set; }
        public HandResult Result { get; private set; }

        public bool IsComplete { get { return Street == Phase.Complete; } }
        public Card[] Board { get { return (Card[])_board.Clone(); } }

        public int BoardCount
        {
            get
            {
                switch (Street)
                {
                    case Phase.Preflop: return 0;
                    case Phase.Flop: return 3;
                    case Phase.Turn: return 4;
                    default: return 5;
                }
            }
        }

        public List<Card> VisibleBoard
        {
            get
            {
                var list = new List<Card>();
                for (int i = 0; i < BoardCount; i++) list.Add(_board[i]);
                return list;
            }
        }

        public int Pot
        {
            get
            {
                int p = 0;
                for (int i = 0; i < Seats.Count; i++) p += Seats[i].TotalIn;
                return p;
            }
        }

        public HoldemHand(IList<Seat> seats, string boardText, int smallBlindSeat, int bigBlindSeat,
                          int smallBlind, int bigBlind, int preflopFirstSeat)
        {
            if (seats == null || seats.Count < 2 || seats.Count > 6)
                throw new LevelConfigException("A hand needs 2 to 6 seats.");
            for (int i = 0; i < seats.Count; i++) Seats.Add(seats[i]);

            List<Card> board = Card.ParseList(boardText);
            if (board.Count != 5) throw new LevelConfigException("The scripted board needs exactly 5 cards.");
            _board = board.ToArray();

            var seen = new HashSet<Card>();
            for (int i = 0; i < _board.Length; i++)
                if (!seen.Add(_board[i])) throw new LevelConfigException("Duplicate card " + _board[i] + " in the scripted deck.");
            for (int i = 0; i < Seats.Count; i++)
            {
                Seat s = Seats[i];
                if (s.Hole == null || s.Hole.Length != 2) throw new LevelConfigException(s.Name + " needs exactly 2 hole cards.");
                if (s.Stack <= 0) throw new LevelConfigException(s.Name + " needs a positive stack.");
                s.StartStack = s.Stack;
                for (int j = 0; j < 2; j++)
                    if (!seen.Add(s.Hole[j])) throw new LevelConfigException("Duplicate card " + s.Hole[j] + " in the scripted deck.");
            }
            if (smallBlindSeat < 0 || smallBlindSeat >= Seats.Count || bigBlindSeat < 0 || bigBlindSeat >= Seats.Count || smallBlindSeat == bigBlindSeat)
                throw new LevelConfigException("Blind seats are invalid.");
            if (preflopFirstSeat < 0 || preflopFirstSeat >= Seats.Count)
                throw new LevelConfigException("Preflop first seat is invalid.");

            SmallBlind = smallBlind;
            BigBlind = bigBlind;
            _needsAct = new bool[Seats.Count];
            Street = Phase.Preflop;
            MinRaise = bigBlind;

            Post(smallBlindSeat, smallBlind);
            Post(bigBlindSeat, bigBlind);
            CurrentBet = bigBlind;
            Events.Add("Blinds: " + Seats[smallBlindSeat].Name + " " + smallBlind + ", " + Seats[bigBlindSeat].Name + " " + bigBlind + ".");

            StartRound(preflopFirstSeat);
        }

        // ---------------------------------------------------------------- queries

        public int ToCall(int seat)
        {
            Seat s = Seats[seat];
            return Math.Min(CurrentBet - s.StreetBet, s.Stack);
        }

        /// <summary>Hand strength category (0..8) of the seat using only the cards visible now.</summary>
        public int MadeCategory(int seat)
        {
            Seat s = Seats[seat];
            if (BoardCount < 3) return s.Hole[0].Rank == s.Hole[1].Rank ? 1 : 0;
            var list = new List<Card>(s.Hole);
            list.AddRange(VisibleBoard);
            return HandEvaluator.Evaluate(list) >> 20;
        }

        // ---------------------------------------------------------------- actions

        /// <summary>Returns null when the action is legal, otherwise the message to show the player.</summary>
        public string Validate(PlayerAction a)
        {
            if (IsComplete || Actor < 0) return "The hand is over.";
            Seat s = Seats[Actor];
            switch (a.Type)
            {
                case ActionType.Fold:
                    return null;
                case ActionType.Check:
                    if (CurrentBet != s.StreetBet) return "You cannot check: there is a bet of " + ToCall(Actor) + " to call.";
                    return null;
                case ActionType.Call:
                    if (ToCall(Actor) <= 0) return "There is nothing to call. Use Check.";
                    return null;
                default:
                    int max = s.StreetBet + s.Stack;
                    if (a.Total <= CurrentBet) return "A bet must be larger than the current bet of " + CurrentBet + ".";
                    if (a.Total > max) return "You only have " + s.Stack + " chips.";
                    if (a.Total < CurrentBet + MinRaise && a.Total != max)
                    {
                        if (CurrentBet == 0) return "Minimum bet is " + MinRaise + ".";
                        return "Minimum raise is " + MinRaise + " (to a total of " + (CurrentBet + MinRaise) + ").";
                    }
                    return null;
            }
        }

        public void Apply(PlayerAction a)
        {
            string error = Validate(a);
            if (error != null) throw new InvalidOperationException(error);

            int who = Actor;
            Seat s = Seats[who];
            switch (a.Type)
            {
                case ActionType.Fold:
                    s.Folded = true;
                    Events.Add(s.Name + ": Fold");
                    break;
                case ActionType.Check:
                    Events.Add(s.Name + ": Check");
                    break;
                case ActionType.Call:
                    int amt = ToCall(who);
                    Post(who, amt);
                    Events.Add(s.Name + ": Call " + amt + (s.AllIn ? " (all-in)" : ""));
                    break;
                default:
                    bool opening = CurrentBet == 0;
                    Post(who, a.Total - s.StreetBet);
                    if (a.Total - CurrentBet >= MinRaise) MinRaise = a.Total - CurrentBet;
                    CurrentBet = a.Total;
                    for (int i = 0; i < Seats.Count; i++)
                        _needsAct[i] = i != who && !Seats[i].Folded && !Seats[i].AllIn;
                    Events.Add(s.Name + ": " + (opening ? "Bet " : "Raise to ") + a.Total + (s.AllIn ? " (all-in)" : ""));
                    break;
            }
            _needsAct[who] = false;

            int alive = 0;
            for (int i = 0; i < Seats.Count; i++) if (!Seats[i].Folded) alive++;
            if (alive == 1) { Settle(false); return; }

            int next = NextActor(who);
            if (next >= 0) Actor = next;
            else AdvanceStreet();
        }

        // ---------------------------------------------------------------- internals

        void Post(int seat, int amount)
        {
            Seat s = Seats[seat];
            int a = Math.Min(amount, s.Stack);
            s.Stack -= a;
            s.StreetBet += a;
            s.TotalIn += a;
            if (s.Stack == 0) s.AllIn = true;
        }

        int NextActor(int start)
        {
            int n = Seats.Count;
            for (int k = 1; k <= n; k++)
            {
                int j = (start + k) % n;
                if (_needsAct[j]) return j;
            }
            return -1;
        }

        int CanActCount()
        {
            int c = 0;
            for (int i = 0; i < Seats.Count; i++) if (!Seats[i].Folded && !Seats[i].AllIn) c++;
            return c;
        }

        void StartRound(int first)
        {
            for (int i = 0; i < Seats.Count; i++) _needsAct[i] = !Seats[i].Folded && !Seats[i].AllIn;
            if (CanActCount() < 2)
            {
                for (int i = 0; i < Seats.Count; i++) _needsAct[i] = false;
                AdvanceStreet();
                return;
            }
            Actor = _needsAct[first] ? first : NextActor(first);
        }

        void AdvanceStreet()
        {
            while (true)
            {
                if (Street == Phase.River) { Settle(true); return; }
                Street = (Phase)((int)Street + 1);
                for (int i = 0; i < Seats.Count; i++) Seats[i].StreetBet = 0;
                CurrentBet = 0;
                MinRaise = BigBlind;

                string label = Street == Phase.Flop ? "Flop" : Street == Phase.Turn ? "Turn" : "River";
                var shown = new List<string>();
                int firstShown = Street == Phase.Flop ? 0 : BoardCount - 1;
                for (int i = firstShown; i < BoardCount; i++) shown.Add(_board[i].ToString());
                Events.Add(label + ": " + string.Join(" ", shown.ToArray()) + "   (pot " + Pot + ")");

                if (CanActCount() >= 2)
                {
                    StartRound(0);
                    return;
                }
                for (int i = 0; i < Seats.Count; i++) _needsAct[i] = false;
            }
        }

        void Settle(bool showdown)
        {
            int n = Seats.Count;
            var won = new int[n];
            var scores = new int[n];
            var names = new string[n];
            for (int i = 0; i < n; i++)
            {
                scores[i] = -1;
                if (showdown && !Seats[i].Folded)
                {
                    var all = new List<Card>(Seats[i].Hole);
                    all.AddRange(_board);
                    scores[i] = HandEvaluator.Evaluate(all);
                    names[i] = HandEvaluator.Describe(scores[i]);
                }
            }

            // side pots from the distinct contribution levels
            var levels = new List<int>();
            for (int i = 0; i < n; i++)
                if (Seats[i].TotalIn > 0 && !levels.Contains(Seats[i].TotalIn)) levels.Add(Seats[i].TotalIn);
            levels.Sort();

            int prev = 0;
            for (int li = 0; li < levels.Count; li++)
            {
                int level = levels[li];
                int amount = 0;
                for (int i = 0; i < n; i++)
                    amount += Math.Min(Seats[i].TotalIn, level) - Math.Min(Seats[i].TotalIn, prev);

                var eligible = new List<int>();
                for (int i = 0; i < n; i++)
                    if (!Seats[i].Folded && Seats[i].TotalIn >= level) eligible.Add(i);
                if (eligible.Count == 0)
                    for (int i = 0; i < n; i++) if (!Seats[i].Folded) eligible.Add(i);

                var winners = new List<int>();
                if (showdown && eligible.Count > 1)
                {
                    int best = -1;
                    for (int e = 0; e < eligible.Count; e++) best = Math.Max(best, scores[eligible[e]]);
                    for (int e = 0; e < eligible.Count; e++) if (scores[eligible[e]] == best) winners.Add(eligible[e]);
                }
                else winners = eligible;

                int share = amount / winners.Count;
                for (int w = 0; w < winners.Count; w++) won[winners[w]] += share;
                won[winners[0]] += amount % winners.Count;
                prev = level;
            }

            var net = new int[n];
            for (int i = 0; i < n; i++)
            {
                Seats[i].Stack += won[i];
                net[i] = won[i] - Seats[i].TotalIn;
            }

            Street = Phase.Complete;
            Actor = -1;
            Result = new HandResult { ByFold = !showdown, Won = won, Net = net, HandNames = names };
            Events.Add(showdown ? "Showdown." : "Hand ends: everybody else folded.");
        }
    }
}
