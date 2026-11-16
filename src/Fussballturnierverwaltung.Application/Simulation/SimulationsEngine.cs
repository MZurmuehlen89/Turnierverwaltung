using Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Simulation;

/// <summary>
/// Zustandsloser Kern der Simulations-Engine. Kein DI, kein I/O — reine Berechnung.
/// Alle Zufallszahlen kommen vom übergebenen Random (reproduzierbar über Seed).
/// </summary>
public static class SimulationsEngine
{
    // ── Öffentliche Eintrittspunkte ────────────────────────────────────────

#pragma warning disable S107 // zwei Parameter mit Standardwerten; Bündeln hieße alle Aufrufer und Tests umstellen, intern läuft es als Paarung (#503)
    public static SimulationsErgebnis Simulieren(
        Mannschaft heim,
        Mannschaft auswaerts,
        IReadOnlyList<EntscheidungsVerfahren> verfahren,
        bool ausfuehrlich,
        Random rng,
        Spielortvorteil spielortvorteil = Spielortvorteil.Heim,
        int ueberraschungsfaktor = 0)
#pragma warning restore S107
    {
        var paarung = new Paarung(heim, auswaerts, spielortvorteil, ueberraschungsfaktor);

        // #361: ausführlich Minute für Minute mit Spielzustand, sonst reines Endergebnis per Poisson.
        var ablauf = ausfuehrlich ? paarung.Anpfiff(rng) : null;
        var (heimTore, gastTore, ereignisse) = ablauf?.RegulaereSpielzeit()
            ?? new(ZiehePoisson(paarung.HeimTorerwartung, rng), ZiehePoisson(paarung.GastTorerwartung, rng), []);

        // Entscheidungsverfahren bei Gleichstand
        int? heimVerl = null, gastVerl = null, heimElf = null, gastElf = null;
        bool? losGewinnerHeim = null;
        var entscheidung = EntscheidungArt.RegulaereSpielzeit;

        if (heimTore == gastTore)
        {
            IReadOnlyList<SimuliertesEreignis> extras;
            (heimVerl, gastVerl, heimElf, gastElf, entscheidung, extras, losGewinnerHeim) =
                VerfahrenKetteAnwenden(paarung, verfahren, ablauf, rng);
            ereignisse = [.. ereignisse, .. extras];
        }

        // Issue #104: Halbzeitstand aus den generierten Torereignissen ableiten (nur bei ausführlicher
        // Simulation verfügbar, sonst existieren keine Minutenangaben — bleibt dann null statt eines
        // fälschlich vorgetäuschten 0:0). Verlängerungs-Torereignisse liegen immer jenseits Minute 90
        // und fließen hier nie mit ein; Tore in der Nachspielzeit („45+2'“) tragen Minute 45 und zählen mit (#361).
        int? heimHalbzeit = ausfuehrlich
            ? ereignisse.Count(e => e.Art == EreignisArt.Tor && e.IstHeim && e.Minute <= 45)
            : null;
        int? gastHalbzeit = ausfuehrlich
            ? ereignisse.Count(e => e.Art == EreignisArt.Tor && !e.IstHeim && e.Minute <= 45)
            : null;

        return new SimulationsErgebnis(
            heimTore, gastTore, heimVerl, gastVerl, heimElf, gastElf,
            entscheidung, ereignisse, heimHalbzeit, gastHalbzeit, losGewinnerHeim);
    }

