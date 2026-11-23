using System.Globalization;
using System.Text;
using Fussballturnierverwaltung.Application.Simulation;
using Fussballturnierverwaltung.Tests.Simulation.Realismus;

namespace Fussballturnierverwaltung.Tests.Simulation;

/// <summary>
/// #176 K9: Messwerkzeug (<c>Explicit</c>, nicht im normalen Lauf und nicht in CI) – die Wirkung des Tabellenbedarfs je Bedarfsart.
/// Je Seed werden 100 Bundesliga-Saisons in Spielplan-Reihenfolge zweimal gespielt, mit Bundesliga-Zonen 1 / 6 / 15: einmal nur
/// eingeteilt (der Bedarf wird berechnet und vermerkt, die Simulation ignoriert ihn) und einmal angewendet. Je Bedarfsart steht
/// der Anteil der Team-Spiele und was sich bei Toren (je Spiel), Remisquote und Punkten (je Team-Spiel) ändert. Aufruf:
/// <c>$env:BEDARF_SEEDS='678,1,2'; dotnet test --project tests/Fussballturnierverwaltung.Tests -- --filter-class "*TabellenbedarfMessung" --explicit only</c>
/// (Standard 678 und 1–9; <c>BEDARF_AUSGABE</c> schreibt die Zusammenfassung zusätzlich in eine Datei).
/// </summary>
public class TabellenbedarfMessung
{
    private sealed record TeamSpiel(Tabellenbedarf Bedarf, int Tore, bool Remis, int Punkte);

    [Fact(Explicit = true)]
    public void Wirkung_JeBedarfsart()
    {
        var seeds = (Environment.GetEnvironmentVariable("BEDARF_SEEDS") ?? "678,1,2,3,4,5,6,7,8,9")
            .Split(',').Select(int.Parse).ToList();
        var ohne = new List<TeamSpiel>();
        var mit = new List<TeamSpiel>();
        foreach (int seed in seeds)
        {
            ohne.AddRange(TeamSpiele(RealismusStichprobe.Erzeugen(seed, tabellenlage: RealismusStichprobe.Tabellenlage.NurEinteilen)));
            mit.AddRange(TeamSpiele(RealismusStichprobe.Erzeugen(seed, tabellenlage: RealismusStichprobe.Tabellenlage.Anwenden)));
        }

        var sb = new StringBuilder();
        sb.AppendLine(string.Create(CultureInfo.InvariantCulture, $"{seeds.Count} Seeds × {RealismusStichprobe.DURCHLAEUFE} Saisons"));
        sb.AppendLine("Bedarf | Anteil Team-Spiele % | Tore ohne → mit | Remis % ohne → mit | Punkte ohne → mit");
        foreach (var bedarf in Enum.GetValues<Tabellenbedarf>())
        {
            var a = ohne.Where(t => t.Bedarf == bedarf).ToList();
            var b = mit.Where(t => t.Bedarf == bedarf).ToList();
            if (a.Count == 0 || b.Count == 0) continue;
            sb.AppendLine(string.Create(CultureInfo.InvariantCulture,
                $"{bedarf} | {100.0 * a.Count / ohne.Count:F1} → {100.0 * b.Count / mit.Count:F1} | " +
                $"{a.Average(t => t.Tore):F3} → {b.Average(t => t.Tore):F3} | " +
                $"{100.0 * a.Count(t => t.Remis) / a.Count:F2} → {100.0 * b.Count(t => t.Remis) / b.Count:F2} | " +
                $"{a.Average(t => t.Punkte):F3} → {b.Average(t => t.Punkte):F3}"));
        }
        TestContext.Current.TestOutputHelper?.WriteLine(sb.ToString());
        if (Environment.GetEnvironmentVariable("BEDARF_AUSGABE") is { } datei) File.WriteAllText(datei, sb.ToString());
    }

    /// <summary>Zwei Einträge je Spiel, einer je Team; die Tore sind die des ganzen Spiels.</summary>
    private static IEnumerable<TeamSpiel> TeamSpiele(IEnumerable<SimuliertesSpiel> spiele)
    {
        foreach (var s in spiele)
        {
            int tore = s.HeimTore + s.GastTore;
            bool remis = s.HeimTore == s.GastTore;
            yield return new TeamSpiel(s.Bedarf.Heim, tore, remis, s.HeimTore > s.GastTore ? 3 : remis ? 1 : 0);
            yield return new TeamSpiel(s.Bedarf.Gast, tore, remis, s.GastTore > s.HeimTore ? 3 : remis ? 1 : 0);
        }
    }
}
