using System.Globalization;
using System.Text;

namespace Fussballturnierverwaltung.Tests.Simulation.Realismus;

/// <summary>#683 (vorläufig, Messung): alle Realismus-Kennzahlen je Seed als Tabelle, nur auf ausdrücklichen Aufruf.</summary>
public class RealismusMessreihe
{
    private static readonly (double Von, double Bis)[] _favoritenklassen =
        [(0.33, 0.40), (0.40, 0.50), (0.50, 0.60), (0.60, 0.70), (0.70, 0.80), (0.80, 1.01)];
    private static readonly (double X, double Y)[] _remisJeSiegchance =
        [(0.378, 0.283), (0.447, 0.270), (0.547, 0.244), (0.645, 0.205), (0.748, 0.157), (0.848, 0.101)];

    private sealed record Kennzahl(string Name, double Von, double Bis, Func<List<SimuliertesSpiel>, double> Messen);

    private static double A(List<SimuliertesSpiel> s, Func<SimuliertesSpiel, bool> b) => Realismusauswertung.Anteil(s, b);
    private static IEnumerable<SimuliertesSpiel> B(List<SimuliertesSpiel> s) => s.Where(x => x.Durchlauf >= RealismusStichprobe.ENDE_HAELFTE_A);
    private static List<(double, double, int, int)> Dc(List<SimuliertesSpiel> s) =>
        s.Select(x => (x.Paarung.MittlereToreHeim, x.Paarung.MittlereToreGast, x.HeimTore, x.GastTore)).ToList();

    private static readonly Kennzahl[] _kennzahlen =
    [
        new("Remis", 0.220, 0.275, s => A(s, x => x.HeimTore == x.GastTore)),
        new("0:0", 0.045, 0.072, s => A(s, x => x.HeimTore == 0 && x.GastTore == 0)),
        new("Zu null", 0.215, 0.260, s => s.Sum(x => (x.HeimTore == 0 ? 1 : 0) + (x.GastTore == 0 ? 1 : 0)) / (2.0 * s.Count)),
        new(">=5 Tore", 0.165, 0.215, s => A(s, x => x.HeimTore + x.GastTore >= 5)),
        new(">=7 Tore", 0.024, 0.046, s => A(s, x => x.HeimTore + x.GastTore >= 7)),
        new("TD>=3", 0.175, 0.225, s => A(s, x => Math.Abs(x.HeimTore - x.GastTore) >= 3)),
        new("1:0+0:1", 0.105, 0.137, s => A(s, x => x.HeimTore + x.GastTore == 1)),
        new("1:1", 0.105, 0.133, s => A(s, x => x.HeimTore == 1 && x.GastTore == 1)),
        new("rho", -0.19, -0.09, s => DixonColes.RhoSchaetzen(Dc(s))),
        new("DC 1:1", 1.10, 9, s => DixonColes.VerhaeltnisZuUnabhaengig(Dc(s), 1, 1)),
        new("DC 1:0", 0, 0.90, s => DixonColes.VerhaeltnisZuUnabhaengig(Dc(s), [(1, 0), (0, 1)])),
        new("1.HZ", 0.425, 0.465, s => Realismusauswertung.AnteilErsteHalbzeit(s)),
        new("Min1-15", 0.110, 0.136, s => Realismusauswertung.TorminutenAnteile(s)[0]),
        new("Min76-90", 0.215, 0.253, s => Realismusauswertung.TorminutenAnteile(s)[5]),
        new("+1 Ndl", 0.085, 0.140, s => Realismusauswertung.FuehrungsAusgang(s, 45, 1).Niederlage),
        new("+1 Fav Sieg", 0.695, 0.795, s => Realismusauswertung.FuehrungsAusgang(B(s), 45, 1, (x, h) => x.Paarung.HeimIstFavorit == h).Sieg),
        new("+1 Auß Sieg", 0.490, 0.615, s => Realismusauswertung.FuehrungsAusgang(B(s), 45, 1, (x, h) => x.Paarung.HeimIstFavorit != h).Sieg),
        new("+1 Sieg", 0.635, 0.710, s => Realismusauswertung.FuehrungsAusgang(s, 45, 1).Sieg),
        new("+2 Sieg", 0.880, 0.945, s => Realismusauswertung.FuehrungsAusgang(s, 45, 2).Sieg),
        new("Zuwachs45-85", 0.10, 0.21, s => Realismusauswertung.FuehrungsAusgang(s, 85, 1).Sieg - Realismusauswertung.FuehrungsAusgang(s, 45, 1).Sieg),
        new("Remisklasse max|Abw|", 0, 0.030, RemisklasseMaxAbweichung),
        new("GC je Team", 1.6, 2.6, s => Realismusauswertung.Chancen(s).GrosschancenJeTeam),
        new("GC Verwertung", 0.35, 0.45, s => Realismusauswertung.Chancen(s).Verwertung),
        new("Elfm je Spiel", 0.26, 0.38, s => Realismusauswertung.Chancen(s).ElfmeterJeSpiel),
        new("Elfm Quote", 0.72, 0.84, s => Realismusauswertung.Chancen(s).ElfmeterQuote),
        new("Elfm Anteil", 0.065, 0.100, s => Realismusauswertung.Chancen(s).AnteilElfmeterTore),
        new("Rot je Team", 0.060, 0.088, s => s.Sum(x => { var (h, g) = Realismusauswertung.RoteKarten(x); return h + g; }) / (2.0 * s.Count)),
        new("Überzahl", 0.10, 0.22, Ueberzahl),
    ];

