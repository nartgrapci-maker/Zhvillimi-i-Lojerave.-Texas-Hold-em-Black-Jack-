using System;

namespace PokerDemo.Core
{
    /// <summary>
    /// Rule-based opponents. Deterministic on purpose: the level cards depend on their exact behaviour.
    ///  Passive     : checks and calls, never bets, never folds (Level 1).
    ///  Level2Rule  : pair or better calls any bet; no pair calls bets up to 50% of the pot, else folds;
    ///                three of a kind or better bets 50% of the pot on the river when checked to (Level 2).
    ///  Tight       : three of a kind+ bets 50% pot when checked to and calls anything;
    ///                a pair calls bets up to 50% pot; otherwise folds.
    ///  Loose       : checks, calls every bet.
    ///  Aggressive  : bets 50% pot whenever checked to; calls with a pair or better, otherwise folds.
    /// </summary>
    public static class BotPolicy
    {
        public static PlayerAction Decide(HoldemHand h, int seat)
        {
            Seat s = h.Seats[seat];
            int toCall = h.ToCall(seat);
            int pot = h.Pot;
            int cat = h.MadeCategory(seat);
            bool preflop = h.Street == Phase.Preflop;
            BotStyle style = s.Style;

            if (toCall <= 0)
            {
                if (preflop) return PlayerAction.Check();

                bool wantBet = false;
                switch (style)
                {
                    case BotStyle.Level2Rule: wantBet = h.Street == Phase.River && cat >= 3; break;
                    case BotStyle.Tight: wantBet = cat >= 3; break;
                    case BotStyle.Aggressive: wantBet = true; break;
                }
                if (!wantBet) return PlayerAction.Check();

                int size = Math.Max(h.MinRaise, pot / 2);
                int total = Math.Min(h.CurrentBet + size, s.StreetBet + s.Stack);
                if (total <= h.CurrentBet) return PlayerAction.Check();
                return PlayerAction.BetTo(total);
            }

            // facing a bet
            if (preflop)
            {
                bool callsAnything = style == BotStyle.Passive || style == BotStyle.Loose;
                return (callsAnything || toCall <= 2 * h.BigBlind) ? PlayerAction.Call() : PlayerAction.Fold();
            }

            bool small = toCall * 2 <= pot - toCall;   // the bet is at most 50% of the pot before it
            switch (style)
            {
                case BotStyle.Passive:
                case BotStyle.Loose:
                    return PlayerAction.Call();
                case BotStyle.Level2Rule:
                    return (cat >= 1 || small) ? PlayerAction.Call() : PlayerAction.Fold();
                case BotStyle.Tight:
                    return (cat >= 3 || (cat >= 1 && small)) ? PlayerAction.Call() : PlayerAction.Fold();
                default:
                    return cat >= 1 ? PlayerAction.Call() : PlayerAction.Fold();
            }
        }
    }
}
