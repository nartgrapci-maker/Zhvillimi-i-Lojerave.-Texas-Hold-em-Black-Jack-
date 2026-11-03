using System;
using System.Collections.Generic;

namespace PokerDemo.Core
{
    /// <summary>One Level/Puzzle Card as data. Seat 0 is always the human player.</summary>
    public class LevelDef
    {
        public int Number;
        public string PhaseLabel;            // Onboarding / Core challenge / Advanced challenge
        public string Title;
        public string Objective;
        public string TargetSkill;
        public string Constraints;
        public string PassText;           // pass condition shown to the player
        public string Hint;               // shown by the Hint button
        public string[] Names;
        public BotStyle[] Styles;
        public string[] Holes;            // "Js Jd" per seat
        public string Board;              // 5 scripted board cards
        public int StartStack = 100;
        public int SmallBlind = 5;
        public int BigBlind = 10;
        public int SmallBlindSeat = 0;
        public int BigBlindSeat = 1;
        public int PreflopFirstSeat = 0;
        public int PassMinChips;          // level is passed when the final stack is at least this
        // playtest-driven additions (proposed in the Playtest Note)
        public bool ShowFirstActionPrompt;
        public bool ShowRiverHint;
        public bool ShowOutsHelper;

        public bool IsPassed(int finalStack) { return finalStack >= PassMinChips; }

        public HoldemHand CreateHand()
        {
            if (Names == null || Holes == null || Styles == null || Names.Length != Holes.Length || Names.Length != Styles.Length)
                throw new LevelConfigException("Level " + Number + " has inconsistent seat data.");

            var seats = new List<Seat>();
            for (int i = 0; i < Names.Length; i++)
            {
                List<Card> hole = Card.ParseList(Holes[i]);
                seats.Add(new Seat
                {
                    Name = Names[i],
                    IsHuman = i == 0,
                    Style = Styles[i],
                    Stack = StartStack,
                    Hole = hole.ToArray()
                });
            }
            return new HoldemHand(seats, Board, SmallBlindSeat, BigBlindSeat, SmallBlind, BigBlind, PreflopFirstSeat);
        }
    }

    public static class Levels
    {
        public static readonly LevelDef[] All = new LevelDef[]
        {
            new LevelDef
            {
                Number = 1, PhaseLabel = "Onboarding", Title = "Learn the actions",
                Objective = "Win the hand.",
                TargetSkill = "Read your hand ranking and use Check, Bet, Call and Fold.",
                Constraints = "The bot only checks or calls. It never raises and never folds.",
                PassText = "Win the hand (finish with more than 100 chips).",
                Hint = "You hold a pair of Jacks. Any three of a kind beats a pair of Aces or a high card. Staying in (Call / Check) costs little here.",
                Names = new string[] { "You", "Bot" },
                Styles = new BotStyle[] { BotStyle.None, BotStyle.Passive },
                Holes = new string[] { "Js Jd", "Ac 5c" },
                Board = "Jh 4c 8d 2s 9d",
                PassMinChips = 101,
                ShowFirstActionPrompt = true
            },
            new LevelDef
            {
                Number = 2, PhaseLabel = "Core challenge", Title = "Know when you are beaten",
                Objective = "Keep at least 60 chips, even when the board turns against you.",
                TargetSkill = "Bet sizing, and folding when your hand is beaten.",
                Constraints = "The bot calls any bet with a pair. With three of a kind it bets half the pot on the river.",
                PassText = "Finish with at least 60 chips.",
                Hint = "Top pair is strong early, but the bot's cards are hidden. Watch how the river card changes who might be ahead before you pay a big bet.",
                Names = new string[] { "You", "Bot" },
                Styles = new BotStyle[] { BotStyle.None, BotStyle.Level2Rule },
                Holes = new string[] { "Ah Kd", "Qc Qd" },
                Board = "Ks 7c 2d 9h Qs",
                PassMinChips = 60,
                ShowFirstActionPrompt = true,
                ShowRiverHint = true
            },
            new LevelDef
            {
                Number = 3, PhaseLabel = "Advanced challenge", Title = "Draws against three opponents",
                Objective = "Win chips against three different opponents.",
                TargetSkill = "Play draws using outs and pot odds, and read several opponents.",
                Constraints = "Bot A is tight, Bot B is loose, Bot C is aggressive. Folding before the turn fails the level.",
                PassText = "Finish with more than 100 chips.",
                Hint = "You hold four spades and a straight draw. Compare the outs with the price of the call before you decide.",
                Names = new string[] { "You", "Bot A (tight)", "Bot B (loose)", "Bot C (aggressive)" },
                Styles = new BotStyle[] { BotStyle.None, BotStyle.Tight, BotStyle.Loose, BotStyle.Aggressive },
                Holes = new string[] { "As Ks", "Qd Qc", "9s 8s", "Kh Jh" },
                Board = "Qs Js 3d Tc 2d",
                PreflopFirstSeat = 2,
                PassMinChips = 101,
                ShowFirstActionPrompt = true,
                ShowOutsHelper = true
            }
        };
    }
}