    private static double RemisklasseMaxAbweichung(List<SimuliertesSpiel> s)
    {
        var b = B(s).ToList();
        double max = 0;
        foreach (var (von, bis) in _favoritenklassen)
        {
            var k = b.Where(x => x.Paarung.FavoritenChanceA >= von && x.Paarung.FavoritenChanceA < bis).ToList();
            if (k.Count < 1000) continue;
            double sieg = A(k, x => x.Paarung.HeimIstFavorit ? x.HeimTore > x.GastTore : x.GastTore > x.HeimTore);
            double remis = A(k, x => x.HeimTore == x.GastTore);
            max = Math.Max(max, Math.Abs(remis - Realismusauswertung.Interpolieren(_remisJeSiegchance, sieg)));
        }
        return max;
    }

    private static double Ueberzahl(List<SimuliertesSpiel> s)
    {
        double siege = 0, erwartet = 0; int n = 0;
        foreach (var x in B(s))
        {
            var (h, g) = Realismusauswertung.RoteKarten(x);
            if (h == g) continue;
            bool heimU = h < g;
            n++;
            siege += (heimU ? x.HeimTore > x.GastTore : x.GastTore > x.HeimTore) ? 1 : 0;
            erwartet += heimU ? x.Paarung.SiegHeimA : x.Paarung.SiegGastA;
        }
        return (siege - erwartet) / n;
    }

    [Fact(Explicit = true)]
    public void Messreihe_JeSeed()
    {
        var seedText = Environment.GetEnvironmentVariable("REALISMUS_SEEDS") ?? "678,1,2,3,4,5,6,7,8,9";
        var ausgabe = Environment.GetEnvironmentVariable("REALISMUS_AUSGABE") ?? Path.Combine(Path.GetTempPath(), "realismus-messreihe.csv");
        var seeds = seedText.Split(',').Select(int.Parse).ToList();
        var sb = new StringBuilder("Seed;" + string.Join(";", _kennzahlen.Select(k => k.Name)) + "\n");
        sb.Append("Von;" + string.Join(";", _kennzahlen.Select(k => k.Von.ToString(CultureInfo.InvariantCulture))) + "\n");
        sb.Append("Bis;" + string.Join(";", _kennzahlen.Select(k => k.Bis.ToString(CultureInfo.InvariantCulture))) + "\n");
        foreach (int seed in seeds)
        {
            var spiele = RealismusStichprobe.Erzeugen(seed);
            sb.Append(seed + ";" + string.Join(";", _kennzahlen.Select(k => k.Messen(spiele).ToString("F5", CultureInfo.InvariantCulture))) + "\n");
            File.WriteAllText(ausgabe, sb.ToString());
        }
    }
}
