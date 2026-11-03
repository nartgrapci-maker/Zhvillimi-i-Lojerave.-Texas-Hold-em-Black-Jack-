using System.Collections.Generic;
using NUnit.Framework;
using PokerDemo.Core;

namespace PokerDemo.Tests
{
    /// <summary>
    /// Replays the solutions written on the Level/Puzzle Cards against the real engine and bots.
    /// Open Window > General > Test Runner > EditMode > Run All.
    /// </summary>
    public class LevelSolutionTests
    {
        static readonly PlayerAction Check = PlayerAction.Check();
        static readonly PlayerAction Call = PlayerAction.Call();
        static readonly PlayerAction Fold = PlayerAction.Fold();
        static PlayerAction Bet(int total) { return PlayerAction.BetTo(total); }

        /// <summary>Plays a level: the given actions are the human's, bots decide for themselves.</summary>
        static HoldemHand Play(int levelNumber, params PlayerAction[] human)
        {
            LevelDef level = Levels.All[levelNumber - 1];
            HoldemHand hand = level.CreateHand();
            int next = 0;
            int guard = 0;
            while (!hand.IsComplete)
            {
                Assert.Less(guard++, 200, "Hand did not finish");
                PlayerAction action;
                if (hand.Seats[hand.Actor].IsHuman)
                {
                    Assert.Less(next, human.Length, "The script ran out of human actions on " + hand.Street);
                    action = human[next++];
                    Assert.IsNull(hand.Validate(action), "Illegal scripted action #" + next + ": " + hand.Validate(action));
                }
                else action = BotPolicy.Decide(hand, hand.Actor);
                hand.Apply(action);
            }
            Assert.AreEqual(human.Length, next, "Not every scripted human action was used");
            return hand;
        }

        // ------------------------------------------------------------------ evaluator

        static int Eval(string cards) { return HandEvaluator.Evaluate(Card.ParseList(cards)); }

        [Test]
        public void Evaluator_NamesHands()
        {
            Assert.AreEqual("Straight, Ace to 10", HandEvaluator.Describe(Eval("As Ks Qs Js Tc 3d 2d")));
            Assert.AreEqual("Straight, 5 to Ace", HandEvaluator.Describe(Eval("As 2d 3c 4h 5s Kd 9c")));
            Assert.AreEqual("Three Jacks", HandEvaluator.Describe(Eval("Js Jd Jh 4c 8d 2s 9d")));
            Assert.AreEqual("Ace-high", HandEvaluator.Describe(Eval("Ac 5c Jh 4c 8d 2s 9d")));
        }

        [Test]
        public void Evaluator_Ordering()
        {
            Assert.Greater(Eval("As Ks Qs Js 3d Tc 2d"), Eval("9s 8s Qs Js 3d Tc 2d"));   // Broadway beats Queen-high straight
            Assert.Greater(Eval("9s 8s Qs Js 3d Tc 2d"), Eval("Qd Qc Qs Js 3d Tc 2d"));   // straight beats three of a kind
            Assert.Greater(Eval("Qd Qc Qs Js 3d Tc 2d"), Eval("Kh Jh Qs Js 3d Tc 2d"));   // trips beat a pair
        }

        // ------------------------------------------------------------------ level data

        [Test]
        public void AllLevels_AreWellFormed()
        {
            for (int i = 0; i < Levels.All.Length; i++)
                Assert.DoesNotThrow(() => Levels.All[i].CreateHand(), "Level " + (i + 1));
        }

        [Test]
        public void DuplicateCard_IsALevelDefect_NotAPlayerLoss()
        {
            var broken = new LevelDef
            {
                Number = 99,
                Names = new string[] { "You", "Bot" },
                Styles = new BotStyle[] { BotStyle.None, BotStyle.Passive },
                Holes = new string[] { "Js Jd", "Js 5c" },       // Js twice
                Board = "Jh 4c 8d 2s 9d"
            };
            Assert.Throws<LevelConfigException>(() => broken.CreateHand());
        }

        // ------------------------------------------------------------------ Level 1

        [Test]
        public void Level1_Solution_WinsWithThreeJacks()
        {
            HoldemHand h = Play(1, Call, Check, Check, Check);
            Assert.AreEqual(110, h.Seats[0].Stack);
            Assert.AreEqual("Three Jacks", h.Result.HandNames[0]);
            Assert.AreEqual("Ace-high", h.Result.HandNames[1]);
            Assert.IsTrue(Levels.All[0].IsPassed(h.Seats[0].Stack));
        }

