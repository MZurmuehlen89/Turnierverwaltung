using System.Diagnostics;
using System.Globalization;
using Fussballturnierverwaltung.Application.Zufall;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.Infrastructure.Persistenz;

namespace Fussballturnierverwaltung.Tests.Simulation;

/// <summary>
/// #176 K10: Messwerkzeug (<c>Explicit</c>, nicht im normalen Lauf und nicht in CI) – was der Tabellenbedarf an Laufzeit kostet.
/// Eine Bundesliga-Saison (18 Teams, 306 Spiele, Spielplan nach der Kreismethode) wird im Simulationsdienst auf einer
/// InMemory-Datenbank simuliert, je einmal ohne und mit Tabellenbereichen (nur mit Zonen rechnet der Dienst den Bedarf); gemessen
/// wird der Median über mehrere Läufe. Aufruf:
/// <c>$env:BEDARF_AUSGABE='<datei>'; dotnet test --project tests/Fussballturnierverwaltung.Tests -- --filter-class "*TabellenbedarfLaufzeitMessung" --explicit only</c>
/// </summary>
public class TabellenbedarfLaufzeitMessung
{
    private const int TEAMS = 18;
    private const int LAEUFE = 7;

    private static TurnierverwaltungDbContext Saison(bool mitBereichen)
    {
        var ctx = SimulationsDienste.NeuerContext();
        SimulationsServiceTests.GrunddatenAnlegen(ctx, mannschaften: TEAMS);
        ctx.TurnierRunden.Add(new TurnierRunde
            { Id = 1, TurnierInstanzId = 1, Name = "Liga", Reihenfolge = 1, Rundenart = Rundenart.Liga, RegelwerkId = 1 });
        if (mitBereichen)
        {
            ctx.TabellenBereiche.Add(new TabellenBereich
                { TurnierRundeId = 1, PlatzVon = 1, PlatzBis = 1, Art = TabellenBereichArt.Weiterkommen, Bezeichnung = "Meister" });
            ctx.TabellenBereiche.Add(new TabellenBereich
                { TurnierRundeId = 1, PlatzVon = 2, PlatzBis = 6, Art = TabellenBereichArt.Weiterkommen, Bezeichnung = "Europa" });
            ctx.TabellenBereiche.Add(new TabellenBereich
                { TurnierRundeId = 1, PlatzVon = 16, PlatzBis = 18, Art = TabellenBereichArt.Abstieg, Bezeichnung = "Abstieg" });
        }

        int nr = 0;
        var teams = Enumerable.Range(1, TEAMS).ToList();
        for (int spieltag = 1; spieltag <= 2 * (TEAMS - 1); spieltag++)
        {
            bool rueck = spieltag > TEAMS - 1;
            for (int i = 0; i < TEAMS / 2; i++)
            {
                nr++;
                int a = teams[i], b = teams[TEAMS - 1 - i];
                ctx.Spiele.Add(new Spiel
                {
                    Id = nr, TurnierInstanzId = 1, TurnierRundeId = 1, Spielnummer = nr, Spieltag = spieltag,
                    HeimMannschaftId = rueck ? b : a, AuswaertsMannschaftId = rueck ? a : b, Status = SpielStatus.Geplant,
                    IstRueckspiel = rueck
                });
            }
            if (spieltag != TEAMS - 1)
            {
                teams.Insert(1, teams[^1]);
                teams.RemoveAt(teams.Count - 1);
            }
        }
        ctx.SaveChanges();
        return ctx;
    }

    private static async Task<double> Messen(bool mitBereichen)
    {
        await using var ctx = Saison(mitBereichen);
        var service = SimulationsDienste.Service(ctx, new Zufallsquelle(123));
        var uhr = Stopwatch.StartNew();
        await service.RundeSimulierenAsync(1, seed: 7);
        return uhr.Elapsed.TotalMilliseconds;
    }

    [Fact(Explicit = true)]
    public async Task Saison_MitUndOhneZonen()
    {
        await Messen(false); // Aufwärmen (JIT)
        await Messen(true);
        var ohne = new List<double>();
        var mit = new List<double>();
        for (int i = 0; i < LAEUFE; i++)
        {
            ohne.Add(await Messen(false));
            mit.Add(await Messen(true));
        }

        double mOhne = Median(ohne), mMit = Median(mit);
        string zeile = string.Create(CultureInfo.InvariantCulture,
            $"Bundesliga-Saison (306 Spiele, InMemory), Median über {LAEUFE} Läufe: ohne Zonen {mOhne:F0} ms, mit Zonen {mMit:F0} ms " +
            $"({100 * (mMit - mOhne) / mOhne:+0.0;-0.0} %)");
        TestContext.Current.TestOutputHelper?.WriteLine(zeile);
        if (Environment.GetEnvironmentVariable("BEDARF_AUSGABE") is { } datei) File.WriteAllText(datei, zeile + Environment.NewLine);
    }

    private static double Median(List<double> werte) => werte.Order().ElementAt(werte.Count / 2);
}
