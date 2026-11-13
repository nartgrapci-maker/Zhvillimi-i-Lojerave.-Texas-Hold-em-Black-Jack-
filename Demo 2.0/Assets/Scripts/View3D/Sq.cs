using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using PokerDemo.Core;

/// <summary>
/// The Albanian text of the 3D game. The Core rules (and the tests that check them) keep their English
/// strings; this class turns hand names, table events, refused moves and all feedback into Albanian
/// for the 3D view, using the same data (cards, stacks, board) the Core produces.
/// </summary>
public static class Sq
{
    public const string Currency = "lekë";

    // ---------------------------------------------------------------------------------------
    // cards and hands
    // ---------------------------------------------------------------------------------------

    /// <summary>Card name in the singular: Aso, Mbret, Dama, Fant, 10.</summary>
    public static string Rank(int rank)
    {
        switch (rank)
        {
            case 14: return "Aso";
            case 13: return "Mbret";
            case 12: return "Dama";
            case 11: return "Fant";
            default: return rank.ToString();
        }
    }

    /// <summary>Card name for groups ("Çift Mbretër", "Treshe Fantë").</summary>
    public static string RankPl(int rank)
    {
        switch (rank)
        {
            case 14: return "Aso";
            case 13: return "Mbretër";
            case 12: return "Dama";
            case 11: return "Fantë";
            default: return rank.ToString();
        }
    }

    static string StraightText(int top)
    {
        if (top == 5) return "5 te Aso";
        return Rank(top) + " te " + Rank(top - 4);
    }

    /// <summary>Albanian name of a hand score: "Treshe Fantë", "Shkallë nga Aso te 10", "Aso e lartë".</summary>
    public static string Describe(int score)
    {
        HandCategory cat = HandEvaluator.CategoryOf(score);
        int k0 = (score >> 16) & 15;
        int k1 = (score >> 12) & 15;
        switch (cat)
        {
            case HandCategory.HighCard: return "Letër e lartë: " + Rank(k0);
            case HandCategory.Pair: return "Çift " + RankPl(k0);
            case HandCategory.TwoPair: return "Dy çifte: " + RankPl(k0) + " dhe " + RankPl(k1);
            case HandCategory.ThreeOfAKind: return "Treshe " + RankPl(k0);
            case HandCategory.Straight: return "Shkallë nga " + StraightText(k0);
            case HandCategory.Flush: return "Ngjyrë, " + Rank(k0) + " e lartë";
            case HandCategory.FullHouse: return "Full: " + RankPl(k0) + " mbi " + RankPl(k1);
            case HandCategory.FourOfAKind: return "Katërshe " + RankPl(k0);
            default: return k0 == 14 ? "Shkallë mbretërore" : "Shkallë ngjyrë nga " + StraightText(k0);
        }
    }

    /// <summary>The seat's final hand once all five board cards exist (null when it cannot be known).</summary>
    public static string HandName(HoldemHand h, int seat)
    {
        Seat s = h.Seats[seat];
        Card[] board = h.Board;
        var all = new List<Card>(s.Hole);
        all.AddRange(board);
        if (all.Count < 5) return null;
        return Describe(HandEvaluator.Evaluate(all));
    }

    /// <summary>The seat's hand using only the cards visible right now (needs the flop).</summary>
    public static string HandNow(HoldemHand h, int seat)
    {
        if (h.BoardCount < 3) return null;
        var all = new List<Card>(h.Seats[seat].Hole);
        all.AddRange(h.VisibleBoard);
        return Describe(HandEvaluator.Evaluate(all));
    }

    // ---------------------------------------------------------------------------------------
    // names and styles
    // ---------------------------------------------------------------------------------------

    public static string Name(string name)
    {
        if (name == "You") return "Ti";
        if (name == "Dealer") return "Krupieri";
        if (name == "Bot") return "Boti";
        if (name.StartsWith("Bot ")) return "Boti " + name.Substring(4);
        return name;
    }

