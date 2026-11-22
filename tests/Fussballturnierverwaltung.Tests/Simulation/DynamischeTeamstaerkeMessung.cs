using System.Globalization;
using Fussballturnierverwaltung.Application.Simulation;
using Fussballturnierverwaltung.Tests.Simulation.Realismus;

namespace Fussballturnierverwaltung.Tests.Simulation;

/// <summary>
/// #359 K7: Messwerkzeug (<c>Explicit</c>, nicht im normalen Lauf und nicht in CI) – die <b>Drift</b> der dynamischen Teamstärke.
/// Je Saison (Bundesliga, Spielplan-Reihenfolge, K 20, Wirkung fest) wird die Abweichung jedes Teams vom Startwert am Saisonende
/// bestimmt und auf den Startwert zurückgeführt (Steigung der Ausgleichsgeraden über alle Teams und Saisons). Eine Steigung
/// nahe 0 heißt: starke Teams gewinnen im Mittel nicht systematisch Elo – die Eichung Rating → Elo stimmt. Aufruf:
/// <c>$env:DYNAMIK_SEEDS='678,1,2'; dotnet test --project tests/Fussballturnierverwaltung.Tests -- --filter-class "*DynamischeTeamstaerkeMessung" --explicit only</c>
/// (Standard 678 und 1–9; <c>DYNAMIK_AUSGABE</c> schreibt die Zeilen zusätzlich in eine Datei).
/// </summary>
public class DynamischeTeamstaerkeMessung
{
    [Fact(Explicit = true)]
    public void DriftSteigung_UeberSaisons()
    {
        var seeds = (Environment.GetEnvironmentVariable("DYNAMIK_SEEDS") ?? "678,1,2,3,4,5,6,7,8,9")
            .Split(',').Select(int.Parse).ToList();
        var teams = KalibrierungsTeams.Teams(KalibrierungsTeams.Bundesliga, 1, mitSpielweise: true);
        var start = teams.ToDictionary(t => t.Id, DynamischeTeamstaerke.Startwert);

        var steigungen = new List<double>();
        foreach (int seed in seeds)
        {
            var spiele = RealismusStichprobe.Erzeugen(seed, dynamischeTeamstaerke: true);
            var x = new List<double>();
            var y = new List<double>();
            foreach (var saison in spiele.GroupBy(s => s.Durchlauf))
            {
                var wertungen = new List<WertungsSpiel>();
                int nummer = 0;
                foreach (var s in saison)   // Reihenfolge der Stichprobe = Spielplan-Reihenfolge
                    wertungen.AddRange(DynamischeTeamstaerke.Wertungen(
                        new Austragung(new Wertungsschluessel(1, 1, ++nummer), s.HeimId, s.GastId,
                            start[s.HeimId], start[s.GastId], Spielortvorteil.Heim, false),
                        s.HeimTore, s.GastTore, null));
                var ende = DynamischeTeamstaerke.Abweichungen(wertungen, RealismusStichprobe.LIGA_K, new Wertungsschluessel(2, 0, 0));
                foreach (var t in teams)
                {
                    x.Add(start[t.Id]);
                    y.Add(ende.GetValueOrDefault(t.Id));
                }
            }
            steigungen.Add(Steigung(x, y));
        }

        double mittel = steigungen.Average();
        double sd = steigungen.Count > 1 ? Math.Sqrt(steigungen.Sum(s => (s - mittel) * (s - mittel)) / (steigungen.Count - 1)) : double.NaN;
        string zeile = string.Create(CultureInfo.InvariantCulture,
            $"Drift-Steigung (Elo-Änderung je Start-Elo-Punkt, K {RealismusStichprobe.LIGA_K}): Mittel {mittel:F4}, SD über Seeds {sd:F4}, " +
            $"Standardfehler des Mittels {sd / Math.Sqrt(steigungen.Count):F4} ({steigungen.Count} Seeds × {RealismusStichprobe.DURCHLAEUFE} Saisons)");
        TestContext.Current.TestOutputHelper?.WriteLine(zeile);
        if (Environment.GetEnvironmentVariable("DYNAMIK_AUSGABE") is { } datei) File.AppendAllText(datei, zeile + Environment.NewLine);
    }

    private static double Steigung(List<double> x, List<double> y)
    {
        double mx = x.Average(), my = y.Average();
        double zaehler = 0, nenner = 0;
        for (int i = 0; i < x.Count; i++)
        {
            zaehler += (x[i] - mx) * (y[i] - my);
            nenner += (x[i] - mx) * (x[i] - mx);
        }
        return zaehler / nenner;
    }
}