        [Test]
        public void Level1_FoldingPreflop_Fails()
        {
            HoldemHand h = Play(1, Fold);
            Assert.AreEqual(95, h.Seats[0].Stack);
            Assert.IsFalse(Levels.All[0].IsPassed(h.Seats[0].Stack));
        }

        [Test]
        public void Level1_BetBelowMinimumRaise_IsRefused()
        {
            HoldemHand h = Levels.All[0].CreateHand();
            string error = h.Validate(Bet(15));        // raise of 5 over the big blind of 10
            Assert.IsNotNull(error);
            StringAssert.Contains("Minimum raise", error);
        }

        // ------------------------------------------------------------------ Level 2

        [Test]
        public void Level2_Solution_FoldingToTheRiverBet_KeepsSixtyChips()
        {
            HoldemHand h = Play(2, Call, Bet(10), Bet(20), Check, Fold);
            Assert.AreEqual(60, h.Seats[0].Stack);
            Assert.IsTrue(Levels.All[1].IsPassed(h.Seats[0].Stack));
        }

        [Test]
        public void Level2_CallingTheRiverBet_LosesToThreeQueens()
        {
            HoldemHand h = Play(2, Call, Bet(10), Bet(20), Check, Call);
            Assert.AreEqual(20, h.Seats[0].Stack);
            Assert.AreEqual("Pair of Kings", h.Result.HandNames[0]);
            Assert.AreEqual("Three Queens", h.Result.HandNames[1]);
            Assert.IsFalse(Levels.All[1].IsPassed(h.Seats[0].Stack));
            StringAssert.Contains("River", Feedback.DecisiveStreet(h, 1, 0));
        }

        [Test]
        public void Level2_OverbettingEarly_LosesEverything()
        {
            HoldemHand h = Play(2, Call, Bet(30), Bet(60));
            Assert.AreEqual(0, h.Seats[0].Stack);
        }

        // ------------------------------------------------------------------ Level 3

        [Test]
        public void Level3_Solution_CallTheDrawAndWinWithBroadway()
        {
            HoldemHand h = Play(3, Call, Check, Call, Check, Call, Check, Call);
            Assert.AreEqual(400, h.Seats[0].Stack);
            Assert.AreEqual("Straight, Ace to 10", h.Result.HandNames[0]);
            Assert.IsTrue(Levels.All[2].IsPassed(h.Seats[0].Stack));
        }

        [Test]
        public void Level3_FoldingTheFlop_Fails()
        {
            HoldemHand h = Play(3, Call, Check, Fold);
            Assert.AreEqual(90, h.Seats[0].Stack);
            Assert.IsFalse(Levels.All[2].IsPassed(h.Seats[0].Stack));
        }

        [Test]
        public void Level3_FlopHasTwelveOuts()
        {
            HoldemHand h = Levels.All[2].CreateHand();
            int guard = 0;
            while (h.Street != Phase.Flop)
            {
                Assert.Less(guard++, 50);
                PlayerAction a = h.Seats[h.Actor].IsHuman ? Call : BotPolicy.Decide(h, h.Actor);
                h.Apply(a);
            }
            StringAssert.Contains("12 of 47", Feedback.OutsHelper(h, 0));
        }

        // ------------------------------------------------------------------ Blackjack

        [Test]
        public void Blackjack_PerfectRun_Is135Chips()
        {
            var s = new BlackjackSession();
            s.StartHand(); s.Current.Hit();    Assert.AreEqual(10, s.Current.Net); s.FinishHand();
            s.StartHand(); s.Current.Stand();  Assert.AreEqual(10, s.Current.Net); s.FinishHand();
            s.StartHand();                     Assert.AreEqual(15, s.Current.Net); s.FinishHand();
            Assert.IsTrue(s.IsOver);
            Assert.AreEqual(135, s.Chips);
            Assert.IsTrue(s.Passed);
        }

        [Test]
        public void Blackjack_HittingOnSeventeen_Busts()
        {
            var s = new BlackjackSession();
            s.StartHand(); s.Current.Hit(); s.FinishHand();
            s.StartHand(); s.Current.Hit();
            Assert.AreEqual(BlackjackOutcome.PlayerBust, s.Current.Outcome);
            Assert.AreEqual(-10, s.Current.Net);
        }
    }
}
