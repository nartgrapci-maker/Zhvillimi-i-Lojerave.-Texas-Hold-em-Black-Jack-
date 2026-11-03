using System;
using System.Collections.Generic;

namespace PokerDemo.Core
{
    public enum CardSuit { Clubs = 0, Diamonds = 1, Hearts = 2, Spades = 3 }

    /// <summary>Thrown when a level / scripted hand is configured wrongly (a level defect, not a player loss).</summary>
    public class LevelConfigException : Exception
    {
        public LevelConfigException(string message) : base(message) { }
    }

    public struct Card : IEquatable<Card>
    {
        public readonly int Rank;      // 2..14 (14 = Ace)
        public readonly CardSuit Suit;

        public Card(int rank, CardSuit suit)
        {
            Rank = rank;
            Suit = suit;
        }

        public bool IsRed { get { return Suit == CardSuit.Hearts || Suit == CardSuit.Diamonds; } }

        /// <summary>Parses "Js", "10d", "Tc", "Ah" (rank then suit letter c/d/h/s).</summary>
        public static Card Parse(string text)
        {
            if (string.IsNullOrEmpty(text) || text.Length < 2)
                throw new LevelConfigException("Bad card text: '" + text + "'");
            string t = text.Trim();
            char suitChar = char.ToLowerInvariant(t[t.Length - 1]);
            string rankText = t.Substring(0, t.Length - 1).ToUpperInvariant();

            CardSuit suit;
            switch (suitChar)
            {
                case 'c': suit = CardSuit.Clubs; break;
                case 'd': suit = CardSuit.Diamonds; break;
                case 'h': suit = CardSuit.Hearts; break;
                case 's': suit = CardSuit.Spades; break;
                default: throw new LevelConfigException("Bad suit in card: '" + text + "'");
            }

            int rank;
            switch (rankText)
            {
                case "A": rank = 14; break;
                case "K": rank = 13; break;
                case "Q": rank = 12; break;
                case "J": rank = 11; break;
                case "T":
                case "10": rank = 10; break;
                default:
                    if (!int.TryParse(rankText, out rank) || rank < 2 || rank > 9)
                        throw new LevelConfigException("Bad rank in card: '" + text + "'");
                    break;
            }
            return new Card(rank, suit);
        }

        /// <summary>Parses a space separated list such as "Js Jd 10h".</summary>
        public static List<Card> ParseList(string text)
        {
            var list = new List<Card>();
            if (string.IsNullOrEmpty(text)) return list;
            string[] parts = text.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++) list.Add(Parse(parts[i]));
            return list;
        }

        public static string RankLabel(int rank)
        {
            switch (rank)
            {
                case 14: return "A";
                case 13: return "K";
                case 12: return "Q";
                case 11: return "J";
                default: return rank.ToString();
            }
        }

        /// <summary>"Ace", "King", ... "10", "9" (numbers stay numbers).</summary>
        public static string RankShort(int rank)
        {
            switch (rank)
            {
                case 14: return "Ace";
                case 13: return "King";
                case 12: return "Queen";
                case 11: return "Jack";
                default: return rank.ToString();
            }
        }

        public static string RankPlural(int rank)
        {
            switch (rank)
            {
                case 2: return "Twos";
                case 3: return "Threes";
                case 4: return "Fours";
                case 5: return "Fives";
                case 6: return "Sixes";
                case 7: return "Sevens";
                case 8: return "Eights";
                case 9: return "Nines";
                case 10: return "Tens";
                case 11: return "Jacks";
                case 12: return "Queens";
                case 13: return "Kings";
                default: return "Aces";
            }
        }

        public static char SuitLetter(CardSuit suit)
        {
            switch (suit)
            {
                case CardSuit.Clubs: return 'c';
                case CardSuit.Diamonds: return 'd';
                case CardSuit.Hearts: return 'h';
                default: return 's';
            }
        }

        public override string ToString() { return RankLabel(Rank) + SuitLetter(Suit); }

        public bool Equals(Card other) { return Rank == other.Rank && Suit == other.Suit; }
        public override bool Equals(object obj) { return obj is Card && Equals((Card)obj); }
        public override int GetHashCode() { return Rank * 4 + (int)Suit; }
    }
}
