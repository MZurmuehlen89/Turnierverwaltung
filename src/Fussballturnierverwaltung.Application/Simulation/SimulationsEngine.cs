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
        int heimTore = ZiehePoisson(paarung.HeimTorerwartung, rng);
        int gastTore = ZiehePoisson(paarung.GastTorerwartung, rng);

        // Ereignisse für Regulärzeit
        var ereignisse = ausfuehrlich
            ? EreignisseGenerieren(heimTore, gastTore, rng, 1, 90)
            : [];

        // Entscheidungsverfahren bei Gleichstand
        int? heimVerl = null, gastVerl = null, heimElf = null, gastElf = null;
        bool? losGewinnerHeim = null;
        var entscheidung = EntscheidungArt.RegulaereSpielzeit;

        if (heimTore == gastTore)
        {
            IReadOnlyList<SimuliertesEreignis> extras;
            (heimVerl, gastVerl, heimElf, gastElf, entscheidung, extras, losGewinnerHeim) =
                VerfahrenKetteAnwenden(paarung, verfahren, ausfuehrlich, rng);
            ereignisse = [.. ereignisse, .. extras];
        }

        // Issue #104: Halbzeitstand aus den generierten Torereignissen ableiten (nur bei ausführlicher
        // Simulation verfügbar, sonst existieren keine Minutenangaben — bleibt dann null statt eines
        // fälschlich vorgetäuschten 0:0). Verlängerungs-Torereignisse liegen immer jenseits Minute 90
        // und fließen hier nie mit ein.
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
    /// nicht her (Issue #383).
    /// </summary>
#pragma warning disable S107 // wie bei Simulieren: Standardwerte, Bündeln hieße alle Aufrufer umstellen
    public static (int? HeimVerl, int? GastVerl, int? HeimElf, int? GastElf,
                   EntscheidungArt Art, IReadOnlyList<SimuliertesEreignis> Ereignisse, bool? LosGewinnerHeim)
        EntscheidungsverfahrenAnwenden(
            Mannschaft heim, Mannschaft auswaerts,
            IReadOnlyList<EntscheidungsVerfahren> verfahren, bool ausfuehrlich, Random rng,
            Spielortvorteil spielortvorteil = Spielortvorteil.Heim, int ueberraschungsfaktor = 0)
