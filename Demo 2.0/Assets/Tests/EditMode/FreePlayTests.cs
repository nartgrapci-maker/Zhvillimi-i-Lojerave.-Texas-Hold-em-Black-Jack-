using System;
using System.Collections.Generic;
using NUnit.Framework;
using PokerDemo.Core;

namespace PokerDemo.Tests
{
    /// <summary>
    /// Free play (random deals, personality bots) cannot be replayed card by card like the levels,
    /// so these tests check the rules that must hold for every session: chips are conserved, every bot
    /// action is legal, hands always end, and the bots really are not predictable.
    /// </summary>
    public class FreePlayTests
    {
        const int TotalChips = 4 * FreePlaySession.StartStack;

        [Test]
        public void Deck_DealsFiftyTwoDistinctCards()
        {
            var deck = new Deck(new Random(7));
            var seen = new HashSet<Card>();
            for (int i = 0; i < 52; i++) Assert.IsTrue(seen.Add(deck.Draw()), "duplicate card dealt");
            Assert.AreEqual(0, deck.Remaining);
        }

        [Test]
        public void Sessions_NeverBreakTheRules()
        {
            int hands = 0;
            for (int seed = 1; seed <= 60; seed++)
            {
                var session = new FreePlaySession(seed);
                int sessionGuard = 0;
                while (!session.IsOver && sessionGuard++ < 400)
                {
                    HoldemHand hand = session.NextHand();
                    hands++;
                    int steps = 0;
                    while (!hand.IsComplete)
                    {
                        Assert.Less(steps++, 300, "Hand did not end (seed " + seed + ")");
                        PlayerAction action = SmartBot.Decide(hand, hand.Actor, session.Rng);
                        Assert.IsNull(hand.Validate(action), "Bot chose an illegal action (seed " + seed + "): " + hand.Validate(action));
                        hand.Apply(action);
                    }
                    session.FinishHand();
                    Assert.AreEqual(TotalChips, session.TotalChips(), "Chips were created or lost (seed " + seed + ")");
                }
            }
            Assert.Greater(hands, 300, "The simulation should have played many hands");
        }

        [Test]
        public void Session_EndsWhenSomeoneIsOut()
        {
            var session = new FreePlaySession(3);
            int guard = 0;
            while (!session.IsOver && guard++ < 400)
            {
                HoldemHand hand = session.NextHand();
                while (!hand.IsComplete) hand.Apply(SmartBot.Decide(hand, hand.Actor, session.Rng));
                session.FinishHand();
            }
            Assert.IsTrue(session.IsOver, "A session should end within a few hundred hands");
            Assert.IsTrue(session.HumanIsOut || session.BotsAllOut);
        }

        [Test]
        public void Bots_AreNotPredictable()
        {
            // Same hand and same seat, different random state: the bot must not always do the same thing.
            var chosen = new HashSet<string>();
            for (int seed = 1; seed <= 200; seed++)
            {
                var session = new FreePlaySession(seed);
                HoldemHand hand = session.NextHand();
                PlayerAction a = SmartBot.Decide(hand, hand.Actor, session.Rng);
                chosen.Add(a.Type + (a.Type == ActionType.Bet ? ":" + a.Total : ""));
            }
            Assert.GreaterOrEqual(chosen.Count, 3, "Bots should vary their actions and bet sizes. Saw: " + string.Join(", ", new List<string>(chosen).ToArray()));
        }

        [Test]
        public void BlindsRiseDuringASession()
        {
            var session = new FreePlaySession(11);
            int first = 0, last = 0;
            for (int i = 0; i < 12 && !session.IsOver; i++)
            {
                HoldemHand hand = session.NextHand();
                if (i == 0) first = session.BigBlind;
                last = session.BigBlind;
                while (!hand.IsComplete) hand.Apply(SmartBot.Decide(hand, hand.Actor, session.Rng));
                session.FinishHand();
            }
            Assert.Greater(last, first, "Blinds should rise as hands are played");
        }
    }
}