    // every woman in the game: the tour (Lule, Rozafa, Drita, Vjollca, Teuta, Klea, Mrika), free play (Elira), the other
    // tables, and the Core's fallback name (Lena). Anyone not listed is drawn as a man.
    static readonly HashSet<string> Women = new HashSet<string> { "Rozafa", "Drita", "Vjollca", "Teuta", "Elira", "Lule", "Klea", "Mrika", "Lena" };

    public static bool IsFemale(string name) { return name != null && Women.Contains(name.Trim()); }

    /// <summary>"lojtar llogaritës", "lojtar i egër": how a bot's style is described.</summary>
    public static string Style(string label)
    {
        switch (label)
        {
            case "Calculating": return "lojtar llogaritës";
            case "Loose and friendly": return "lojtar i çlirët e miqësor";
            case "Wild": return "lojtar i egër";
            case "Cautious": return "lojtar i kujdesshëm";
            default: return "lojtar";
        }
    }

    // ---------------------------------------------------------------------------------------
    // table events and refused moves (translated from the Core's English)
    // ---------------------------------------------------------------------------------------

    static readonly Regex ReBlinds = new Regex(@"^Blinds: (.+) (\d+), (.+) (\d+)\.$");
    static readonly Regex ReFold = new Regex(@"^(.+): Fold$");
    static readonly Regex ReCheck = new Regex(@"^(.+): Check$");
    static readonly Regex ReCall = new Regex(@"^(.+): Call (\d+)( \(all-in\))?$");
    static readonly Regex ReBet = new Regex(@"^(.+): (Bet|Raise to) (\d+)( \(all-in\))?$");
    static readonly Regex ReStreet = new Regex(@"^(Flop|Turn|River): (.*?)\s+\(pot (\d+)\)$");

    public static string Event(string e)
    {
        Match m;
        if ((m = ReBlinds.Match(e)).Success)
            return "Bllindat: " + Name(m.Groups[1].Value) + " " + m.Groups[2].Value + ", " + Name(m.Groups[3].Value) + " " + m.Groups[4].Value + ".";
        if ((m = ReFold.Match(e)).Success) return Name(m.Groups[1].Value) + ": heq dorë";
        if ((m = ReCheck.Match(e)).Success) return Name(m.Groups[1].Value) + ": kalon";
        if ((m = ReCall.Match(e)).Success)
            return Name(m.Groups[1].Value) + ": barazon " + m.Groups[2].Value + (m.Groups[3].Success ? " (all-in)" : "");
        if ((m = ReBet.Match(e)).Success)
        {
            bool bet = m.Groups[2].Value == "Bet";
            return Name(m.Groups[1].Value) + ": " + (bet ? "vë " : "ngrit në ") + m.Groups[3].Value + (m.Groups[4].Success ? " (all-in)" : "");
        }
        if ((m = ReStreet.Match(e)).Success)
            return m.Groups[1].Value + ": " + m.Groups[2].Value + "   (pot " + m.Groups[3].Value + ")";
        if (e == "Showdown.") return "Shfaqja e letrave.";
        if (e == "Hand ends: everybody else folded.") return "Dora mbaron: të tjerët hoqën dorë.";
        return e;
    }

    static readonly Regex ReErrCheck = new Regex(@"^You cannot check: there is a bet of (\d+) to call\.$");
    static readonly Regex ReErrLarger = new Regex(@"^A bet must be larger than the current bet of (\d+)\.$");
    static readonly Regex ReErrOnly = new Regex(@"^You only have (\d+) chips\.$");
    static readonly Regex ReErrMinBet = new Regex(@"^Minimum bet is (\d+)\.$");
    static readonly Regex ReErrMinRaise = new Regex(@"^Minimum raise is (\d+) \(to a total of (\d+)\)\.$");