    /// <summary>
    /// Wendet die Entscheidungsverfahren-Kette (Verlängerung, Elfmeterschießen, …) direkt an, ohne
    /// vorher eine reguläre Spielzeit zu würfeln — aus <see cref="Simulieren"/> extrahiert (dort bei
    /// Gleichstand nach 90 Minuten genutzt), zusätzlich öffentlich für die freie Hin-/Rückspiel-
    /// Simulation (Issue #237): dort entscheidet erst das Aggregat aus beiden Beinen, ob überhaupt
    /// eine Verlängerung fällig ist — das darf nicht (wie zuvor) blind pro Bein einzeln geprüft werden.
    /// <c>LosGewinnerHeim</c> ist nur bei Losentscheid/Münzwurf gesetzt — dort geben die Tore den Sieger
    /// nicht her (Issue #383). <paramref name="platzverweise"/>: Platzverweise des Spiels, an das die Verlängerung
    /// anschließt (#361) – im Rückspiel die des Rückspiels; die Ermüdung setzt nach 90 Minuten fort.
    /// </summary>
#pragma warning disable S107 // wie bei Simulieren: Standardwerte, Bündeln hieße alle Aufrufer umstellen
    public static (int? HeimVerl, int? GastVerl, int? HeimElf, int? GastElf,
                   EntscheidungArt Art, IReadOnlyList<SimuliertesEreignis> Ereignisse, bool? LosGewinnerHeim)
        EntscheidungsverfahrenAnwenden(
            Mannschaft heim, Mannschaft auswaerts,
            IReadOnlyList<EntscheidungsVerfahren> verfahren, bool ausfuehrlich, Random rng,
            Spielortvorteil spielortvorteil = Spielortvorteil.Heim, int ueberraschungsfaktor = 0,
            Platzverweise platzverweise = default)
#pragma warning restore S107
    {
        var paarung = new Paarung(heim, auswaerts, spielortvorteil, ueberraschungsfaktor);
        var ablauf = ausfuehrlich ? paarung.Anpfiff(rng, platzverweise, gespielteMinuten: 90) : null;
        return VerfahrenKetteAnwenden(paarung, verfahren, ablauf, rng);
    }

    private static (int? HeimVerl, int? GastVerl, int? HeimElf, int? GastElf,
                    EntscheidungArt Art, IReadOnlyList<SimuliertesEreignis> Ereignisse, bool? LosGewinnerHeim)
        VerfahrenKetteAnwenden(
            Paarung paarung, IReadOnlyList<EntscheidungsVerfahren> verfahren, Spielablauf? ablauf, Random rng)
    {
        int? heimVerl = null, gastVerl = null, heimElf = null, gastElf = null;
        var entscheidung = EntscheidungArt.RegulaereSpielzeit;
        IReadOnlyList<SimuliertesEreignis> ereignisse = [];

        foreach (var v in verfahren.OrderBy(v => v.Reihenfolge))
        {
            var (result, art, hv, gv, he, ge, extraEreignisse) =
                VerfahrenSimulieren(v.VerfahrenArt, paarung, rng, ablauf);

            entscheidung = art;
            // Verlängerungstore aus einem früheren Schritt (z.B. VerlaengerungKlassisch)
            // nicht mit null überschreiben, wenn der Folgeschritt (z.B. Elfmeter) null zurückgibt
            if (hv.HasValue) heimVerl = hv;
            if (gv.HasValue) gastVerl = gv;
            if (he.HasValue) heimElf = he;
            if (ge.HasValue) gastElf = ge;
            ereignisse = [.. ereignisse, .. extraEreignisse];

            if (result != 0) // Sieger gefunden
            {
                bool? losGewinnerHeim = art is EntscheidungArt.Losentscheid or EntscheidungArt.Muenzwurf
                    ? result > 0
                    : null;
                return (heimVerl, gastVerl, heimElf, gastElf, entscheidung, ereignisse, losGewinnerHeim);
            }
        }

        return (heimVerl, gastVerl, heimElf, gastElf, entscheidung, ereignisse, null);
    }

    /// <summary>Issue #352: Ortsvorteil eines Spiels. Außerhalb neutraler Runden hat immer das Heimteam den
    /// Vorteil; auf neutralem Platz nur ein Gastgeber des Turniers — auch wenn er nominell als Gast angesetzt
    /// ist. Treffen zwei Gastgeber (oder zwei Nicht-Gastgeber) aufeinander, bleibt der Platz neutral.</summary>
    public static Spielortvorteil SpielortvorteilErmitteln(
        bool neutralerSpielort, int? heimId, int? auswaertsId, IReadOnlyCollection<int> gastgeberIds)
    {
        if (!neutralerSpielort) return Spielortvorteil.Heim;
        bool heimIstGastgeber      = heimId is int h && gastgeberIds.Contains(h);
        bool auswaertsIstGastgeber = auswaertsId is int a && gastgeberIds.Contains(a);
        return (heimIstGastgeber, auswaertsIstGastgeber) switch
        {
            (true, false) => Spielortvorteil.Heim,
            (false, true) => Spielortvorteil.Auswaerts,
            _             => Spielortvorteil.Neutral
        };
    }

