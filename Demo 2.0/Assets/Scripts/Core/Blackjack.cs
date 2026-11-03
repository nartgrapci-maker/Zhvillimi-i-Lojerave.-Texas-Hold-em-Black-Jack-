using System;
using System.Collections.Generic;

namespace PokerDemo.Core
{
    public enum BlackjackOutcome { InProgress, PlayerBlackjack, PlayerWin, Push, DealerWin, PlayerBust, DealerBlackjack }

    /// <summary>One scripted Blackjack hand. Dealer stands on all 17s; a natural pays 3:2.</summary>
    public class BlackjackHand
    {
        public readonly List<Card> Player;
        public readonly List<Card> Dealer;     // index 0 is the up card, index 1 the hidden card
        public readonly int Bet;
        readonly Queue<Card> _draw;

        public bool HoleHidden { get; private set; }
        public BlackjackOutcome Outcome { get; private set; }
        public int Net { get; private set; }
        public bool IsFinished { get { return Outcome != BlackjackOutcome.InProgress; } }

        public BlackjackHand(string player, string dealer, string draws, int bet)
        {
            Player = Card.ParseList(player);
            Dealer = Card.ParseList(dealer);
            var queue = Card.ParseList(draws);
            _draw = new Queue<Card>(queue);
            Bet = bet;
            HoleHidden = true;
            Outcome = BlackjackOutcome.InProgress;

            if (Player.Count != 2 || Dealer.Count != 2) throw new LevelConfigException("Blackjack hands need 2 player and 2 dealer cards.");
            var seen = new HashSet<Card>();
            var all = new List<Card>(Player);
            all.AddRange(Dealer);
            all.AddRange(queue);
            for (int i = 0; i < all.Count; i++)
                if (!seen.Add(all[i])) throw new LevelConfigException("Duplicate card " + all[i] + " in the scripted shoe.");

            bool playerNatural = Total(Player) == 21;
            bool dealerNatural = Total(Dealer) == 21;
            if (playerNatural || dealerNatural)
            {
                HoleHidden = false;
                if (playerNatural && dealerNatural) Finish(BlackjackOutcome.Push, 0);
                else if (playerNatural) Finish(BlackjackOutcome.PlayerBlackjack, Bet * 3 / 2);
                else Finish(BlackjackOutcome.DealerBlackjack, -Bet);
            }
        }

        public static int Total(IList<Card> cards)
        {
            int total = 0, aces = 0;
            for (int i = 0; i < cards.Count; i++)
            {
                int r = cards[i].Rank;
                if (r == 14) { total += 11; aces++; }
                else if (r >= 11) total += 10;
                else total += r;
            }
            while (total > 21 && aces > 0) { total -= 10; aces--; }
            return total;
        }

        public void Hit()
        {
            if (IsFinished) return;
            Player.Add(Draw());
            int t = Total(Player);
            if (t > 21) { HoleHidden = false; Finish(BlackjackOutcome.PlayerBust, -Bet); }
            else if (t == 21) Stand();
        }

        public void Stand()
        {
            if (IsFinished) return;
            HoleHidden = false;
            while (Total(Dealer) < 17) Dealer.Add(Draw());
            int p = Total(Player), d = Total(Dealer);
            if (d > 21 || p > d) Finish(BlackjackOutcome.PlayerWin, Bet);
            else if (p == d) Finish(BlackjackOutcome.Push, 0);
            else Finish(BlackjackOutcome.DealerWin, -Bet);
        }

        Card Draw()
        {
            if (_draw.Count == 0) throw new LevelConfigException("The scripted shoe ran out of cards (level defect).");
            return _draw.Dequeue();
        }

        void Finish(BlackjackOutcome outcome, int net) { Outcome = outcome; Net = net; }
    }

    /// <summary>The scripted 3-hand Blackjack mode (parallel track): perfect run = 135 chips, pass = at least 110.</summary>
    public class BlackjackSession
    {
        public const int StartChips = 100;
        public const int PassChips = 110;
        public const int BetSize = 10;

        static readonly string[][] Script = new string[][]
        {
            new string[] { "10s 6h", "10d 7c", "5c" },   // hit to 21 and win
            new string[] { "Kc 7s", "6d 10h", "9c" },    // stand: the dealer must draw and busts
            new string[] { "As Kh", "10c 7d", "" }       // natural Blackjack, pays 3:2
        };

        public int Chips = StartChips;
        public int HandIndex;      // 0..2, becomes 3 when the session is over
        public BlackjackHand Current;

        public int HandCount { get { return Script.Length; } }
        public bool IsOver { get { return HandIndex >= Script.Length; } }
        public bool Passed { get { return Chips >= PassChips; } }

        public static string HintFor(int handIndex)
        {
            switch (handIndex)
            {
                case 0: return "You have 16 and the dealer shows a 10. Hitting is risky, but the dealer will not fold: you must beat their total.";
                case 1: return "The dealer shows a 6 and must keep drawing below 17. When the dealer is likely to bust, do not risk your own 17.";
                default: return "";
            }
        }

        public void StartHand()
        {
            if (IsOver) return;
            string[] h = Script[HandIndex];
            Current = new BlackjackHand(h[0], h[1], h[2], BetSize);
        }

        /// <summary>Call after the current hand finished: books the result and moves to the next hand.</summary>
        public void FinishHand()
        {
            if (Current == null || !Current.IsFinished) return;
            Chips += Current.Net;
            HandIndex++;
            Current = null;
        }

        public void Restart()
        {
            Chips = StartChips;
            HandIndex = 0;
            Current = null;
        }
    }
}