#pragma warning restore S107
        => VerfahrenKetteAnwenden(
            new Paarung(heim, auswaerts, spielortvorteil, ueberraschungsfaktor), verfahren, ausfuehrlich, rng);

    private static (int? HeimVerl, int? GastVerl, int? HeimElf, int? GastElf,
                    EntscheidungArt Art, IReadOnlyList<SimuliertesEreignis> Ereignisse, bool? LosGewinnerHeim)
        VerfahrenKetteAnwenden(
            Paarung paarung, IReadOnlyList<EntscheidungsVerfahren> verfahren, bool ausfuehrlich, Random rng)
    {
        int? heimVerl = null, gastVerl = null, heimElf = null, gastElf = null;
        var entscheidung = EntscheidungArt.RegulaereSpielzeit;
        IReadOnlyList<SimuliertesEreignis> ereignisse = [];

        foreach (var v in verfahren.OrderBy(v => v.Reihenfolge))
        {
            var (result, art, hv, gv, he, ge, extraEreignisse) =
                VerfahrenSimulieren(v.VerfahrenArt, paarung, rng, ausfuehrlich);

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
    }

    /// <summary>Ausgang eines einzelnen Verfahrens. <see cref="Vorsprung"/> &gt; 0: Heimsieg, &lt; 0: Gastsieg,
    /// 0: weiter mit dem nächsten Verfahren der Kette.</summary>
    private readonly record struct Verfahrensausgang(
        int Vorsprung, EntscheidungArt Art,
        int? HeimVerl, int? GastVerl, int? HeimElf, int? GastElf,
        IReadOnlyList<SimuliertesEreignis> Ereignisse);

    private static Verfahrensausgang VerfahrenSimulieren(
        VerfahrenArt verfahren, Paarung paarung, Random rng, bool ausfuehrlich)
        => verfahren switch
        {
            VerfahrenArt.VerlaengerungKlassisch => VerlaengerungSimulieren(paarung, rng, ausfuehrlich),
            VerfahrenArt.GoldenGoal             => GoldenGoalSimulieren(paarung, rng, ausfuehrlich),
            VerfahrenArt.SilverGoal             => SilverGoalSimulieren(paarung, rng, ausfuehrlich),
            VerfahrenArt.Elfmeterschiessen      => ElfmeterschiessenAusgang(paarung, rng, ausfuehrlich),
            VerfahrenArt.Losentscheid           => LosAusgang(EntscheidungArt.Losentscheid, rng),
            VerfahrenArt.Muenzwurf              => LosAusgang(EntscheidungArt.Muenzwurf, rng),
            _ => new(0, EntscheidungArt.RegulaereSpielzeit, null, null, null, null, [])
        };

    private static Verfahrensausgang VerlaengerungSimulieren(Paarung paarung, Random rng, bool ausfuehrlich)
    {
        int hv = ZiehePoisson(paarung.HeimTorerwartung * (30.0 / 90.0), rng);
        int gv = ZiehePoisson(paarung.GastTorerwartung * (30.0 / 90.0), rng);
        var ereignisse = ausfuehrlich ? EreignisseGenerieren(hv, gv, rng, 91, 120) : [];
        return new(hv - gv, EntscheidungArt.Verlaengerung, hv, gv, null, null, ereignisse);
    }

    /// <summary>Minute für Minute bis zum ersten Tor, höchstens 30 Minuten.</summary>
    private static Verfahrensausgang GoldenGoalSimulieren(Paarung paarung, Random rng, bool ausfuehrlich)
    {
        double heimJeMinute = paarung.HeimTorerwartung / 90.0;
        double gastJeMinute = paarung.GastTorerwartung / 90.0;
        for (int minute = 91; minute <= 120; minute++)
        {
            bool h = rng.NextDouble() < heimJeMinute;
            bool g = rng.NextDouble() < gastJeMinute;
            if (!h && !g) continue;

            // Treffen beide in derselben Minute, entscheidet das Los, wer zuerst traf — bis #629 zählte
            // stets das Heimtor.
            bool heimTrifft = h && (!g || rng.Next(2) == 0);
            IReadOnlyList<SimuliertesEreignis> ereignisse = ausfuehrlich
                ? [new(minute, EreignisArt.GoldenGoal, heimTrifft, "Golden Goal")]
                : [];
            return heimTrifft
                ? new(1,  EntscheidungArt.GoldenGoal, 1, 0, null, null, ereignisse)
                : new(-1, EntscheidungArt.GoldenGoal, 0, 1, null, null, ereignisse);
        }
        return new(0, EntscheidungArt.GoldenGoal, 0, 0, null, null, []);
    }

    /// <summary>Silbernes Tor: Führt ein Team nach der ersten Verlängerungshälfte (15 Min.), endet das Spiel
    /// sofort; bei Gleichstand wird die zweite Hälfte (weitere 15 Min.) gespielt.</summary>
    private static Verfahrensausgang SilverGoalSimulieren(Paarung paarung, Random rng, bool ausfuehrlich)
    {
        double heimJeHaelfte = paarung.HeimTorerwartung * (15.0 / 90.0);
        double gastJeHaelfte = paarung.GastTorerwartung * (15.0 / 90.0);

        int hv1 = ZiehePoisson(heimJeHaelfte, rng);
        int gv1 = ZiehePoisson(gastJeHaelfte, rng);
        var ereignisse = ausfuehrlich ? EreignisseGenerieren(hv1, gv1, rng, 91, 105) : [];

        if (hv1 != gv1)
            return new(hv1 - gv1, EntscheidungArt.SilverGoal, hv1, gv1, null, null, ereignisse);

        // #391: Ein Sieg erst in der zweiten Hälfte ist ein gewöhnlicher Sieg nach Verlängerung.
        int hv2 = ZiehePoisson(heimJeHaelfte, rng);
        int gv2 = ZiehePoisson(gastJeHaelfte, rng);
        if (ausfuehrlich)
            ereignisse = [.. ereignisse, .. EreignisseGenerieren(hv2, gv2, rng, 106, 120)];

        return new(hv2 - gv2, EntscheidungArt.Verlaengerung, hv1 + hv2, gv1 + gv2, null, null, ereignisse);
    }

    private static Verfahrensausgang ElfmeterschiessenAusgang(Paarung paarung, Random rng, bool ausfuehrlich)
    {
        var (he, ge, ereignisse) = Elfmeterschiessen.Simulieren(paarung.Heim, paarung.Auswaerts, rng, ausfuehrlich);
        return new(he - ge, EntscheidungArt.Elfmeterschiessen, null, null, he, ge, ereignisse);
    }

    private static Verfahrensausgang LosAusgang(EntscheidungArt art, Random rng)
        => new(rng.Next(2) == 0 ? 1 : -1, art, null, null, null, null, []);

    // ── Ausführliche Ereignisse ───────────────────────────────────────────

    private static IReadOnlyList<SimuliertesEreignis> EreignisseGenerieren(
        int heimTore, int gastTore,
        Random rng, int vonMinute, int bisMinute)
    {
        int spanne = bisMinute - vonMinute + 1;
        var ereignisse = new List<SimuliertesEreignis>();

        // Tore: zufällige Minuten zuweisen
        var heimMinuten = ZufallsMinuten(heimTore, vonMinute, bisMinute, rng);
        var gastMinuten = ZufallsMinuten(gastTore, vonMinute, bisMinute, rng);

        foreach (var min in heimMinuten)
            ereignisse.Add(new(min, EreignisArt.Tor, true, ZufaelligerTorstilText(rng)));
        foreach (var min in gastMinuten)
            ereignisse.Add(new(min, EreignisArt.Tor, false, ZufaelligerTorstilText(rng)));

        // Gelbe Karten: ~2,5 pro Team bei 90 Min., anteilig für kürzere Zeiträume
        double basisGelbe = 2.5 * spanne / 90.0;
        int heimGelbe = ZiehePoisson(basisGelbe, rng);
        int gastGelbe = ZiehePoisson(basisGelbe, rng);

        foreach (var min in ZufallsMinuten(heimGelbe, vonMinute, bisMinute, rng))
            ereignisse.Add(new(min, EreignisArt.GelbeKarte, true, "Gelbe Karte"));
        foreach (var min in ZufallsMinuten(gastGelbe, vonMinute, bisMinute, rng))
            ereignisse.Add(new(min, EreignisArt.GelbeKarte, false, "Gelbe Karte"));

        // Rote Karten: ~0,15 pro Team bei 90 Min.
        double basisRote = 0.15 * spanne / 90.0;
        if (ZiehePoisson(basisRote, rng) > 0)
            ereignisse.Add(new(rng.Next(vonMinute, bisMinute + 1), EreignisArt.RoteKarte, true, "Rote Karte"));
        if (ZiehePoisson(basisRote, rng) > 0)
            ereignisse.Add(new(rng.Next(vonMinute, bisMinute + 1), EreignisArt.RoteKarte, false, "Rote Karte"));

        ereignisse.Sort((a, b) => a.Minute.CompareTo(b.Minute));
        return ereignisse;
    }

    // Issue #126: reine Text-/Flavor-Anreicherung des Tor-Ereignisses, kein Einfluss auf Spielstand
    // oder Simulationslogik. Auswahl angelehnt an das Attribut "Schussart II" im DFL-Definitionskatalog
    // "Offizielle Spieldaten" (2189_original.pdf), das u.a. Fallrückzieher, Hacke, Chip und Schlenzer führt.
    private static readonly string[] _torstile =
    [
        "Kopfball", "Flachschuss", "Volleyschuss", "Fallrückzieher", "Hacke",
        "Chipball", "Schlenzer", "Abstauber", "Distanzschuss", "Direktabnahme"
    ];

    // Issue #127: ebenfalls reine Flavor-Anreicherung, kein Einfluss auf Spielstand oder
    // Tor-Wahrscheinlichkeit. Realistischer Anteil von Kontertoren an allen Toren im Amateur-/
    // Profifußball liegt grob im niedrigen zweistelligen Prozentbereich.
    private const double KontertorWahrscheinlichkeit = 0.15;

    // Issue #128: ebenfalls reine Flavor-Anreicherung, kein Einfluss auf Spielstand oder
    // Simulationslogik. Grobes 3x3-Raster angelehnt an die "Torzone" im DFL-Definitionskatalog
    // "Offizielle Spieldaten" (Abbildung 5) — Mitte/Mitte bewusst als "Mitte" statt "Mitte Mitte".
    private static readonly string[] _torzonen =
    [
        "oben links", "oben Mitte", "oben rechts",
        "Mitte links", "Mitte",      "Mitte rechts",
        "unten links", "unten Mitte", "unten rechts"
    ];

    private static string ZufaelligerTorstilText(Random rng)
    {
        string basis = $"Tor per {_torstile[rng.Next(_torstile.Length)]}";
        string mitKonter = rng.NextDouble() < KontertorWahrscheinlichkeit ? $"{basis} nach Konter" : basis;
        return $"{mitKonter} ({_torzonen[rng.Next(_torzonen.Length)]})";
    }

    private static List<int> ZufallsMinuten(int anzahl, int von, int bis, Random rng)
    {
        var minuten = new List<int>(anzahl);
        for (int i = 0; i < anzahl; i++)
            minuten.Add(rng.Next(von, bis + 1));
        minuten.Sort();
        return minuten;
    }
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