    // ── Expected-Goals-Formel ──────────────────────────────────────────────

    private static double BerechneErwartetesTore(
        Mannschaft angreifer, Mannschaft verteidiger, bool istHeim, Spielortvorteil spielortvorteil,
        int ueberraschungsfaktor)
    {
        double angriff  = (angreifer.OffensivRating  * 0.6 + angreifer.MittelfeldRating  * 0.4) / 100.0;
        double verteid  = (verteidiger.DefensivRating * 0.6 + verteidiger.MittelfeldRating * 0.4) / 100.0;
        // Issue #352: auf neutralem Platz der Mittelwert aus Heim- und Auswärtsstärke statt eines festen 1,0 —
        // die Stärken liegen typisch bei 65–75, ein fester Faktor würde die Torerwartung deutlich aufblähen.
        double ortFaktor = spielortvorteil switch
        {
            Spielortvorteil.Neutral   => (angreifer.Heimstaerke + angreifer.Auswaertsstaerke) / 200.0,
            Spielortvorteil.Auswaerts => (istHeim ? angreifer.Auswaertsstaerke : angreifer.Heimstaerke) / 100.0,
            _                         => (istHeim ? angreifer.Heimstaerke : angreifer.Auswaertsstaerke) / 100.0
        };

        // Issue #404: Kräfteverhältnis mit Exponent gespreizt — linear gestaucht gewannen Außenseiter viel zu oft
        // (≈ 6 statt real 1–4 ausgeschiedene Bundesligisten in DFB-Pokal-Runde 1) bei nur ~2,1 Toren je Spiel.
        // Issue #360: der Überraschungsfaktor senkt die Spreizung anteilig — bei 100 % zählt nur noch der Spielort.
        double spreizung = STAERKE_SPREIZUNG * (1 - Math.Clamp(ueberraschungsfaktor, 0, 100) / 100.0);
        double lambda = TOR_GRUNDERWARTUNG
                        * Math.Pow(angriff / Math.Max(0.3, verteid), spreizung)
                        * ortFaktor;
        return Math.Clamp(lambda, MIN_TORERWARTUNG, MAX_TORERWARTUNG);
    }

    // Issue #404: per Monte-Carlo an der Bundesliga kalibriert (Seed-Ratings) — ~3,1 Tore je Spiel, ~43 % Heimsiege,
    // Meister ~79 Punkte, ~2,8 ausgeschiedene Bundesligisten in Pokalrunde 1. Abgesichert durch SimulationsKalibrierungTests.
    private const double TOR_GRUNDERWARTUNG = 1.95;
    private const double STAERKE_SPREIZUNG  = 2.2;
    private const double MIN_TORERWARTUNG   = 0.1;
    private const double MAX_TORERWARTUNG   = 5.0;

    // ── Poisson-Ziehung (Knuth) ───────────────────────────────────────────

    private static int ZiehePoisson(double lambda, Random rng)
    {
        double L = Math.Exp(-Math.Min(lambda, 20.0));
        double p = 1.0;
        int k = 0;
        do { k++; p *= rng.NextDouble(); } while (p > L && k < 25);
        return k - 1;
    }

    // ── Verfahren bei Gleichstand ──────────────────────────────────────────

    /// <summary>Die beiden Mannschaften samt Rahmen des Spiels — jedes Verfahren braucht alle vier Angaben.</summary>
    private readonly record struct Paarung(
        Mannschaft Heim, Mannschaft Auswaerts, Spielortvorteil Spielortvorteil, int Ueberraschungsfaktor)
    {
        public double HeimTorerwartung =>
            BerechneErwartetesTore(Heim, Auswaerts, istHeim: true, Spielortvorteil, Ueberraschungsfaktor);

        public double GastTorerwartung =>
            BerechneErwartetesTore(Auswaerts, Heim, istHeim: false, Spielortvorteil, Ueberraschungsfaktor);

        public Spielablauf Anpfiff(Random rng, Platzverweise platzverweise = default, int gespielteMinuten = 0) =>
            new(HeimTorerwartung, GastTorerwartung, rng, platzverweise, gespielteMinuten);
    }