    public static string Error(string e)
    {
        if (string.IsNullOrEmpty(e)) return e;
        Match m;
        if (e == "The hand is over.") return "Dora ka mbaruar.";
        if ((m = ReErrCheck.Match(e)).Success) return "Nuk mund të kalosh: ka një bast prej " + m.Groups[1].Value + " " + Currency + " për të barazuar.";
        if (e == "There is nothing to call. Use Check.") return "Nuk ke çfarë të barazosh. Përdor «Kalo».";
        if ((m = ReErrLarger.Match(e)).Success) return "Basti duhet të jetë më i madh se basti aktual prej " + m.Groups[1].Value + ".";
        if ((m = ReErrOnly.Match(e)).Success) return "Ke vetëm " + m.Groups[1].Value + " " + Currency + ".";
        if ((m = ReErrMinBet.Match(e)).Success) return "Basti minimal është " + m.Groups[1].Value + ".";
        if ((m = ReErrMinRaise.Match(e)).Success) return "Ngritja minimale është " + m.Groups[1].Value + " (deri në gjithsej " + m.Groups[2].Value + ").";
        return e;
    }

    // ---------------------------------------------------------------------------------------
    // levels
    // ---------------------------------------------------------------------------------------

    public class LevelText
    {
        public string Phase, Title, Objective, Skill, Constraints, Pass, Hint;
    }

    public static LevelText Level(int number)
    {
        switch (number)
        {
            case 1:
                return new LevelText
                {
                    Phase = "Hyrje",
                    Title = "Mëso veprimet",
                    Objective = "Fito dorën.",
                    Skill = "Lexo renditjen e dorës dhe përdor Kalo, Vë, Barazo dhe Hiq dorë.",
                    Constraints = "Boti vetëm kalon ose barazon. Nuk ngre kurrë dhe nuk heq dorë.",
                    Pass = "Fito dorën (përfundo me më shumë se 100 " + Currency + ").",
                    Hint = "Ke një çift Fantësh. Çdo treshe fiton ndaj një çifti Asosh ose një letre të lartë. Të qëndrosh në lojë (Barazo / Kalo) kushton pak këtu."
                };
            case 2:
                return new LevelText
                {
                    Phase = "Sfida kryesore",
                    Title = "Dije kur je i mundur",
                    Objective = "Ruaj të paktën 60 " + Currency + ", edhe kur bordi kthehet kundër teje.",
                    Skill = "Madhësia e bastit dhe heqja dorë kur dora jote është e mundur.",
                    Constraints = "Boti barazon çdo bast me një çift. Me një treshe vë gjysmën e potit në river.",
                    Pass = "Përfundo me të paktën 60 " + Currency + ".",
                    Hint = "Çifti i lartë është i fortë në fillim, por letrat e botit janë të fshehura. Shiko si e ndryshon letra e river-it se kush është përpara, para se të paguash një bast të madh."
                };
            default:
                return new LevelText
                {
                    Phase = "Sfida e avancuar",
                    Title = "Kërkim letre kundër tre kundërshtarëve",
                    Objective = "Fito " + Currency + " kundër tre kundërshtarëve të ndryshëm.",
                    Skill = "Luaj draw-t me «outs» dhe pot odds, dhe lexo disa kundërshtarë njëkohësisht.",
                    Constraints = "Boti A është i ngushtë, Boti B i çlirët, Boti C agresiv. Heqja dorë para turn-it e dështon nivelin.",
                    Pass = "Përfundo me më shumë se 100 " + Currency + ".",
                    Hint = "Ke katër letra maç dhe një draw shkalle. Krahaso outs-et me çmimin e barazimit para se të vendosësh."
                };
        }
    }

    // ---------------------------------------------------------------------------------------
    // feedback (the Albanian versions of Feedback.cs)
    // ---------------------------------------------------------------------------------------

    public static string FirstActionPrompt(int toCall)
    {
        return "Të mungojnë edhe " + toCall + " " + Currency + " për të barazuar bllindën e madhe. Shtyp «Barazo» për të qëndruar në lojë.";
    }

    public static string RiverHint(HoldemHand h)
    {
        if (h.Street != Phase.River) return null;
        Card river = h.Board[4];
        bool pairsBoard = false;
        for (int i = 0; i < 4; i++) if (h.Board[i].Rank == river.Rank) pairsBoard = true;
        if (!pairsBoard && river.Rank < 12) return null;
        return "Letra " + Rank(river.Rank) + " në river (" + river + ") mund t'i japë kundërshtarit një treshe ose një çift më të mirë. A je ende përpara?";
    }

