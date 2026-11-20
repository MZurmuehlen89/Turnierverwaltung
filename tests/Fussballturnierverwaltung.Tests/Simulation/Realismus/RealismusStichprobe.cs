using Fussballturnierverwaltung.Application.Simulation;

namespace Fussballturnierverwaltung.Tests.Simulation.Realismus;

/// <summary>Je Paarung vorab gerechnete Größen der Stichprobe: Vorhersagen aus der Hälfte A der Durchläufe (Favorit,
/// Siegchancen), mittlere Tore aus allen Durchläufen (für λ und μ).</summary>
public sealed record PaarungsDaten(
    double SiegHeimA, double SiegGastA,
    double MittlereToreHeim, double MittlereToreGast)
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

    public static List<SimuliertesSpiel> Erzeugen(int seed)
    {
        var teams = KalibrierungsTeams.Teams(KalibrierungsTeams.Bundesliga, 1, mitSpielweise: true);
        var rng = new Random(seed);
        var roh = new List<Rohspiel>();
        for (int d = 0; d < DURCHLAEUFE; d++)
        {
            foreach (var heim in teams)
            {
                foreach (var gast in teams.Where(t => t != heim))
                    roh.Add(new(heim.Id, gast.Id, d, SimulationsEngine.Simulieren(heim, gast, [], ausfuehrlich: true, rng)));
            }
        }

        var paarungen = roh.GroupBy(r => (r.Heim, r.Gast)).ToDictionary(g => g.Key, g => PaarungBilden(g.ToList()));
        return roh.Select(r => new SimuliertesSpiel(r.Heim, r.Gast, r.Durchlauf, r.Ergebnis, paarungen[(r.Heim, r.Gast)])).ToList();
    }

    private static PaarungsDaten PaarungBilden(List<Rohspiel> spiele)
    {
        static double Anteil(List<Rohspiel> s, Func<SimulationsErgebnis, bool> bedingung) =>
            (double)s.Count(x => bedingung(x.Ergebnis)) / s.Count;
        static bool Heimsieg(SimulationsErgebnis e) => e.HeimToreRegulaer > e.AuswaertsToreRegulaer;
        static bool Gastsieg(SimulationsErgebnis e) => e.HeimToreRegulaer < e.AuswaertsToreRegulaer;

        var a = spiele.Where(s => s.Durchlauf < ENDE_HAELFTE_A).ToList();
        return new PaarungsDaten(
            Anteil(a, Heimsieg), Anteil(a, Gastsieg),
            spiele.Average(s => s.Ergebnis.HeimToreRegulaer), spiele.Average(s => s.Ergebnis.AuswaertsToreRegulaer));
    }
}
