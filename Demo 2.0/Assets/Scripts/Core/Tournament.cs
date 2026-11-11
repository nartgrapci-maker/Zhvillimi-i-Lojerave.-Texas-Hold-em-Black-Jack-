using System;

namespace PokerDemo.Core
{
    /// <summary>One stage of the tournament: three opponents with fixed names and playing styles.</summary>
    public class StageDef
    {
        public string[] BotNames;
        public BotPersonality[] Bots;
        public int BotStack;
    }

    /// <summary>
    /// The tournament "Udhetimi nepër Shqipëri": seven stages in the order of the city wheel (Oda e burrave, Shkodër,
    /// Berat, Gjirokastër, Tiranë, Sarandë, Theth), each against three new opponents who play harder than the stage
    /// before (they bluff more, make fewer random mistakes, and the last stages start them with more chips).
    /// Stage s is played on map s - 1 (map -1 is the oda).
    /// </summary>
    public static class Tournament
    {
        static BotPersonality P(string label, double tight, double aggr, double bluff, double noise)
        {
            return new BotPersonality { Label = label, Tightness = tight, Aggression = aggr, BluffFreq = bluff, Noise = noise };
        }

        public static readonly StageDef[] Stages = new StageDef[]
        {
            // 1 Oda e burrave: a game among family by the fire, the gentlest table of all
            new StageDef
            {
                BotNames = new string[] { "Qemal", "Lule", "Hysen" },
                Bots = new BotPersonality[]
                {
                    P("Loose and friendly", 0.20, 0.15, 0.02, 0.18),
                    P("Cautious", 0.78, 0.20, 0.01, 0.10),
                    P("Loose and friendly", 0.25, 0.20, 0.03, 0.16)
                },
                BotStack = 200
            },
            // 2 Shkodër: friendly table, easy to read
            new StageDef
            {
                BotNames = new string[] { "Zef", "Rozafa", "Pjetër" },
                Bots = new BotPersonality[]
                {
                    P("Loose and friendly", 0.22, 0.20, 0.03, 0.14),
                    P("Cautious", 0.80, 0.25, 0.02, 0.08),
                    P("Loose and friendly", 0.30, 0.30, 0.05, 0.14)
                },
                BotStack = 200
            },
            // 3 Berat: a bluffer appears
            new StageDef
            {
                BotNames = new string[] { "Besnik", "Drita", "Ilir" },
                Bots = new BotPersonality[]
                {
                    P("Calculating", 0.62, 0.55, 0.06, 0.07),
                    P("Wild", 0.35, 0.75, 0.15, 0.12),
                    P("Cautious", 0.85, 0.30, 0.02, 0.05)
                },
                BotStack = 200
            },
            // 4 Gjirokastër: tough and aggressive
            new StageDef
            {
                BotNames = new string[] { "Skënder", "Vjollca", "Gjergj" },
                Bots = new BotPersonality[]
                {
                    P("Calculating", 0.60, 0.65, 0.08, 0.05),
                    P("Wild", 0.30, 0.85, 0.20, 0.10),
                    P("Calculating", 0.70, 0.50, 0.05, 0.04)
                },
                BotStack = 200
            },
            // 5 Tiranë: the capital, sharp players
            new StageDef
            {
                BotNames = new string[] { "Arben", "Teuta", "Dritan" },
                Bots = new BotPersonality[]
                {
                    P("Calculating", 0.62, 0.70, 0.10, 0.03),
                    P("Wild", 0.35, 0.90, 0.25, 0.06),
                    P("Calculating", 0.72, 0.60, 0.07, 0.03)
                },
                BotStack = 220
            },
            // 6 Sarandë: summer players on the riviera, bold and hard to scare off
            new StageDef
            {
                BotNames = new string[] { "Andi", "Klea", "Marenglen" },
                Bots = new BotPersonality[]
                {
                    P("Wild", 0.32, 0.88, 0.24, 0.05),
                    P("Calculating", 0.66, 0.68, 0.09, 0.03),
                    P("Wild", 0.38, 0.92, 0.27, 0.05)
                },
                BotStack = 230
            },
            // 7 Theth: the final, high in the Alps, the best bots and the biggest stacks
            new StageDef
            {
                BotNames = new string[] { "Gjon", "Mrika", "Prenk" },
                Bots = new BotPersonality[]
                {
                    P("Calculating", 0.64, 0.74, 0.11, 0.02),
                    P("Wild", 0.36, 0.92, 0.26, 0.04),
                    P("Calculating", 0.74, 0.64, 0.08, 0.02)
                },
                BotStack = 250
            }
        };

        public static FreePlaySession StartStage(int stage, int seed)
        {
            StageDef d = Stages[stage];
            return new FreePlaySession(seed, d.BotNames, d.Bots, d.BotStack);
        }
    }
}