    public static string OutsHelper(HoldemHand h, int seat)
    {
        if (h.BoardCount != 3 && h.BoardCount != 4) return null;
        Seat s = h.Seats[seat];
        var known = new List<Card>(s.Hole);
        known.AddRange(h.VisibleBoard);
        if (HandEvaluator.CategoryOf(HandEvaluator.Evaluate(known)) >= HandCategory.Straight)
            return "Ke tashmë një shkallë ose më mirë.";

        int unseen = 0, outs = 0;
        for (int rank = 2; rank <= 14; rank++)
            for (int suit = 0; suit < 4; suit++)
            {
                var c = new Card(rank, (CardSuit)suit);
                if (known.Contains(c)) continue;
                unseen++;
                var test = new List<Card>(known);
                test.Add(c);
                if (HandEvaluator.CategoryOf(HandEvaluator.Evaluate(test)) >= HandCategory.Straight) outs++;
            }
        int pct = (int)Math.Round(100.0 * outs / unseen);
        var sb = new StringBuilder();
        sb.Append("Letra që të bëjnë shkallë ose më mirë (outs): " + outs + " nga " + unseen + " të paparat (rreth " + pct + "% për letrën tjetër).");
        int toCall = h.ToCall(seat);
        if (toCall > 0)
        {
            int need = (int)Math.Round(100.0 * toCall / (h.Pot + toCall));
            sb.Append("  Të barazosh " + toCall + " në një pot prej " + h.Pot + " do të thotë se të duhen rreth " + need + "%.");
        }
        return sb.ToString();
    }

    public static string DecisiveStreet(HoldemHand h, int winnerSeat, int mySeat)
    {
        Card[] board = h.Board;
        string[] labels = { "Flop", "Turn", "River" };
        for (int i = 0; i < 3; i++)
        {
            int count = 3 + i;
            var w = new List<Card>(h.Seats[winnerSeat].Hole);
            var m = new List<Card>(h.Seats[mySeat].Hole);
            for (int b = 0; b < count; b++) { w.Add(board[b]); m.Add(board[b]); }
            if (HandEvaluator.Evaluate(w) > HandEvaluator.Evaluate(m))
                return i == 0
                    ? "Ishe tashmë pas në Flop."
                    : "Gjithçka ndryshoi në " + labels[i] + " (" + board[count - 1] + ").";
        }
        return null;
    }

    public static string HandSummary(HoldemHand h)
    {
        HandResult r = h.Result;
        Seat me = h.Seats[0];
        var sb = new StringBuilder();

        if (r.ByFold)
        {
            if (me.Folded) sb.Append("Hoqe dorë. Stoku: " + me.Stack + " " + Currency + ".");
            else sb.Append("Të tjerët hoqën dorë. Fiton " + r.Won[0] + " " + Currency + ". Stoku: " + me.Stack + ".");
            return sb.ToString();
        }

        int best = 0;
        for (int i = 1; i < h.Seats.Count; i++) if (r.Won[i] > r.Won[best]) best = i;

        if (r.Won[0] > 0 && r.Net[0] > 0)
        {
            string loser = null;
            for (int i = 1; i < h.Seats.Count; i++)
                if (!h.Seats[i].Folded) { loser = HandName(h, i); break; }
            sb.Append("Fitove " + r.Net[0] + " " + Currency + "! " + HandName(h, 0) + (loser != null ? " fiton ndaj: " + loser : "") + ". Stoku: " + me.Stack + ".");
        }
        else
        {
            string mine = me.Folded ? null : HandName(h, 0);
            sb.Append("Humbe nga: " + HandName(h, best) + ".");
            if (mine != null) sb.Append(" Dora jote (" + mine + ") u mund.");
            string decisive = me.Folded ? null : DecisiveStreet(h, best, 0);
            if (decisive != null) sb.Append(" " + decisive);
            sb.Append(" Stoku: " + me.Stack + " " + Currency + ".");
        }
        return sb.ToString();
    }

