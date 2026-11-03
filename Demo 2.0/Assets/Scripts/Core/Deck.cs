using System;
using System.Collections.Generic;

namespace PokerDemo.Core
{
    /// <summary>A shuffled 52 card deck. Pass a seeded System.Random to get a repeatable shuffle.</summary>
    public class Deck
    {
        readonly List<Card> _cards = new List<Card>(52);
        int _next;

        public Deck(Random rng)
        {
            for (int rank = 2; rank <= 14; rank++)
                for (int suit = 0; suit < 4; suit++)
                    _cards.Add(new Card(rank, (CardSuit)suit));

            // Fisher-Yates
            for (int i = _cards.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                Card tmp = _cards[i];
                _cards[i] = _cards[j];
                _cards[j] = tmp;
            }
        }

        public int Remaining { get { return _cards.Count - _next; } }

        public Card Draw()
        {
            if (_next >= _cards.Count) throw new InvalidOperationException("The deck is empty.");
            return _cards[_next++];
        }
    }
}
