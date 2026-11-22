using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Application.Simulation;

namespace Fussballturnierverwaltung.Tests.Simulation.Realismus;

/// <summary>Je Paarung vorab gerechnete Größen der Stichprobe: Vorhersagen aus der Hälfte A der Durchläufe (Favorit,
/// Siegchancen), mittlere Tore aus allen Durchläufen (für λ und μ). Die Siegchancen aus Hälfte B dienen nur der
/// Kreuzschätzung der Remisklassen (#679).</summary>
public sealed record PaarungsDaten(
    double SiegHeimA, double SiegGastA,
    double MittlereToreHeim, double MittlereToreGast,
    double SiegHeimB, double SiegGastB)
{
    public bool HeimIstFavorit => SiegHeimA > SiegGastA;
    public double FavoritenChanceA => Math.Max(SiegHeimA, SiegGastA);
}

/// <summary>Ein simuliertes Spiel der gemeinsamen Stichprobe (ausführlicher Pfad, Ereignisse enthalten).</summary>
public sealed record SimuliertesSpiel(
    int HeimId, int GastId, int Durchlauf, SimulationsErgebnis Ergebnis, PaarungsDaten Paarung)
{
    public int HeimTore => Ergebnis.HeimToreRegulaer;
    public int GastTore => Ergebnis.AuswaertsToreRegulaer;

    /// <summary>#679: Siegchancen der Paarung aus der Hälfte, zu der dieses Spiel nicht gehört – so bestimmt ein Spiel
    /// nie seine eigene Einteilung, und beide Hälften können gemessen werden.</summary>
    private (double Heim, double Gast) SiegchancenAusGegenhaelfte =>
        Durchlauf < RealismusStichprobe.ENDE_HAELFTE_A
            ? (Paarung.SiegHeimB, Paarung.SiegGastB)
            : (Paarung.SiegHeimA, Paarung.SiegGastA);

    public bool HeimIstFavoritLautGegenhaelfte => SiegchancenAusGegenhaelfte.Heim > SiegchancenAusGegenhaelfte.Gast;

    public double FavoritenChanceLautGegenhaelfte => Math.Max(SiegchancenAusGegenhaelfte.Heim, SiegchancenAusGegenhaelfte.Gast);
}

/// <summary>
/// #678: eine gemeinsame Monte-Carlo-Stichprobe für alle Realismus-Tests – 100 Bundesliga-Saisons (30 600 Spiele) im
/// ausführlichen Pfad mit der Spielweise aus <c>SpielweisenVorschlag</c>, fester Seed. Im Mutationslauf bleibt sie
/// leer (die Realismus-Tests setzen dort aus; sie sollen keine Mutanten töten).
/// </summary>
public sealed class RealismusStichprobe
{
    public const int SEED = 678;
    public const int DURCHLAEUFE = 100;
    /// <summary>Durchläufe unterhalb dieser Grenze bilden die Hälfte A (Vorhersage), die übrigen die Hälfte B.</summary>
    public const int ENDE_HAELFTE_A = DURCHLAEUFE / 2;

    public IReadOnlyList<SimuliertesSpiel> Spiele { get; }

    public RealismusStichprobe()
    {
        Spiele = Mutationslauf.Aktiv ? [] : Erzeugen(SEED);
    }

    private sealed record Rohspiel(int Heim, int Gast, int Durchlauf, SimulationsErgebnis Ergebnis);

    /// <summary>K der dynamischen Teamstärke in Ligen (#359).</summary>
    public const double LIGA_K = 20;

    /// <summary>Die Stichprobe. <paramref name="dynamischeTeamstaerke"/> (#359, nur Messreihe, <c>REALISMUS_DYNAMIK=1</c>) spielt
    /// jede Saison in der Reihenfolge eines echten Spielplans (34 Spieltage, Rückrunde mit getauschtem Heimrecht), jedes Spiel
    /// mit dem Stand der Teams aus den Spielen davor; ohne sie bleibt die Stichprobe bitgleich wie vor #359.</summary>
    public static List<SimuliertesSpiel> Erzeugen(int seed, bool dynamischeTeamstaerke = false)
    {
        var teams = KalibrierungsTeams.Teams(KalibrierungsTeams.Bundesliga, 1, mitSpielweise: true);
        var rng = new Random(seed);
        var roh = new List<Rohspiel>();
        for (int d = 0; d < DURCHLAEUFE; d++)
        {
            // #679: je Saison eine Turnierstärke je Team. Der Seed folgt aus Stichproben-Seed und Durchlauf, nicht aus
            // dem Strom der Spiele – der bleibt unverändert.
            int staerkeSeed = seed * 1000 + d;
            if (dynamischeTeamstaerke)
            {
                SaisonMitDynamik(teams, d, staerkeSeed, rng, roh);
                continue;
            }
            foreach (var heim in teams)
            {
                foreach (var gast in teams.Where(t => t != heim))
                    roh.Add(new(heim.Id, gast.Id, d, SimulationsEngine.Simulieren(
                        heim, gast, [], ausfuehrlich: true, rng, turnierform: Turnierstaerke.Form(staerkeSeed, heim.Id, gast.Id))));
            }
        }

        var paarungen = roh.GroupBy(r => (r.Heim, r.Gast)).ToDictionary(g => g.Key, g => PaarungBilden(g.ToList()));
        return roh.Select(r => new SimuliertesSpiel(r.Heim, r.Gast, r.Durchlauf, r.Ergebnis, paarungen[(r.Heim, r.Gast)])).ToList();
    }

