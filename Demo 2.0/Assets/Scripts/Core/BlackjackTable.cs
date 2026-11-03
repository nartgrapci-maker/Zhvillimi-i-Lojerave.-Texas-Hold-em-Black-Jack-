using System;
using System.Collections.Generic;

namespace PokerDemo.Core
{
    /// <summary>Several decks shuffled together. Reshuffled between rounds when it runs low.</summary>
    public class Shoe
    {
        readonly Random _rng;
        readonly int _decks;
        readonly List<Card> _cards = new List<Card>();
        int _next;

        public Shoe(Random rng, int decks)
        {
            _rng = rng;
            _decks = decks;
            Shuffle();
        }

        public int Remaining { get { return _cards.Count - _next; } }
        public bool NeedsShuffle { get { return Remaining < 20; } }

        public void Shuffle()
        {
            _cards.Clear();
            for (int d = 0; d < _decks; d++)
                for (int rank = 2; rank <= 14; rank++)
                    for (int suit = 0; suit < 4; suit++)
                        _cards.Add(new Card(rank, (CardSuit)suit));
            for (int i = _cards.Count - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                Card t = _cards[i];
                _cards[i] = _cards[j];
                _cards[j] = t;
            }
            _next = 0;
        }

        public Card Draw()
        {
            if (_next >= _cards.Count) Shuffle();     // safety net; normally reshuffled between rounds
            return _cards[_next++];
        }
    }

    public enum BlackjackAdvice { Hit, Stand, Double }

    /// <summary>
    /// One hand of free Blackjack against the dealer: Hit, Stand or Double down.
    /// The dealer stands on all 17s. A natural pays 3:2 (bets are multiples of 10, so it is exact).
    /// </summary>
    public class BlackjackRound
    {
        public readonly List<Card> Player = new List<Card>();
        public readonly List<Card> Dealer = new List<Card>();    // index 0 is the up card, index 1 the hidden card
        public int Bet { get; private set; }
        public bool Doubled { get; private set; }
        public bool AllowDouble;                                  // set by the table when the player can afford it
        public bool HoleHidden { get; private set; }
        public BlackjackOutcome Outcome { get; private set; }
        public int Net { get; private set; }
        public bool IsFinished { get { return Outcome != BlackjackOutcome.InProgress; } }

        readonly Shoe _shoe;

        public BlackjackRound(Shoe shoe, int bet)
        {
            _shoe = shoe;
            Bet = bet;
            HoleHidden = true;
            Outcome = BlackjackOutcome.InProgress;
            Player.Add(shoe.Draw());
            Dealer.Add(shoe.Draw());
            Player.Add(shoe.Draw());
            Dealer.Add(shoe.Draw());

            bool playerNatural = BlackjackHand.Total(Player) == 21;
            bool dealerNatural = BlackjackHand.Total(Dealer) == 21;
            if (playerNatural || dealerNatural)
            {
                HoleHidden = false;
                if (playerNatural && dealerNatural) Finish(BlackjackOutcome.Push, 0);
                else if (playerNatural) Finish(BlackjackOutcome.PlayerBlackjack, Bet * 3 / 2);
                else Finish(BlackjackOutcome.DealerBlackjack, -Bet);
            }
        }

        public bool CanDouble { get { return !IsFinished && AllowDouble && !Doubled && Player.Count == 2; } }

        public void Hit()
        {
            if (IsFinished) return;
            Player.Add(_shoe.Draw());
            int t = BlackjackHand.Total(Player);
            if (t > 21) { HoleHidden = false; Finish(BlackjackOutcome.PlayerBust, -Bet); }
            else if (t == 21) Stand();
        }

        public void Stand()
        {
            if (IsFinished) return;
            HoleHidden = false;
            while (BlackjackHand.Total(Dealer) < 17) Dealer.Add(_shoe.Draw());
            int p = BlackjackHand.Total(Player), d = BlackjackHand.Total(Dealer);
            if (d > 21 || p > d) Finish(BlackjackOutcome.PlayerWin, Bet);
            else if (p == d) Finish(BlackjackOutcome.Push, 0);
            else Finish(BlackjackOutcome.DealerWin, -Bet);
        }

        /// <summary>Doubles the bet, takes exactly one more card and stands. Returns false when it is not allowed.</summary>
        public bool DoubleDown()
        {
            if (!CanDouble) return false;
            Doubled = true;
            Bet *= 2;
            Player.Add(_shoe.Draw());
            if (BlackjackHand.Total(Player) > 21) { HoleHidden = false; Finish(BlackjackOutcome.PlayerBust, -Bet); }
            else Stand();
            return true;
        }

