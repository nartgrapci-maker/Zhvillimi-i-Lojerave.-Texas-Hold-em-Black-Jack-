using System;
using System.Collections.Generic;

namespace PokerDemo.Core
{
    /// <summary>
    /// Personality of a free-play bot. The numbers are fixed per bot, but every hand the bot gets a new random
    /// mood, plus random noise on every decision, so its play cannot be read from a single rule.
    /// </summary>
    public class BotPersonality
    {
        public string Label;          // shown to the player, e.g. "Calculating"
        public double Tightness;      // 0 = plays almost everything, 1 = only strong hands
        public double Aggression;     // 0 = passive (calls), 1 = bets and raises a lot
        public double BluffFreq;      // chance per decision to try a bluff
        public double Noise;          // random +/- added to the hand strength estimate
        public double Mood;           // re-rolled every hand: positive = more optimistic

        public void NewHand(Random rng)
        {
            Mood = (rng.NextDouble() - 0.5) * 0.30;   // -0.15 .. +0.15
        }

        public static BotPersonality[] Presets()
        {
            return new BotPersonality[]
            {
                new BotPersonality { Label = "Calculating", Tightness = 0.65, Aggression = 0.55, BluffFreq = 0.06, Noise = 0.07 },
                new BotPersonality { Label = "Loose and friendly", Tightness = 0.25, Aggression = 0.25, BluffFreq = 0.04, Noise = 0.12 },
                new BotPersonality { Label = "Wild", Tightness = 0.30, Aggression = 0.85, BluffFreq = 0.22, Noise = 0.15 },
                new BotPersonality { Label = "Cautious", Tightness = 0.85, Aggression = 0.30, BluffFreq = 0.02, Noise = 0.05 }
            };
        }
    }

    /// <summary>
    /// Free-play opponent. Estimates hand strength (including draws), adds personality, mood and noise,
    /// then bets, calls or folds. Every action is checked against the table rules before it is returned,
    /// so the bot can never make an illegal move.
    /// </summary>
    public static class SmartBot
    {
        public static PlayerAction Decide(HoldemHand h, int seat, Random rng)
        {
            Seat me = h.Seats[seat];
            BotPersonality p = me.Persona ?? BotPersonality.Presets()[0];

            double strength = Strength(h, seat);
            double s = strength + (rng.NextDouble() - 0.5) * 2.0 * p.Noise + p.Mood;
            if (s < 0) s = 0;
            if (s > 1) s = 1;

            int toCall = h.ToCall(seat);
            int pot = h.Pot;
            bool bluff = rng.NextDouble() < p.BluffFreq * (0.5 + p.Aggression);
            bool preflop = h.Street == Phase.Preflop;
            PlayerAction chosen;

            if (toCall <= 0)
            {
                double betThreshold = 0.64 - 0.26 * p.Aggression + 0.10 * (p.Tightness - 0.5);
                bool wantBet = s >= betThreshold && rng.NextDouble() < 0.55 + 0.4 * p.Aggression;
                if (!wantBet && bluff && !preflop) wantBet = true;
                if (wantBet)
                {
                    double[] sizes = { 0.33, 0.5, 0.66, 1.0 };
                    int pick = s > 0.85 ? rng.Next(2, 4) : rng.Next(0, 3);
                    chosen = Raise(h, seat, sizes[pick]);
                }
                else chosen = PlayerAction.Check();
            }
            else
            {
                double potOdds = (double)toCall / (pot + toCall);
                double need = potOdds * (0.85 + 0.5 * p.Tightness) + 0.06 * p.Tightness;
                bool bigBetAlready = h.CurrentBet > h.BigBlind * 8;
                bool canRaise = !bigBetAlready || s > 0.92;

                if (canRaise && s > 0.78 && rng.NextDouble() < 0.25 + 0.5 * p.Aggression)
                    chosen = Raise(h, seat, rng.Next(2) == 0 ? 0.66 : 1.0);
                else if (canRaise && bluff && !preflop && rng.NextDouble() < 0.5)
                    chosen = Raise(h, seat, 0.75);
                else if (s >= need)
                    chosen = PlayerAction.Call();
                else if (rng.NextDouble() < 0.06 * (1.0 - p.Tightness))
                    chosen = PlayerAction.Call();          // curiosity call
                else
                    chosen = PlayerAction.Fold();
            }

            return Legal(h, chosen);
        }