    public static string RevealOpponents(HoldemHand h)
    {
        var sb = new StringBuilder();
        for (int i = 1; i < h.Seats.Count; i++)
        {
            Seat s = h.Seats[i];
            if (sb.Length > 0) sb.Append("\n");
            sb.Append(Name(s.Name) + " kishte " + s.Hole[0] + " " + s.Hole[1]);
            if (!s.Folded && !h.Result.ByFold) sb.Append("  (" + HandName(h, i) + ")");
            else if (s.Folded) sb.Append("  (hoqi dorë)");
        }
        return sb.ToString();
    }

    public static string PassMessage(LevelDef level, HoldemHand h)
    {
        Seat me = h.Seats[0];
        LevelText t = Level(level.Number);
        if (level.IsPassed(me.Stack))
        {
            if (level.Number == 2 && me.Folded)
                return "Niveli u kalua. I ruajte " + me.Stack + " " + Currency + " duke hequr dorë kur ishe i mundur.";
            return "Niveli u kalua me " + me.Stack + " " + Currency + ".";
        }
        return "Niveli nuk u kalua. Qëllimi: " + t.Pass + " Përfundove me " + me.Stack + ". Provo të njëjtën dorë; butoni «Ndihmë» mund të të ndihmojë.";
    }

    // ---------------------------------------------------------------------------------------
    // Blackjack
    // ---------------------------------------------------------------------------------------

    public static string BjResult(BlackjackOutcome outcome, int net, int playerTotal, int dealerTotal)
    {
        switch (outcome)
        {
            case BlackjackOutcome.PlayerBlackjack:
                return "Blackjack! Aso + një letër me vlerë 10 është 21 me dy letra, kështu dora mbaron menjëherë (pa Merr ose Qëndro) dhe paguhet 3:2: +" + net + ".";
            case BlackjackOutcome.PlayerWin:
                return dealerTotal > 21
                    ? "Krupieri ia kalon 21 me " + dealerTotal + ". Fiton +" + net + "."
                    : "Fiton: " + playerTotal + " mund " + dealerTotal + ". +" + net + ".";
            case BlackjackOutcome.Push:
                return "Baraspeshë: të dy keni " + playerTotal + ". Basti të kthehet.";
            case BlackjackOutcome.DealerWin:
                return "Krupieri fiton: " + dealerTotal + " mund " + playerTotal + ". " + net + ".";
            case BlackjackOutcome.PlayerBust:
                return "E ke kaluar 21! Totali yt është " + playerTotal + ". " + net + ".";
            case BlackjackOutcome.DealerBlackjack:
                return "Krupieri ka Blackjack. " + net + ".";
            default:
                return "";
        }
    }

    /// <summary>The hint of the scripted Blackjack lesson (hand 0, 1, 2).</summary>
    public static string BjLessonHint(int handIndex)
    {
        switch (handIndex)
        {
            case 0: return "Ke 16 dhe krupieri tregon 10. Të marrësh letër është e rrezikshme, por krupieri nuk heq dorë: duhet ta kalosh totalin e tij.";
            case 1: return "Krupieri tregon 6 dhe duhet të marrë letra derisa të arrijë 17. Kur krupieri ka shans të madh ta kalojë 21, mos e rrezikoj 17-shin tënd.";
            default: return "";
        }
    }

    public static string BjAdvice(BlackjackAdvice advice, int total, bool soft, int dealerUp)
    {
        string basis = "Ke " + (soft ? "një total të butë " : "") + total + " dhe krupieri tregon " + (dealerUp == 11 ? "Aso" : dealerUp.ToString()) + ". ";
        switch (advice)
        {
            case BlackjackAdvice.Double:
                return basis + "Strategjia bazë: dyfisho bastin dhe merr vetëm një letër tjetër.";
            case BlackjackAdvice.Stand:
                return basis + "Strategjia bazë: qëndro. " + (dealerUp <= 6 ? "Krupieri ka shans të madh ta kalojë 21." : "Rreziku për ta kaluar 21 është më i madh se përfitimi.");
            default:
                return basis + "Strategjia bazë: merr një letër. " + (total <= 11 ? "Nuk mund ta kalosh 21 me një letër." : "Krupieri ka një letër të fortë, ndaj nuk mjafton të presësh.");
        }
    }

    // ---------------------------------------------------------------------------------------
    // places and talk
    // ---------------------------------------------------------------------------------------