    /// <summary>Ausgang eines einzelnen Verfahrens. <see cref="Vorsprung"/> &gt; 0: Heimsieg, &lt; 0: Gastsieg,
    /// 0: weiter mit dem nächsten Verfahren der Kette.</summary>
    private readonly record struct Verfahrensausgang(
        int Vorsprung, EntscheidungArt Art,
        int? HeimVerl, int? GastVerl, int? HeimElf, int? GastElf,
        IReadOnlyList<SimuliertesEreignis> Ereignisse);

    /// <summary>Mit <paramref name="ablauf"/> (ausführlich) wird Minute für Minute weitergespielt (#361), ohne ihn
    /// per Poisson wie zuvor – dieser Pfad bleibt unverändert.</summary>
    private static Verfahrensausgang VerfahrenSimulieren(
        VerfahrenArt verfahren, Paarung paarung, Random rng, Spielablauf? ablauf)
        => verfahren switch
        {
            VerfahrenArt.VerlaengerungKlassisch => ablauf is null
                ? VerlaengerungSimulieren(paarung, rng)
                : Verlaengerungsausgang(EntscheidungArt.Verlaengerung, ablauf.Verlaengerung()),
            VerfahrenArt.GoldenGoal => ablauf is null
                ? GoldenGoalSimulieren(paarung, rng)
                : Verlaengerungsausgang(EntscheidungArt.GoldenGoal, ablauf.GoldenGoal()),
            VerfahrenArt.SilverGoal => ablauf is null
                ? SilverGoalSimulieren(paarung, rng)
                : SilverGoalAusgang(ablauf),
            VerfahrenArt.Elfmeterschiessen => ElfmeterschiessenAusgang(paarung, rng, ausfuehrlich: ablauf is not null),
            VerfahrenArt.Losentscheid      => LosAusgang(EntscheidungArt.Losentscheid, rng),
            VerfahrenArt.Muenzwurf         => LosAusgang(EntscheidungArt.Muenzwurf, rng),
            _ => new(0, EntscheidungArt.RegulaereSpielzeit, null, null, null, null, [])
        };

    private static Verfahrensausgang Verlaengerungsausgang(EntscheidungArt art, Spielablauf.Abschnitt abschnitt)
        => new(abschnitt.HeimTore - abschnitt.GastTore, art,
               abschnitt.HeimTore, abschnitt.GastTore, null, null, abschnitt.Ereignisse);

    /// <summary>Silbernes Tor, ausführlich: wie <see cref="SilverGoalSimulieren"/>, nur Minute für Minute.</summary>
    private static Verfahrensausgang SilverGoalAusgang(Spielablauf ablauf)
    {
        var erste = ablauf.ErsteVerlaengerungshaelfte();
        if (erste.HeimTore != erste.GastTore)
            return Verlaengerungsausgang(EntscheidungArt.SilverGoal, erste);

        // #391: Ein Sieg erst in der zweiten Hälfte ist ein gewöhnlicher Sieg nach Verlängerung.
        var zweite = ablauf.ZweiteVerlaengerungshaelfte();
        return new(zweite.HeimTore - zweite.GastTore, EntscheidungArt.Verlaengerung,
                   erste.HeimTore + zweite.HeimTore, erste.GastTore + zweite.GastTore, null, null,
                   [.. erste.Ereignisse, .. zweite.Ereignisse]);
    }

    // ── Schnellsimulation per Poisson (unverändert seit vor #361) ─────────

    private static Verfahrensausgang VerlaengerungSimulieren(Paarung paarung, Random rng)
    {
        int hv = ZiehePoisson(paarung.HeimTorerwartung * (30.0 / 90.0), rng);
        int gv = ZiehePoisson(paarung.GastTorerwartung * (30.0 / 90.0), rng);
        return new(hv - gv, EntscheidungArt.Verlaengerung, hv, gv, null, null, []);
    }