        // -------------------------------------------------------------------------------------

        /// <summary>Falls back to a safe legal action when the chosen one would be refused.</summary>
        public static PlayerAction Legal(HoldemHand h, PlayerAction a)
        {
            if (h.Validate(a) == null) return a;
            if (h.ToCall(h.Actor) > 0) return PlayerAction.Call();
            return PlayerAction.Check();
        }

        static PlayerAction Raise(HoldemHand h, int seat, double fractionOfPot)
        {
            Seat s = h.Seats[seat];
            int maxTotal = s.StreetBet + s.Stack;
            int increase = Math.Max(h.MinRaise, (int)Math.Round(h.Pot * fractionOfPot));
            int total = h.CurrentBet + increase;
            if (total < maxTotal) total = Math.Max(h.CurrentBet + h.MinRaise, (total / 5) * 5);
            if (total > maxTotal) total = maxTotal;
            if (total <= h.CurrentBet) return h.ToCall(seat) > 0 ? PlayerAction.Call() : PlayerAction.Check();
            return PlayerAction.BetTo(total);
        }

        /// <summary>Rough hand strength 0..1 using only the cards the seat can see.</summary>
        public static double Strength(HoldemHand h, int seat)
        {
            Seat s = h.Seats[seat];
            if (h.BoardCount < 3) return PreflopStrength(s.Hole);

            var known = new List<Card>(s.Hole);
            known.AddRange(h.VisibleBoard);
            int score = HandEvaluator.Evaluate(known);
            int cat = score >> 20;
            int k0 = (score >> 16) & 15;

            int maxBoard = 0;
            List<Card> board = h.VisibleBoard;
            for (int i = 0; i < board.Count; i++) if (board[i].Rank > maxBoard) maxBoard = board[i].Rank;
            int hi = Math.Max(s.Hole[0].Rank, s.Hole[1].Rank);

            double st;
            switch (cat)
            {
                case 0: st = 0.08 + (hi - 2) / 12.0 * 0.12; break;
                case 1:
                    bool usesHole = s.Hole[0].Rank == k0 || s.Hole[1].Rank == k0;
                    if (!usesHole) st = 0.16;                                   // the pair is on the board
                    else if (k0 >= maxBoard) st = 0.58 + (k0 - 2) / 12.0 * 0.12; // top pair or better
                    else st = 0.40;
                    break;
                case 2: st = 0.74; break;
                case 3: st = 0.82; break;
                case 4: st = 0.90; break;
                case 5: st = 0.92; break;
                case 6: st = 0.96; break;
                case 7: st = 0.99; break;
                default: st = 1.0; break;
            }

            // value of a draw to a straight or better
            if (cat < 4 && h.BoardCount < 5)
            {
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
                        if ((HandEvaluator.Evaluate(test) >> 20) >= 4) outs++;
                    }
                }
                int toCome = 5 - h.BoardCount;
                double miss = 1.0;
                for (int i = 0; i < toCome; i++) miss *= 1.0 - (double)outs / Math.Max(1, unseen - i);
                st += (1.0 - miss) * 0.55;
            }
            return Math.Min(1.0, st);
        }

        static double PreflopStrength(Card[] hole)
        {
            int hi = Math.Max(hole[0].Rank, hole[1].Rank);
            int lo = Math.Min(hole[0].Rank, hole[1].Rank);
            if (hi == lo) return 0.50 + (hi - 2) / 12.0 * 0.45;

            double st = (hi - 2) / 12.0 * 0.35 + (lo - 2) / 12.0 * 0.20;
            if (hole[0].Suit == hole[1].Suit) st += 0.05;
            if (hi - lo <= 1) st += 0.04;
            else if (hi - lo == 2) st += 0.02;
            if (hi == 14) st += 0.05;
            return st;
        }
    }
}