        void Finish(BlackjackOutcome outcome, int net) { Outcome = outcome; Net = net; }

        // ---------------------------------------------------------------------------------------
        // Basic strategy, used by the Hint button. Total and "soft" (an Ace counted as 11) come from the player's cards.
        // ---------------------------------------------------------------------------------------

        public static bool IsSoft(IList<Card> cards)
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
            return aces > 0;
        }

        public static int UpCardValue(Card up)
        {
            if (up.Rank == 14) return 11;
            return up.Rank >= 11 ? 10 : up.Rank;
        }

        public BlackjackAdvice Suggest()
        {
            int total = BlackjackHand.Total(Player);
            int up = UpCardValue(Dealer[0]);
            bool soft = IsSoft(Player);
            bool dbl = CanDouble;

            if (soft)
            {
                if (total >= 19) return BlackjackAdvice.Stand;
                if (total == 18)
                {
                    if (up >= 3 && up <= 6) return dbl ? BlackjackAdvice.Double : BlackjackAdvice.Stand;
                    return up >= 9 ? BlackjackAdvice.Hit : BlackjackAdvice.Stand;
                }
                if (dbl && up >= 4 && up <= 6 && total >= 13) return BlackjackAdvice.Double;
                return BlackjackAdvice.Hit;
            }
            if (total >= 17) return BlackjackAdvice.Stand;
            if (total >= 13) return up <= 6 ? BlackjackAdvice.Stand : BlackjackAdvice.Hit;
            if (total == 12) return up >= 4 && up <= 6 ? BlackjackAdvice.Stand : BlackjackAdvice.Hit;
            if (total == 11) return dbl && up <= 10 ? BlackjackAdvice.Double : BlackjackAdvice.Hit;
            if (total == 10) return dbl && up <= 9 ? BlackjackAdvice.Double : BlackjackAdvice.Hit;
            if (total == 9) return dbl && up >= 3 && up <= 6 ? BlackjackAdvice.Double : BlackjackAdvice.Hit;
            return BlackjackAdvice.Hit;
        }
    }

    /// <summary>Free Blackjack: a chip bankroll, a chosen bet, a 4-deck shoe, one round at a time.</summary>
    public class BlackjackTable
    {
        public const int StartChips = 200;
        public const int MinBet = 10;
        public const int MaxBet = 100;
        public const int BetStep = 10;
        public const int Decks = 4;

        public readonly Shoe Shoe;
        public int Chips = StartChips;
        public int Bet = MinBet;
        public int Peak = StartChips;
        public int Rounds, Wins, Pushes, Losses, Blackjacks;
        public bool Reshuffled;                    // true when the last Deal() started a fresh shoe
        public BlackjackRound Current;

        public BlackjackTable(int seed)
        {
            Shoe = new Shoe(new Random(seed), Decks);
        }

        /// <summary>The highest bet allowed now: at most MaxBet and never more than the chips left (in steps of 10).</summary>
        public int MaxAllowedBet { get { return Math.Max(MinBet, Math.Min(MaxBet, Chips / BetStep * BetStep)); } }
        public bool IsBroke { get { return Current == null && Chips < MinBet; } }
        public bool CanDeal { get { return Current == null && Chips >= MinBet; } }

        public void AdjustBet(int direction)
        {
            Bet = Math.Max(MinBet, Math.Min(MaxAllowedBet, Bet + direction * BetStep));
        }

        public void SetBet(int bet)
        {
            Bet = Math.Max(MinBet, Math.Min(MaxAllowedBet, bet / BetStep * BetStep));
        }

        public BlackjackRound Deal()
        {
            if (!CanDeal) return null;
            Reshuffled = false;
            if (Shoe.NeedsShuffle) { Shoe.Shuffle(); Reshuffled = true; }
            if (Bet > MaxAllowedBet) Bet = MaxAllowedBet;
            Current = new BlackjackRound(Shoe, Bet) { AllowDouble = Chips >= 2 * Bet };
            return Current;
        }

        /// <summary>Books the result of the finished round into the bankroll and statistics.</summary>
        public void Finish()
        {
            if (Current == null || !Current.IsFinished) return;
            Chips += Current.Net;
            Rounds++;
            if (Current.Outcome == BlackjackOutcome.PlayerBlackjack) Blackjacks++;
            if (Current.Net > 0) Wins++;
            else if (Current.Net < 0) Losses++;
            else Pushes++;
            if (Chips > Peak) Peak = Chips;
            Current = null;
            if (Bet > MaxAllowedBet) Bet = MaxAllowedBet;
        }
    }
}
