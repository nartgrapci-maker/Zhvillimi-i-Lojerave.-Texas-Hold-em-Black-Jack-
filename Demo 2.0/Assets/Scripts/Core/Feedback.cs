using System;
using System.Collections.Generic;
using System.Text;

namespace PokerDemo.Core
{
    /// <summary>All player-facing instruction / feedback text, kept in one place so it can be playtested and edited.</summary>
    public static class Feedback
    {
        /// <summary>Playtest fix, Level 1: explain the first action (the blind).</summary>
        public static string FirstActionPrompt(int toCall)
        {
            return "You owe " + toCall + " more chips to match the big blind. Press Call to stay in.";
        }

        /// <summary>Playtest fix, Level 2: warn when the river could change who is ahead.</summary>
        public static string RiverHint(HoldemHand h)
        {
            if (h.Street != Phase.River) return null;
            Card river = h.Board[4];
            bool pairsBoard = false;
            for (int i = 0; i < 4; i++) if (h.Board[i].Rank == river.Rank) pairsBoard = true;
            if (!pairsBoard && river.Rank < 12) return null;
            return "The " + Card.RankShort(river.Rank) + " on the river (" + river + ") could give a rival three of a kind or a better pair. Is your hand still ahead?";
        }

        /// <summary>Playtest fix, Level 3: show outs and the price of the call. Null when it does not apply.</summary>
        public static string OutsHelper(HoldemHand h, int seat)
        {
            if (h.BoardCount != 3 && h.BoardCount != 4) return null;
            Seat s = h.Seats[seat];
            var known = new List<Card>(s.Hole);
            known.AddRange(h.VisibleBoard);
            if (HandEvaluator.CategoryOf(HandEvaluator.Evaluate(known)) >= HandCategory.Straight)
                return "You already have a straight or better.";

            int unseen = 0, outs = 0;
            for (int rank = 2; rank <= 14; rank++)
            {
                for (int suit = 0; suit < 4; suit++)
                {
                    var c = new Card(rank, (CardSuit)suit);
                    if (known.Contains(c)) continue;
                    unseen++;
                    var test = new List<Card>(known);
                    test.Add(c);
                    if (HandEvaluator.CategoryOf(HandEvaluator.Evaluate(test)) >= HandCategory.Straight) outs++;
                }
            }
            int pct = (int)Math.Round(100.0 * outs / unseen);
            var sb = new StringBuilder();
            sb.Append("Outs to a straight or better: " + outs + " of " + unseen + " unseen cards (about " + pct + "% next card).");
            int toCall = h.ToCall(seat);
            if (toCall > 0)
            {
                int need = (int)Math.Round(100.0 * toCall / (h.Pot + toCall));
                sb.Append("  Calling " + toCall + " into a pot of " + h.Pot + " means you need about " + need + "%.");
            }
            return sb.ToString();
        }

        /// <summary>Names the street where the winner first overtook the player ("the River (Qs)").</summary>
        public static string DecisiveStreet(HoldemHand h, int winnerSeat, int mySeat)
        {
            Card[] board = h.Board;
            string[] labels = new string[] { "Flop", "Turn", "River" };
            for (int i = 0; i < 3; i++)
            {
                int count = 3 + i;
                var w = new List<Card>(h.Seats[winnerSeat].Hole);
                var m = new List<Card>(h.Seats[mySeat].Hole);
                for (int b = 0; b < count; b++) { w.Add(board[b]); m.Add(board[b]); }
                if (HandEvaluator.Evaluate(w) > HandEvaluator.Evaluate(m))
                    return i == 0
                        ? "You were already behind on the Flop."
                        : "It turned on the " + labels[i] + " (" + board[count - 1] + ").";
            }
            return null;
        }

        /// <summary>The end-of-hand message for the human player (seat 0).</summary>
        public static string HandSummary(HoldemHand h)
        {
            HandResult r = h.Result;
            Seat me = h.Seats[0];
            var sb = new StringBuilder();

            if (r.ByFold)
            {
                if (me.Folded) sb.Append("You folded. Stack: " + me.Stack + ".");
                else sb.Append("Everybody else folded. You win " + r.Won[0] + ". Stack: " + me.Stack + ".");
                return sb.ToString();
            }

            int best = 0;
            for (int i = 1; i < h.Seats.Count; i++) if (r.Won[i] > r.Won[best]) best = i;

            if (r.Won[0] > 0 && r.Net[0] > 0)
            {
                string loser = null;
                for (int i = 1; i < h.Seats.Count; i++)
                    if (!h.Seats[i].Folded && r.HandNames[i] != null) { loser = r.HandNames[i]; break; }
                sb.Append("You win " + r.Net[0] + ". " + r.HandNames[0] + (loser != null ? " beats " + loser : "") + ". Stack: " + me.Stack + ".");
            }
            else
            {
                string mine = me.Folded ? null : r.HandNames[0];
                sb.Append("Lost to " + r.HandNames[best] + ".");
                if (mine != null) sb.Append(" Your " + mine + " was beaten.");
                string decisive = me.Folded ? null : DecisiveStreet(h, best, 0);
                if (decisive != null) sb.Append(" " + decisive);
                sb.Append(" Stack: " + me.Stack + ".");
            }
            return sb.ToString();
        }

        /// <summary>Shows what the opponents were holding once the hand is over.</summary>
        public static string RevealOpponents(HoldemHand h)
        {
            var sb = new StringBuilder();
            for (int i = 1; i < h.Seats.Count; i++)
            {
                Seat s = h.Seats[i];
                if (sb.Length > 0) sb.Append("\n");
                sb.Append(s.Name + " held " + s.Hole[0] + " " + s.Hole[1]);
                if (h.Result.HandNames != null && h.Result.HandNames[i] != null) sb.Append("  (" + h.Result.HandNames[i] + ")");
                else if (s.Folded) sb.Append("  (folded)");
            }
            return sb.ToString();
        }

        public static string PassMessage(LevelDef level, HoldemHand h)
        {
            Seat me = h.Seats[0];
            if (level.IsPassed(me.Stack))
            {
                if (level.Number == 2 && me.Folded)
                    return "Level passed. You kept " + me.Stack + " chips by folding when you were beaten.";
                return "Level passed with " + me.Stack + " chips.";
            }
            return "Level not passed. Goal: " + level.PassText + " You finished with " + me.Stack + ". Retry the same hand; the Hint button can help.";
        }
    }
}