    /// <summary>Minute für Minute bis zum ersten Tor, höchstens 30 Minuten.</summary>
    private static Verfahrensausgang GoldenGoalSimulieren(Paarung paarung, Random rng)
    {
        double heimJeMinute = paarung.HeimTorerwartung / 90.0;
        double gastJeMinute = paarung.GastTorerwartung / 90.0;
        for (int minute = 91; minute <= 120; minute++)
        {
            bool h = rng.NextDouble() < heimJeMinute;
            bool g = rng.NextDouble() < gastJeMinute;
            if (!h && !g) continue;

            // Treffen beide in derselben Minute, entscheidet das Los, wer zuerst traf — bis #629 zählte
            // stets das Heimtor. Der ausführliche Pfad würfelt seit #361 nach dem Anteil an der Torerwartung.
            bool heimTrifft = h && (!g || rng.Next(2) == 0);
            return heimTrifft
                ? new(1,  EntscheidungArt.GoldenGoal, 1, 0, null, null, [])
                : new(-1, EntscheidungArt.GoldenGoal, 0, 1, null, null, []);
        }
        return new(0, EntscheidungArt.GoldenGoal, 0, 0, null, null, []);
    }

    /// <summary>Silbernes Tor: Führt ein Team nach der ersten Verlängerungshälfte (15 Min.), endet das Spiel
    /// sofort; bei Gleichstand wird die zweite Hälfte (weitere 15 Min.) gespielt.</summary>
    private static Verfahrensausgang SilverGoalSimulieren(Paarung paarung, Random rng)
    {
        double heimJeHaelfte = paarung.HeimTorerwartung * (15.0 / 90.0);
        double gastJeHaelfte = paarung.GastTorerwartung * (15.0 / 90.0);

        int hv1 = ZiehePoisson(heimJeHaelfte, rng);
        int gv1 = ZiehePoisson(gastJeHaelfte, rng);
        if (hv1 != gv1)
            return new(hv1 - gv1, EntscheidungArt.SilverGoal, hv1, gv1, null, null, []);

        // #391: Ein Sieg erst in der zweiten Hälfte ist ein gewöhnlicher Sieg nach Verlängerung.
        int hv2 = ZiehePoisson(heimJeHaelfte, rng);
        int gv2 = ZiehePoisson(gastJeHaelfte, rng);
        return new(hv2 - gv2, EntscheidungArt.Verlaengerung, hv1 + hv2, gv1 + gv2, null, null, []);
    }

    private static Verfahrensausgang ElfmeterschiessenAusgang(Paarung paarung, Random rng, bool ausfuehrlich)
    {
        var (he, ge, ereignisse) = Elfmeterschiessen.Simulieren(paarung.Heim, paarung.Auswaerts, rng, ausfuehrlich);
        return new(he - ge, EntscheidungArt.Elfmeterschiessen, null, null, he, ge, ereignisse);
    }

    private static Verfahrensausgang LosAusgang(EntscheidungArt art, Random rng)
        => new(rng.Next(2) == 0 ? 1 : -1, art, null, null, null, null, []);
}

// ── DTOs ─────────────────────────────────────────────────────────────────

public record SimulationsErgebnis(
    int HeimToreRegulaer,
    int AuswaertsToreRegulaer,
    int? HeimToreVerlaengerung,
    int? AuswaertsToreVerlaengerung,
    int? HeimElfmetertore,
    int? AuswaertsElfmetertore,
    EntscheidungArt EntscheidungArt,
    IReadOnlyList<SimuliertesEreignis> Ereignisse,
    int? HalbzeitHeimTore = null,
    int? HalbzeitAuswaertsTore = null,
    // Issue #383: Sieger eines Losentscheids/Münzwurfs (true = Heim) — sonst null, dort folgt er aus den Toren.
    bool? LosGewinnerHeim = null);

/// <summary>Issue #352: wer im Spiel den Ortsvorteil hat — siehe
/// <see cref="SimulationsEngine.SpielortvorteilErmitteln"/>.</summary>
public enum Spielortvorteil
{
    Heim,
    Neutral,
    Auswaerts
}

/// <summary>Ein Ereignis der ausführlichen Simulation. <see cref="Nachspielminute"/> ist nur in der Nachspielzeit
/// gesetzt (#361): „90+4'“ = Minute 90, Nachspielminute 4.</summary>
public record SimuliertesEreignis(
    int Minute,
    EreignisArt Art,
    bool IstHeim,
    string Beschreibung,
    int? Nachspielminute = null);
