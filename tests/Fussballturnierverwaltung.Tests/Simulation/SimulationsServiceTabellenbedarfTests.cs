using FluentAssertions;
using Fussballturnierverwaltung.Application.Zufall;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Fussballturnierverwaltung.Tests.Simulation;

/// <summary>#176: Der Simulationsdienst übergibt den Tabellenbedarf an die Engine – aber nur, wo die Runde Zonen hat. Eine Liga mit
/// sechs Teams, einfache Runde in fünf Spieltagen (Kreismethode), je einmal ohne und mit Tabellenbereichen.</summary>
public class SimulationsServiceTabellenbedarfTests
{
    private const int TEAMS = 6;

    private static TurnierverwaltungDbContext Liga(bool mitBereichen)
    {
        var ctx = SimulationsDienste.NeuerContext();
        SimulationsServiceTests.GrunddatenAnlegen(ctx, mannschaften: TEAMS);
        ctx.TurnierRunden.Add(new TurnierRunde
        {
            Id = 1, TurnierInstanzId = 1, Name = "Liga", Reihenfolge = 1, Rundenart = Rundenart.Liga, RegelwerkId = 1
        });
        if (mitBereichen)
        {
            ctx.TabellenBereiche.Add(new TabellenBereich
                { TurnierRundeId = 1, PlatzVon = 1, PlatzBis = 1, Art = TabellenBereichArt.Weiterkommen, Bezeichnung = "Meister" });
            ctx.TabellenBereiche.Add(new TabellenBereich
                { TurnierRundeId = 1, PlatzVon = 5, PlatzBis = 6, Art = TabellenBereichArt.Abstieg, Bezeichnung = "Abstieg" });
        }

        int nr = 0;
        var teams = Enumerable.Range(1, TEAMS).ToList();
        for (int spieltag = 1; spieltag < TEAMS; spieltag++)
        {
            for (int i = 0; i < TEAMS / 2; i++)
            {
                nr++;
                ctx.Spiele.Add(new Spiel
                {
                    Id = nr, TurnierInstanzId = 1, TurnierRundeId = 1, Spielnummer = nr, Spieltag = spieltag,
                    HeimMannschaftId = teams[i], AuswaertsMannschaftId = teams[TEAMS - 1 - i], Status = SpielStatus.Geplant
                });
            }
            teams.Insert(1, teams[^1]); // Kreismethode: das erste Team bleibt, die übrigen rücken um einen Platz
            teams.RemoveAt(teams.Count - 1);
        }
        ctx.SaveChanges();
        return ctx;
    }

    private static async Task<string> Ergebnisse(bool mitBereichen, int seed)
    {
        await using var ctx = Liga(mitBereichen);
        await SimulationsDienste.Service(ctx, new Zufallsquelle(123)).RundeSimulierenAsync(1, seed);
        return string.Join(' ', ctx.Spiele.AsNoTracking().Include(s => s.Ergebnis).Include(s => s.Ereignisse)
            .OrderBy(s => s.Id)
            .Select(s => $"{s.Ergebnis!.HeimToreRegulaer}:{s.Ergebnis.AuswaertsToreRegulaer}/{s.Ereignisse.Count}"));
    }

    [Fact]
    public async Task Liga_ohne_Zonen_rechnet_ohne_Tabellenbedarf()
    {
        // Zweimal dasselbe: ohne Zonen ist der Bedarf überall „offen“, die Simulation bleibt reproduzierbar.
        (await Ergebnisse(mitBereichen: false, seed: 5)).Should().Be(await Ergebnisse(mitBereichen: false, seed: 5));
    }

    [Fact]
    public async Task Liga_mit_Zonen_nutzt_den_Tabellenbedarf()
    {
        // Mit Zonen verschiebt der Bedarf die Taktik in den späten Spieltagen: über mehrere Seeds weicht mindestens eine Saison ab.
        int abweichend = 0;
        for (int seed = 1; seed <= 15; seed++)
            if (await Ergebnisse(mitBereichen: true, seed) != await Ergebnisse(mitBereichen: false, seed))
                abweichend++;

        abweichend.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Liga_mit_Zonen_bleibt_reproduzierbar()
    {
        (await Ergebnisse(mitBereichen: true, seed: 7)).Should().Be(await Ergebnisse(mitBereichen: true, seed: 7));
    }
}