    private static void SaisonMitDynamik(
        List<Mannschaft> teams, int durchlauf, int staerkeSeed, Random rng, List<Rohspiel> roh)
    {
        var start = teams.ToDictionary(t => t.Id, DynamischeTeamstaerke.Startwert);
        var wertungen = new List<WertungsSpiel>();
        int nummer = 0;
        var spieltage = Spielplan(teams);
        for (int tag = 0; tag < spieltage.Count; tag++)
            foreach (var (heim, gast) in spieltage[tag])
            {
                var termin = new Austragung(new Wertungsschluessel(1, tag + 1, ++nummer), heim.Id, gast.Id,
                    start[heim.Id], start[gast.Id], Spielortvorteil.Heim, false);
                var abweichung = DynamischeTeamstaerke.Abweichungen(wertungen, LIGA_K, termin.Schluessel);
                var form = DynamischeTeamstaerke.Anwenden(Turnierstaerke.Form(staerkeSeed, heim.Id, gast.Id),
                    abweichung.GetValueOrDefault(heim.Id), abweichung.GetValueOrDefault(gast.Id));
                var ergebnis = SimulationsEngine.Simulieren(heim, gast, [], ausfuehrlich: true, rng, turnierform: form);
                wertungen.AddRange(DynamischeTeamstaerke.Wertungen(
                    termin, ergebnis.HeimToreRegulaer, ergebnis.AuswaertsToreRegulaer, null));
                roh.Add(new(heim.Id, gast.Id, durchlauf, ergebnis));
            }
    }

    /// <summary>Doppelrunde nach der Kreisverfahren-Methode: n − 1 Spieltage der Hinrunde, danach dieselben mit getauschtem
    /// Heimrecht. Nicht aus der App (die Realismus-Stichprobe braucht nur eine plausible Reihenfolge).</summary>
    internal static List<List<(Mannschaft Heim, Mannschaft Gast)>> Spielplan(IReadOnlyList<Mannschaft> teams)
    {
        int n = teams.Count;
        var kreis = teams.ToList();
        var hinrunde = new List<List<(Mannschaft, Mannschaft)>>();
        for (int runde = 0; runde < n - 1; runde++)
        {
            var tag = new List<(Mannschaft, Mannschaft)>();
            for (int i = 0; i < n / 2; i++)
            {
                var (a, b) = (kreis[i], kreis[n - 1 - i]);
                tag.Add((i + runde) % 2 == 0 ? (a, b) : (b, a));
            }
            hinrunde.Add(tag);
            kreis.Insert(1, kreis[^1]);
            kreis.RemoveAt(kreis.Count - 1);
        }
        return [.. hinrunde, .. hinrunde.Select(t => t.Select(p => (p.Item2, p.Item1)).ToList())];
    }

    private static PaarungsDaten PaarungBilden(List<Rohspiel> spiele)
    {
        static double Anteil(List<Rohspiel> s, Func<SimulationsErgebnis, bool> bedingung) =>
            (double)s.Count(x => bedingung(x.Ergebnis)) / s.Count;
        static bool Heimsieg(SimulationsErgebnis e) => e.HeimToreRegulaer > e.AuswaertsToreRegulaer;
        static bool Gastsieg(SimulationsErgebnis e) => e.HeimToreRegulaer < e.AuswaertsToreRegulaer;

        var a = spiele.Where(s => s.Durchlauf < ENDE_HAELFTE_A).ToList();
        var b = spiele.Where(s => s.Durchlauf >= ENDE_HAELFTE_A).ToList();
        return new PaarungsDaten(
            Anteil(a, Heimsieg), Anteil(a, Gastsieg),
            spiele.Average(s => s.Ergebnis.HeimToreRegulaer), spiele.Average(s => s.Ergebnis.AuswaertsToreRegulaer),
            Anteil(b, Heimsieg), Anteil(b, Gastsieg));
    }
}
