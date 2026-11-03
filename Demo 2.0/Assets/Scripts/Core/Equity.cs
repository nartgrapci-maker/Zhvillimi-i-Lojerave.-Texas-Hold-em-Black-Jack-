using System;
using System.Collections.Generic;

namespace PokerDemo.Core
{
    /// <summary>
    /// Monte Carlo estimate of how often a seat wins the hand: the unseen cards are dealt at random many times
    /// (the rest of the board and a random hand for every opponent still in) and the results are counted.
    /// Uses only what the seat can see, so it never peeks at the opponents' real cards.
    /// </summary>
    public static class Equity
    {
        /// <summary>Chance (0..1) that the seat wins the pot; a tie counts as a share.</summary>
        public static double WinChance(HoldemHand h, int seat, int simulations, Random rng)
        {
            Seat me = h.Seats[seat];
            int opponents = 0;
            for (int i = 0; i < h.Seats.Count; i++)
                if (i != seat && !h.Seats[i].Folded) opponents++;
            if (opponents == 0) return 1.0;

            List<Card> board = h.VisibleBoard;
            var seen = new HashSet<Card>();
            seen.Add(me.Hole[0]);
            seen.Add(me.Hole[1]);
            for (int i = 0; i < board.Count; i++) seen.Add(board[i]);

            var rest = new List<Card>(52);
            for (int rank = 2; rank <= 14; rank++)
                for (int suit = 0; suit < 4; suit++)
                {
                    var c = new Card(rank, (CardSuit)suit);
                    if (!seen.Contains(c)) rest.Add(c);
                }

            int boardNeeded = 5 - board.Count;
            int need = boardNeeded + 2 * opponents;
            if (need > rest.Count) return 0.5;

            var mine = new Card[7];
            var theirs = new Card[7];
            mine[0] = me.Hole[0];
            mine[1] = me.Hole[1];
            double score = 0;
            for (int sim = 0; sim < simulations; sim++)
            {
                // partial Fisher-Yates: only the first `need` cards are needed
                for (int i = 0; i < need; i++)
                {
                    int j = i + rng.Next(rest.Count - i);
                    Card t = rest[i];
                    rest[i] = rest[j];
                    rest[j] = t;
                }
                for (int b = 0; b < 5; b++)
                {
                    Card c = b < board.Count ? board[b] : rest[b - board.Count];
                    mine[2 + b] = c;
                    theirs[2 + b] = c;
                }
                int myScore = HandEvaluator.Evaluate(mine);
                bool beaten = false;
                int tied = 1;
                for (int o = 0; o < opponents && !beaten; o++)
                {
                    theirs[0] = rest[boardNeeded + 2 * o];
                    theirs[1] = rest[boardNeeded + 2 * o + 1];
                    int s = HandEvaluator.Evaluate(theirs);
                    if (s > myScore) beaten = true;
                    else if (s == myScore) tied++;
                }
                if (!beaten) score += 1.0 / tied;
            }
            return score / simulations;
        }
    }
}