    public class City
    {
        public string Name, Title, Tagline;
    }

    /// <summary>The seven levels of the tour, in the order of the city wheel (clockwise from the top). Level s is map s - 1.</summary>
    public static readonly City[] Cities =
    {
        new City { Name = "Oda e burrave", Title = "Oda e burrave · Fillimi", Tagline = "Pranë oxhakut, me kafe e raki: këtu fillon udhëtimi." },
        new City { Name = "Shkodër", Title = "Shkodra · Qyteti i Rozafës", Tagline = "Pranë liqenit, nën kështjellën e Rozafës." },
        new City { Name = "Berat", Title = "Berati · Qyteti i njëmijë dritareve", Tagline = "Shtëpi të bardha që ngjiten në kodër." },
        new City { Name = "Prishtinë", Title = "Prishtina · Qyteti i rinisë", Tagline = "Te monumenti NEWBORN, nën kupolat e Bibliotekës Kombëtare." },
        new City { Name = "Tiranë", Title = "Tirana · Kryeqyteti", Tagline = "Te Sheshi Skënderbej luajnë më të mirët e qytetit." },
        new City { Name = "Sarandë", Title = "Saranda · Riviera shqiptare", Tagline = "Perëndimi i diellit mbi detin Jon." },
        new City { Name = "Ulqin", Title = "Ulqini · Finalja", Tagline = "Kalaja mbi Adriatik nën dritën e hënës: këtu caktohet kampioni." }
    };

    /// <summary>A place the player can choose to play in. Id is the number the world builder uses (-1 is the oda).</summary>
    public class MapInfo
    {
        public int Id;
        public string Name, Title, Tagline;
    }

    public static readonly MapInfo[] Maps =
    {
        new MapInfo { Id = -1, Name = "Oda e burrave", Title = Cities[0].Title, Tagline = Cities[0].Tagline },
        new MapInfo { Id = 0, Name = "Shkodër", Title = Cities[1].Title, Tagline = Cities[1].Tagline },
        new MapInfo { Id = 1, Name = "Berat", Title = Cities[2].Title, Tagline = Cities[2].Tagline },
        new MapInfo { Id = 2, Name = "Prishtinë", Title = Cities[3].Title, Tagline = Cities[3].Tagline },
        new MapInfo { Id = 3, Name = "Tiranë", Title = Cities[4].Title, Tagline = Cities[4].Tagline },
        new MapInfo { Id = 4, Name = "Sarandë", Title = Cities[5].Title, Tagline = Cities[5].Tagline },
        new MapInfo { Id = 5, Name = "Ulqin", Title = Cities[6].Title, Tagline = Cities[6].Tagline }
    };

    public const string Welcome = "Mirë se vjen! Kafeja është gati, letrat janë të përziera.";
    public const string Saying = "«Shtëpia e shqiptarit është e Zotit dhe e mikut.»  (nga Kanuni)";

    static readonly string[] TauntRaise = { "Hajde, shiko!", "Po e ngre pak.", "Ke guxim?", "Ky pot është imi!", "Besa-besë, kam letra!" };
    static readonly string[] TauntAllIn = { "Të gjitha në lojë!", "Ose tani, ose kurrë!", "Gjithçka!" };
    static readonly string[] TauntFold = { "Këtë herë jo.", "E lë për ty.", "Më mirë të pres." };
    static readonly string[] TauntWin = { "Faleminderit!", "Fat më të mirë herën tjetër!", "Të pëlqeu kjo? Gëzuar!", "Hë, more mik!" };
    static readonly string[] TauntLose = { "Të lumtë!", "Bravo, ma more.", "Do të ketë edhe një herë." };

    public enum Taunt { Raise, AllIn, Fold, Win, Lose }

    public static string Say(Taunt kind, System.Random rng)
    {
        string[] list;
        switch (kind)
        {
            case Taunt.Raise: list = TauntRaise; break;
            case Taunt.AllIn: list = TauntAllIn; break;
            case Taunt.Fold: list = TauntFold; break;
            case Taunt.Win: list = TauntWin; break;
            default: list = TauntLose; break;
        }
        return list[rng.Next(list.Length)];
    }
}
