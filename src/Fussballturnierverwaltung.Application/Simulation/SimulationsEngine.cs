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

    public static SimulationsErgebnis Simulieren(
        Mannschaft heim,
        Mannschaft auswaerts,
        IReadOnlyList<EntscheidungsVerfahren> verfahren,
        bool ausfuehrlich,
        Random rng)
    {
        double heimLambda = BerechneErwartetesTore(heim, auswaerts, istHeim: true);
        double gastLambda = BerechneErwartetesTore(auswaerts, heim, istHeim: false);

        int heimTore = ZiehePoisson(heimLambda, rng);
        int gastTore = ZiehePoisson(gastLambda, rng);

        // Ereignisse für Regulärzeit
        var ereignisse = ausfuehrlich
            ? EreignisseGenerieren(heim, auswaerts, heimTore, gastTore, rng, 1, 90)
            : [];

        // Entscheidungsverfahren bei Gleichstand
        int? heimVerl = null, gastVerl = null, heimElf = null, gastElf = null;
        var entscheidung = EntscheidungArt.RegulaereSpielzeit;

        if (heimTore == gastTore)
        {
            foreach (var v in verfahren.OrderBy(v => v.Reihenfolge))
            {
                var (result, art, hv, gv, he, ge, extraEreignisse) =
                    VerfahrenSimulieren(v.VerfahrenArt, heim, auswaerts, rng, ausfuehrlich);

                entscheidung = art;
                // Verlängerungstore aus einem früheren Schritt (z.B. VerlaengerungKlassisch)
                // nicht mit null überschreiben, wenn der Folgeschritt (z.B. Elfmeter) null zurückgibt
                if (hv.HasValue) heimVerl = hv;
                if (gv.HasValue) gastVerl = gv;
                if (he.HasValue) heimElf = he;
                if (ge.HasValue) gastElf = ge;
                ereignisse = [.. ereignisse, .. extraEreignisse];

                if (result != 0) break; // Sieger gefunden
            }
        }

        return new SimulationsErgebnis(
            heimTore, gastTore, heimVerl, gastVerl, heimElf, gastElf,
            entscheidung, ereignisse);
    }

    // ── Expected-Goals-Formel ──────────────────────────────────────────────

    private static double BerechneErwartetesTore(Mannschaft angreifer, Mannschaft verteidiger, bool istHeim)
    {
        double angriff  = (angreifer.OffensivRating  * 0.6 + angreifer.MittelfeldRating  * 0.4) / 100.0;
        double verteid  = (verteidiger.DefensivRating * 0.6 + verteidiger.MittelfeldRating * 0.4) / 100.0;
        double ortFaktor = istHeim
            ? angreifer.Heimstaerke     / 100.0
            : angreifer.Auswaertsstaerke / 100.0;

        // Grunderwartung ~1.35 Tore pro Team; Angriff vs. Verteidigung + Heimvorteil
        double lambda = 1.35 * (angriff / Math.Max(0.3, verteid)) * ortFaktor;
        return Math.Clamp(lambda, 0.25, 5.0);
    }

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

    private static (int Result, EntscheidungArt Art,
                    int? HV, int? GV, int? HE, int? GE,
                    IReadOnlyList<SimuliertesEreignis> Extras)
        VerfahrenSimulieren(
            VerfahrenArt verfahren, Mannschaft heim, Mannschaft auswaerts,
            Random rng, bool ausfuehrlich)
    {
        switch (verfahren)
        {
            case VerfahrenArt.VerlaengerungKlassisch:
            {
                double hL = BerechneErwartetesTore(heim, auswaerts, istHeim: true)  * (30.0 / 90.0);
                double gL = BerechneErwartetesTore(auswaerts, heim, istHeim: false) * (30.0 / 90.0);
                int hv = ZiehePoisson(hL, rng);
                int gv = ZiehePoisson(gL, rng);
                var extras = ausfuehrlich
                    ? EreignisseGenerieren(heim, auswaerts, hv, gv, rng, 91, 120)
                    : (IReadOnlyList<SimuliertesEreignis>)[];
                return (hv - gv, EntscheidungArt.Verlaengerung, hv, gv, null, null, extras);
            }

            case VerfahrenArt.GoldenGoal:
            {
                // Simuliere bis zum ersten Tor (max. 30 Min.)
                double hProbPerMin = BerechneErwartetesTore(heim, auswaerts, istHeim: true)  / 90.0;
                double gProbPerMin = BerechneErwartetesTore(auswaerts, heim, istHeim: false) / 90.0;
                int hv = 0, gv = 0;
                var extras = new List<SimuliertesEreignis>();
                for (int min = 91; min <= 120; min++)
                {
                    bool h = rng.NextDouble() < hProbPerMin;
                    bool g = rng.NextDouble() < gProbPerMin;
                    if (h) { hv++; if (ausfuehrlich) extras.Add(new(min, EreignisArt.GoldenGoal, true, "Golden Goal")); break; }
                    if (g) { gv++; if (ausfuehrlich) extras.Add(new(min, EreignisArt.GoldenGoal, false, "Golden Goal")); break; }
                }
                return (hv - gv, EntscheidungArt.GoldenGoal, hv, gv, null, null, extras);
            }

            case VerfahrenArt.SilverGoal:
            {
                // Silbernes Tor: Führt ein Team nach der ersten Verlängerungshälfte (15 Min.), endet
                // das Spiel sofort; bei Gleichstand wird die zweite Hälfte (weitere 15 Min.) gespielt.
                double hL15 = BerechneErwartetesTore(heim, auswaerts, istHeim: true)  * (15.0 / 90.0);
                double gL15 = BerechneErwartetesTore(auswaerts, heim, istHeim: false) * (15.0 / 90.0);

                int hv1 = ZiehePoisson(hL15, rng);
                int gv1 = ZiehePoisson(gL15, rng);
                var extras = ausfuehrlich
                    ? EreignisseGenerieren(heim, auswaerts, hv1, gv1, rng, 91, 105)
                    : (IReadOnlyList<SimuliertesEreignis>)[];

                int hv = hv1, gv = gv1;
                if (hv1 == gv1)
                {
                    int hv2 = ZiehePoisson(hL15, rng);
                    int gv2 = ZiehePoisson(gL15, rng);
                    if (ausfuehrlich)
                        extras = [.. extras, .. EreignisseGenerieren(heim, auswaerts, hv2, gv2, rng, 106, 120)];
                    hv += hv2;
                    gv += gv2;
                }

                return (hv - gv, EntscheidungArt.SilverGoal, hv, gv, null, null, extras);
            }

            case VerfahrenArt.Elfmeterschiessen:
            {
                var (he, ge) = ElfmeterschiessenSimulieren(heim, auswaerts, rng);
                return (he - ge, EntscheidungArt.Elfmeterschiessen, null, null, he, ge, []);
            }

            case VerfahrenArt.Losentscheid:
                return (rng.Next(2) == 0 ? 1 : -1, EntscheidungArt.Losentscheid, null, null, null, null, []);

            case VerfahrenArt.Muenzwurf:
                return (rng.Next(2) == 0 ? 1 : -1, EntscheidungArt.Muenzwurf, null, null, null, null, []);

            default:
                return (0, EntscheidungArt.RegulaereSpielzeit, null, null, null, null, []);
        }
    }

    // ── Elfmeterschießen ──────────────────────────────────────────────────

    private static (int Heim, int Gast) ElfmeterschiessenSimulieren(
        Mannschaft heim, Mannschaft auswaerts, Random rng)
    {
        // Trefferwahrscheinlichkeit: Basis 73 % + leichte Anpassung über Offensivrating
        double pHeim = 0.73 + (heim.OffensivRating     - 70) * 0.002;
        double pGast = 0.73 + (auswaerts.OffensivRating - 70) * 0.002;
        pHeim = Math.Clamp(pHeim, 0.5, 0.92);
        pGast = Math.Clamp(pGast, 0.5, 0.92);

        int h = 0, g = 0;
        for (int i = 0; i < 5; i++)
        {
            if (rng.NextDouble() < pHeim) h++;
            if (rng.NextDouble() < pGast) g++;
        }

        // Plötzlicher Tod bei Gleichstand
        while (h == g)
        {
            bool hs = rng.NextDouble() < pHeim;
            bool gs = rng.NextDouble() < pGast;
            if (hs) h++;
            if (gs) g++;
        }

        return (h, g);
    }

    // ── Ausführliche Ereignisse ───────────────────────────────────────────

    private static IReadOnlyList<SimuliertesEreignis> EreignisseGenerieren(
        Mannschaft heim, Mannschaft auswaerts,
        int heimTore, int gastTore,
        Random rng, int vonMinute, int bisMinute)
    {
        int spanne = bisMinute - vonMinute + 1;
        var ereignisse = new List<SimuliertesEreignis>();

        // Tore: zufällige Minuten zuweisen
        var heimMinuten = ZufallsMinuten(heimTore, vonMinute, bisMinute, rng);
        var gastMinuten = ZufallsMinuten(gastTore, vonMinute, bisMinute, rng);

        foreach (var min in heimMinuten)
            ereignisse.Add(new(min, EreignisArt.Tor, true, "Tor"));
        foreach (var min in gastMinuten)
            ereignisse.Add(new(min, EreignisArt.Tor, false, "Tor"));

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
    IReadOnlyList<SimuliertesEreignis> Ereignisse);

public record SimuliertesEreignis(
    int Minute,
    EreignisArt Art,
    bool IstHeim,
    string Beschreibung);
