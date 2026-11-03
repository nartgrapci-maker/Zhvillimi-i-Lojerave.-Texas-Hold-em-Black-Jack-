using System;
using System.Collections.Generic;

namespace PokerDemo.Core
{
    public class Player
    {
        public string Name;
        public bool IsHuman;
        public int Stack;
        public BotPersonality Persona;
        public bool IsOut { get { return Stack <= 0; } }
    }

    /// <summary>
    /// Free-play table: you against three bots with random deals. Stacks carry over from hand to hand,
    /// the dealer button rotates, and the blinds go up every few hands. Player 0 is always the human.
    /// </summary>
    public class FreePlaySession
    {
        public const int StartStack = 200;
        public const int HandsPerBlindLevel = 4;
        static readonly int[][] BlindLevels = new int[][]
        {
            new int[] { 5, 10 }, new int[] { 10, 20 }, new int[] { 20, 40 }, new int[] { 40, 80 }, new int[] { 75, 150 }
        };

        public readonly List<Player> Players = new List<Player>();
        public int HandNumber { get; private set; }
        public int SmallBlind { get; private set; }
        public int BigBlind { get; private set; }
        public int DealerPlayer { get; private set; }
        public HoldemHand Current { get; private set; }
        /// <summary>For each seat of the current hand, the index into Players.</summary>
        public int[] ActivePlayers { get; private set; }
        public Random Rng { get; private set; }

        public FreePlaySession(int seed) : this(seed, null, null, StartStack) { }

        /// <summary>
        /// Tournament stage: fixed bot names and personalities (each still gets a small random variation
        /// and a new mood every hand), and a chosen starting stack for the bots.
        /// </summary>
        public FreePlaySession(int seed, string[] botNames, BotPersonality[] botPersonas, int botStack)
        {
            Rng = new Random(seed);
            Players.Add(new Player { Name = "You", IsHuman = true, Stack = StartStack });
            string[] names = botNames ?? new string[] { "Marco", "Lena", "Viktor" };
            BotPersonality[] presets = BotPersonality.Presets();

            // free play: each game the three bots get a different mix of personalities
            var pool = new List<BotPersonality>(presets);
            for (int i = 0; i < 3; i++)
            {
                BotPersonality template;
                double jitter;
                if (botPersonas != null)
                {
                    template = botPersonas[i];
                    jitter = 0.08;
                }
                else
                {
                    int pick = Rng.Next(pool.Count);
                    template = pool[pick];
                    pool.RemoveAt(pick);
                    jitter = 0.2;
                }
                Players.Add(new Player
                {
                    Name = names[i],
                    Stack = botStack,
                    Persona = new BotPersonality
                    {
                        Label = template.Label,
                        Tightness = Clamp01(template.Tightness + (Rng.NextDouble() - 0.5) * jitter),
                        Aggression = Clamp01(template.Aggression + (Rng.NextDouble() - 0.5) * jitter),
                        BluffFreq = Clamp01(template.BluffFreq + (Rng.NextDouble() - 0.5) * 0.04),
                        Noise = template.Noise
                    }
                });
            }
            DealerPlayer = -1;
        }

        static double Clamp01(double v) { return v < 0 ? 0 : (v > 1 ? 1 : v); }

        public bool HumanIsOut { get { return Players[0].IsOut; } }

        public bool BotsAllOut
        {
            get
            {
                for (int i = 1; i < Players.Count; i++) if (!Players[i].IsOut) return false;
                return true;
            }
        }

        public bool IsOver { get { return HumanIsOut || BotsAllOut; } }

        /// <summary>Deals the next hand. Call FinishHand() first when the previous hand is complete.</summary>
        public HoldemHand NextHand()
        {
            if (IsOver) return null;
            if (Current != null && !Current.IsComplete) throw new InvalidOperationException("The current hand is not finished.");

            var active = new List<int>();
            for (int i = 0; i < Players.Count; i++) if (!Players[i].IsOut) active.Add(i);

            // rotate the dealer button to the next player still in the game
            int d = DealerPlayer;
            do { d = (d + 1) % Players.Count; } while (Players[d].IsOut);
            DealerPlayer = d;
            int dealerPos = active.IndexOf(d);

            int level = Math.Min(HandNumber / HandsPerBlindLevel, BlindLevels.Length - 1);
            SmallBlind = BlindLevels[level][0];
            BigBlind = BlindLevels[level][1];

            int n = active.Count;
            int sb, bb, first;
            if (n == 2) { sb = dealerPos; bb = (dealerPos + 1) % 2; first = sb; }
            else { sb = (dealerPos + 1) % n; bb = (dealerPos + 2) % n; first = (bb + 1) % n; }

            var deck = new Deck(Rng);
            var seats = new List<Seat>();
            for (int i = 0; i < n; i++)
            {
                Player pl = Players[active[i]];
                if (pl.Persona != null) pl.Persona.NewHand(Rng);
                seats.Add(new Seat
                {
                    Name = pl.Name,
                    IsHuman = pl.IsHuman,
                    Style = BotStyle.None,
                    Persona = pl.Persona,
                    Stack = pl.Stack,
                    Hole = new Card[] { deck.Draw(), deck.Draw() }
                });
            }
            var board = new List<string>();
            for (int i = 0; i < 5; i++) board.Add(deck.Draw().ToString());

            ActivePlayers = active.ToArray();
            HandNumber++;
            Current = new HoldemHand(seats, string.Join(" ", board.ToArray()), sb, bb, SmallBlind, BigBlind, first);
            return Current;
        }

        /// <summary>Copies the final stacks of a completed hand back to the players.</summary>
        public void FinishHand()
        {
            if (Current == null || !Current.IsComplete) return;
            for (int i = 0; i < ActivePlayers.Length; i++)
                Players[ActivePlayers[i]].Stack = Current.Seats[i].Stack;
        }

        public int TotalChips()
        {
            int t = 0;
            for (int i = 0; i < Players.Count; i++) t += Players[i].Stack;
            return t;
        }
    }
}
