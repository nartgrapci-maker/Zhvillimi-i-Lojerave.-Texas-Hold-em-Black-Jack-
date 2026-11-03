using System;
using System.Collections.Generic;

namespace PokerDemo.Core
{
    public enum HandCategory
    {
        HighCard = 0, Pair = 1, TwoPair = 2, ThreeOfAKind = 3, Straight = 4,
        Flush = 5, FullHouse = 6, FourOfAKind = 7, StraightFlush = 8
    }

    /// <summary>
    /// Scores the best 5-card poker hand out of 5..7 cards. Higher score = better hand.
    /// Score layout: category (bits 20+) then up to five 4-bit rank values.
    /// </summary>
    public static class HandEvaluator
    {
        public static int Evaluate(IList<Card> cards)
        {
            int n = cards.Count;
            if (n < 5 || n > 7) throw new ArgumentException("Need 5 to 7 cards, got " + n);

            int best = -1;
            var pick = new Card[5];
            for (int mask = 0; mask < (1 << n); mask++)
            {
                if (PopCount(mask) != 5) continue;
                int k = 0;
                for (int i = 0; i < n; i++)
                    if ((mask & (1 << i)) != 0) pick[k++] = cards[i];
                int s = Score5(pick);
                if (s > best) best = s;
            }
            return best;
        }

        public static HandCategory CategoryOf(int score) { return (HandCategory)(score >> 20); }

        static int PopCount(int v)
        {
            int c = 0;
            while (v != 0) { c += v & 1; v >>= 1; }
            return c;
        }

        static int Score5(Card[] c)
        {
            int[] counts = new int[15];
            bool flush = true;
            for (int i = 0; i < 5; i++)
            {
                counts[c[i].Rank]++;
                if (c[i].Suit != c[0].Suit) flush = false;
            }

            // groups encoded as count*100+rank, sorted so bigger groups (then higher ranks) come first
            var keys = new List<int>();
            for (int r = 14; r >= 2; r--)
                if (counts[r] > 0) keys.Add(counts[r] * 100 + r);
            keys.Sort((a, b) => b.CompareTo(a));

            int straightHigh = 0;
            if (keys.Count == 5)
            {
                int top = keys[0] - 100;
                int low = keys[4] - 100;
                if (top - low == 4) straightHigh = top;
                else if (top == 14 && keys[1] - 100 == 5 && low == 2) straightHigh = 5; // A-2-3-4-5
            }

            int c0 = keys[0] / 100;
            int c1 = keys.Count > 1 ? keys[1] / 100 : 0;

            HandCategory cat;
            if (straightHigh > 0 && flush) cat = HandCategory.StraightFlush;
            else if (c0 == 4) cat = HandCategory.FourOfAKind;
            else if (c0 == 3 && c1 == 2) cat = HandCategory.FullHouse;
            else if (flush) cat = HandCategory.Flush;
            else if (straightHigh > 0) cat = HandCategory.Straight;
            else if (c0 == 3) cat = HandCategory.ThreeOfAKind;
            else if (c0 == 2 && c1 == 2) cat = HandCategory.TwoPair;
            else if (c0 == 2) cat = HandCategory.Pair;
            else cat = HandCategory.HighCard;

            int[] k = new int[5];
            if (cat == HandCategory.Straight || cat == HandCategory.StraightFlush) k[0] = straightHigh;
            else for (int i = 0; i < keys.Count; i++) k[i] = keys[i] % 100;

            return ((int)cat << 20) | (k[0] << 16) | (k[1] << 12) | (k[2] << 8) | (k[3] << 4) | k[4];
        }

        static string StraightText(int top)
        {
            string low = top == 5 ? "Ace" : Card.RankShort(top - 4);
            return Card.RankShort(top) + " to " + low;
        }

        /// <summary>Human readable name, e.g. "Three Jacks", "Straight, Ace to 10", "Ace-high".</summary>
        public static string Describe(int score)
        {
            HandCategory cat = CategoryOf(score);
            int k0 = (score >> 16) & 15;
            int k1 = (score >> 12) & 15;
            switch (cat)
            {
                case HandCategory.HighCard: return Card.RankShort(k0) + "-high";
                case HandCategory.Pair: return "Pair of " + Card.RankPlural(k0);
                case HandCategory.TwoPair: return "Two pair, " + Card.RankPlural(k0) + " and " + Card.RankPlural(k1);
                case HandCategory.ThreeOfAKind: return "Three " + Card.RankPlural(k0);
                case HandCategory.Straight: return "Straight, " + StraightText(k0);
                case HandCategory.Flush: return "Flush, " + Card.RankShort(k0) + "-high";
                case HandCategory.FullHouse: return "Full house, " + Card.RankPlural(k0) + " over " + Card.RankPlural(k1);
                case HandCategory.FourOfAKind: return "Four " + Card.RankPlural(k0);
                default: return k0 == 14 ? "Royal flush" : "Straight flush, " + StraightText(k0);
            }
        }
    }
}
