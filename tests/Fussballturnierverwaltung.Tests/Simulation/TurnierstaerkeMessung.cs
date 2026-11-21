using Fussballturnierverwaltung.Application.Simulation;
using Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Tests.Simulation;

/// <summary>
/// #679 K11: Messwerkzeug (<c>Explicit</c>, nicht im normalen Lauf und nicht in CI) – wie oft gewinnt ein K.-o.-Turnier mit
/// den 16 stärksten Bundesliga-Teams (zufällige Auslosung, neutraler Platz, Verlängerung und Elfmeterschießen) jemand aus
/// der unteren Hälfte der Stärke, mit und ohne Turnierstärke? Aufruf:
/// <c>dotnet test --project tests/Fussballturnierverwaltung.Tests -- --filter-class "*TurnierstaerkeMessung" --explicit only</c>;
/// <c>TURNIERSTAERKE_SEEDS</c> (Standard 678,1–9) und <c>TURNIERSTAERKE_TURNIERE</c> (Standard 1000) stellen den Umfang ein.
/// Die Werte stehen in der Testausgabe und, falls <c>TURNIERSTAERKE_AUSGABE</c> gesetzt ist, in dieser Datei.
/// </summary>
public class TurnierstaerkeMessung
{
    private static readonly EntscheidungsVerfahren[] _verfahren =
    [
        new() { VerfahrenArt = VerfahrenArt.VerlaengerungKlassisch, Reihenfolge = 1 },
        new() { VerfahrenArt = VerfahrenArt.Elfmeterschiessen, Reihenfolge = 2 }
    ];

    [Fact(Explicit = true)]
    public void SiegerAusDerUnterenHaelfte_OhneUndMitTurnierstaerke()
    {
        var seeds = (Environment.GetEnvironmentVariable("TURNIERSTAERKE_SEEDS") ?? "678,1,2,3,4,5,6,7,8,9")
            .Split(',').Select(int.Parse).ToList();
        int turniere = int.Parse(Environment.GetEnvironmentVariable("TURNIERSTAERKE_TURNIERE") ?? "1000");
        var teams = KalibrierungsTeams.Teams(KalibrierungsTeams.Bundesliga, 1, mitSpielweise: true)
            .OrderByDescending(t => t.GesamtRating).Take(16).ToList();
        var obereHaelfte = teams.Take(8).Select(t => t.Id).ToHashSet();
        var top4 = teams.Take(4).Select(t => t.Id).ToHashSet();

        foreach (bool mitStaerke in new[] { false, true })
        {
            double untere = 0, oberste = 0;
            foreach (int seed in seeds)
            {
                var rng = new Random(seed);
                int unten = 0, ganzOben = 0;
                for (int t = 0; t < turniere; t++)
                {
                    var sieger = Turnier(teams, rng, mitStaerke ? seed * 1000 + t : null);
                    if (!obereHaelfte.Contains(sieger.Id)) unten++;
                    if (top4.Contains(sieger.Id)) ganzOben++;
                }
                untere += 100.0 * unten / turniere;
                oberste += 100.0 * ganzOben / turniere;
            }
            Ausgeben(
                $"{(mitStaerke ? "mit" : "ohne")} Turnierstärke: Sieger aus der unteren Hälfte {untere / seeds.Count:F2} %, aus den Top 4 {oberste / seeds.Count:F2} % ({seeds.Count} Seeds × {turniere} Turniere)");
        }
    }

    private static void Ausgeben(string zeile)
    {
        TestContext.Current.TestOutputHelper?.WriteLine(zeile);
        if (Environment.GetEnvironmentVariable("TURNIERSTAERKE_AUSGABE") is { } datei) File.AppendAllText(datei, zeile + Environment.NewLine);
    }

    private static Mannschaft Turnier(List<Mannschaft> teams, Random rng, int? staerkeSeed)
    {
        var runde = teams.OrderBy(_ => rng.Next()).ToList();
        while (runde.Count > 1)
        {
            var naechste = new List<Mannschaft>();
            for (int i = 0; i < runde.Count; i += 2)
            {
                var (a, b) = (runde[i], runde[i + 1]);
                var form = staerkeSeed is int s ? Turnierstaerke.Form(s, a.Id, b.Id) : (Turnierform?)null;
                var e = SimulationsEngine.Simulieren(
                    a, b, _verfahren, ausfuehrlich: true, rng, Spielortvorteil.Neutral, turnierform: form);
                naechste.Add(AGewinnt(e) ? a : b);
            }
            runde = naechste;
        }
        return runde[0];
    }

    private static bool AGewinnt(SimulationsErgebnis e)
    {
        int a = e.HeimToreRegulaer + (e.HeimToreVerlaengerung ?? 0);
        int b = e.AuswaertsToreRegulaer + (e.AuswaertsToreVerlaengerung ?? 0);
        return a != b ? a > b : (e.HeimElfmetertore ?? 0) > (e.AuswaertsElfmetertore ?? 0);
    }
}
