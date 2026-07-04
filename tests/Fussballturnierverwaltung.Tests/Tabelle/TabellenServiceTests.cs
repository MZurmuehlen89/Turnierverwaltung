using FluentAssertions;
using Fussballturnierverwaltung.Application.Tabelle;
using Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.Infrastructure.Persistenz;
using Fussballturnierverwaltung.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fussballturnierverwaltung.Tests.Tabelle;

public class TabellenServiceTests
{
    // ── Hilfsmethoden ─────────────────────────────────────────────────────

    private static TurnierverwaltungDbContext NeuerContext() =>
        new(new DbContextOptionsBuilder<TurnierverwaltungDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static TabellenService ServiceFuer(TurnierverwaltungDbContext ctx) =>
        new(new TurnierRundeRepository(ctx),
            new SpielRepository(ctx),
            new RegelwerkRepository(ctx));

    // Legt Regelwerk + PunkteRegel + TurnierRunde an (ohne RankingRegelSet).
    private static void GrundSetupAnlegen(
        TurnierverwaltungDbContext ctx,
        out int rundeId)
    {
        var regelwerk = new Regelwerk { Id = 1, Name = "Standard" };
        ctx.Regelwerke.Add(regelwerk);
        ctx.PunkteRegeln.Add(new PunkteRegel
        {
            Id = 1, RegelwerkId = 1,
            PunkteFuerSieg = 3, PunkteFuerUnentschieden = 1, PunkteFuerNiederlage = 0
        });

        var runde = new TurnierRunde
        {
            Id = 1, TurnierInstanzId = 1, Name = "Liga",
            Rundenart = Rundenart.Liga, RegelwerkId = 1, Reihenfolge = 1
        };
        ctx.TurnierRunden.Add(runde);

        ctx.Mannschaften.AddRange(
            new Mannschaft { Id = 1, Name = "Team A", Kurzname = "TA", LandId = 1 },
            new Mannschaft { Id = 2, Name = "Team B", Kurzname = "TB", LandId = 1 });

        ctx.SaveChanges();
        rundeId = runde.Id;
    }

    private static void SpielHinzufuegen(
        TurnierverwaltungDbContext ctx, int id, int rundeId,
        int heimId, int auswaertsId,
        int? heimTore, int? auswaertsTore,
        int? siegerMannschaftId = null,
        bool istRueckspiel = false)
    {
        var spiel = new Spiel
        {
            Id = id, TurnierInstanzId = 1, TurnierRundeId = rundeId,
            Spielnummer = id, HeimMannschaftId = heimId, AuswaertsMannschaftId = auswaertsId,
            Status = heimTore.HasValue ? SpielStatus.Abgeschlossen : SpielStatus.Geplant,
            IstRueckspiel = istRueckspiel
        };
        ctx.Spiele.Add(spiel);

        if (heimTore.HasValue && auswaertsTore.HasValue)
        {
            ctx.SpielErgebnisse.Add(new SpielErgebnis
            {
                Id = id, SpielId = id,
                HeimToreRegulaer = heimTore.Value,
                AuswaertsToreRegulaer = auswaertsTore.Value,
                EntscheidungArt = EntscheidungArt.RegulaereSpielzeit,
                SiegerMannschaftId = siegerMannschaftId
            });
        }
        ctx.SaveChanges();
    }

    // ── Tests ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task LeereSpielliste_GibtLeereTabelle()
    {
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, out int rundeId);
        var service = ServiceFuer(ctx);

        var tabelle = await service.TabelleBerechnenAsync(rundeId);

        tabelle.Should().BeEmpty();
    }

    [Fact]
    public async Task Sieg_BringtDreiPunkte_Niederlage_NullPunkte()
    {
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, out int rundeId);
        SpielHinzufuegen(ctx, 1, rundeId, heimId: 1, auswaertsId: 2,
            heimTore: 2, auswaertsTore: 0, siegerMannschaftId: 1);
        var service = ServiceFuer(ctx);

        var tabelle = await service.TabelleBerechnenAsync(rundeId);

        tabelle.Should().HaveCount(2);
        var teamA = tabelle.First(e => e.Mannschaft.Id == 1);
        var teamB = tabelle.First(e => e.Mannschaft.Id == 2);
        teamA.Punkte.Should().Be(3);
        teamA.Siege.Should().Be(1);
        teamB.Punkte.Should().Be(0);
        teamB.Niederlagen.Should().Be(1);
    }

    [Fact]
    public async Task Unentschieden_BringtJeweilsEinenPunkt()
    {
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, out int rundeId);
        SpielHinzufuegen(ctx, 1, rundeId, heimId: 1, auswaertsId: 2,
            heimTore: 1, auswaertsTore: 1, siegerMannschaftId: null);
        var service = ServiceFuer(ctx);

        var tabelle = await service.TabelleBerechnenAsync(rundeId);

        tabelle.Should().HaveCount(2);
        tabelle.Should().AllSatisfy(e =>
        {
            e.Punkte.Should().Be(1);
            e.Unentschieden.Should().Be(1);
        });
    }

    [Fact]
    public async Task TordifferenzBestimmtRangBeiGleichenPunkten()
    {
        // Team A: 1 Sieg mit +3 GD; Team B: 1 Sieg mit +1 GD → gleiche Punkte
        await using var ctx = NeuerContext();
        ctx.Regelwerke.Add(new Regelwerk { Id = 1, Name = "Standard" });
        ctx.PunkteRegeln.Add(new PunkteRegel
            { Id = 1, RegelwerkId = 1, PunkteFuerSieg = 3, PunkteFuerUnentschieden = 1, PunkteFuerNiederlage = 0 });
        ctx.TurnierRunden.Add(new TurnierRunde
            { Id = 1, TurnierInstanzId = 1, Name = "Liga", Rundenart = Rundenart.Liga, RegelwerkId = 1, Reihenfolge = 1 });
        ctx.Mannschaften.AddRange(
            new Mannschaft { Id = 1, Name = "Team A", Kurzname = "TA", LandId = 1 },
            new Mannschaft { Id = 2, Name = "Team B", Kurzname = "TB", LandId = 1 },
            new Mannschaft { Id = 3, Name = "Team C", Kurzname = "TC", LandId = 1 });
        ctx.SaveChanges();

        // Team A schlägt Team C 3:0; Team B schlägt Team C 2:1
        SpielHinzufuegen(ctx, 1, rundeId: 1, heimId: 1, auswaertsId: 3,
            heimTore: 3, auswaertsTore: 0, siegerMannschaftId: 1);
        SpielHinzufuegen(ctx, 2, rundeId: 1, heimId: 2, auswaertsId: 3,
            heimTore: 2, auswaertsTore: 1, siegerMannschaftId: 2);
        var service = ServiceFuer(ctx);

        var tabelle = await service.TabelleBerechnenAsync(1);

        tabelle[0].Mannschaft.Id.Should().Be(1, "Team A hat die bessere Tordifferenz (+3 vs +1)");
        tabelle[1].Mannschaft.Id.Should().Be(2);
    }

    [Fact]
    public async Task GeplantesSpiel_WirdNichtGewertet()
    {
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, out int rundeId);
        // abgeschlossenes Spiel
        SpielHinzufuegen(ctx, 1, rundeId, heimId: 1, auswaertsId: 2,
            heimTore: 1, auswaertsTore: 0, siegerMannschaftId: 1);
        // geplantes Spiel ohne Ergebnis
        SpielHinzufuegen(ctx, 2, rundeId, heimId: 2, auswaertsId: 1,
            heimTore: null, auswaertsTore: null);
        var service = ServiceFuer(ctx);

        var tabelle = await service.TabelleBerechnenAsync(rundeId);

        tabelle.Should().HaveCount(2);
        var teamA = tabelle.First(e => e.Mannschaft.Id == 1);
        teamA.Spiele.Should().Be(1, "nur das abgeschlossene Spiel zählt");
        teamA.Punkte.Should().Be(3);
    }

    [Fact]
    public async Task FairplayRankingRegel_BestimmtRangBeiSonstGleichenStats()
    {
        // Regeln: Punkte → Tordifferenz → ErzielteTore → FairplayWertung
        await using var ctx = NeuerContext();
        var regelSet = new RankingRegelSet { Id = 1, Name = "FIFA" };
        var regeln = new[]
        {
            new RankingRegel { Id = 1, Regelart = RankingRegelArt.Punkte,         Sortierrichtung = Sortierrichtung.Absteigend },
            new RankingRegel { Id = 2, Regelart = RankingRegelArt.Tordifferenz,   Sortierrichtung = Sortierrichtung.Absteigend },
            new RankingRegel { Id = 3, Regelart = RankingRegelArt.ErzielteTore,   Sortierrichtung = Sortierrichtung.Absteigend },
            new RankingRegel { Id = 4, Regelart = RankingRegelArt.FairplayWertung, Sortierrichtung = Sortierrichtung.Absteigend },
        };
        var eintraege = regeln.Select((r, i) => new RankingRegelSetEintrag
            { Id = i + 1, RankingRegelSetId = 1, RankingRegelId = r.Id, Reihenfolge = i + 1 }).ToArray();

        var regelwerk = new Regelwerk { Id = 1, Name = "Standard", RankingRegelSetId = 1 };
        ctx.RankingRegelSets.Add(regelSet);
        ctx.RankingRegeln.AddRange(regeln);
        ctx.RankingRegelSetEintraege.AddRange(eintraege);
        ctx.Regelwerke.Add(regelwerk);
        ctx.PunkteRegeln.Add(new PunkteRegel
            { Id = 1, RegelwerkId = 1, PunkteFuerSieg = 3, PunkteFuerUnentschieden = 1, PunkteFuerNiederlage = 0 });
        ctx.TurnierRunden.Add(new TurnierRunde
            { Id = 1, TurnierInstanzId = 1, Name = "Liga", Rundenart = Rundenart.Liga, RegelwerkId = 1, Reihenfolge = 1 });
        ctx.Mannschaften.AddRange(
            new Mannschaft { Id = 1, Name = "Team A", Kurzname = "TA", LandId = 1 },
            new Mannschaft { Id = 2, Name = "Team B", Kurzname = "TB", LandId = 1 });
        ctx.SaveChanges();

        // 1:1 Unentschieden → beide 1 Punkt, 0 GD, 1 Tor
        var spiel = new Spiel
        {
            Id = 1, TurnierInstanzId = 1, TurnierRundeId = 1,
            Spielnummer = 1, HeimMannschaftId = 1, AuswaertsMannschaftId = 2,
            Status = SpielStatus.Abgeschlossen
        };
        ctx.Spiele.Add(spiel);
        ctx.SpielErgebnisse.Add(new SpielErgebnis
        {
            Id = 1, SpielId = 1,
            HeimToreRegulaer = 1, AuswaertsToreRegulaer = 1,
            EntscheidungArt = EntscheidungArt.RegulaereSpielzeit, SiegerMannschaftId = null
        });
        // Team B bekommt eine Gelbe Karte (MannschaftId = 2)
        ctx.SpielEreignisse.Add(new SpielEreignis
        {
            Id = 1, SpielId = 1, MannschaftId = 2,
            EreignisArt = EreignisArt.GelbeKarte, Minute = 30
        });
        ctx.SaveChanges();

        var service = ServiceFuer(ctx);
        var tabelle = await service.TabelleBerechnenAsync(1);

        tabelle.Should().HaveCount(2);
        tabelle[0].Mannschaft.Id.Should().Be(1,
            "Team A ohne Karten soll dank FairplayWertung besser stehen als Team B mit Gelber Karte");
        tabelle[1].Mannschaft.Id.Should().Be(2);
    }

    // ── Hinrunde / Rückrunde / Heim- / Auswärtstabelle (Issue #68) ─────────

    private static async Task<(TabellenService Service, int RundeId)> HinUndRueckspielSetupAsync()
    {
        var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, out int rundeId);
        // Hinspiel: Team A (Heim) schlägt Team B 2:0
        SpielHinzufuegen(ctx, 1, rundeId, heimId: 1, auswaertsId: 2,
            heimTore: 2, auswaertsTore: 0, siegerMannschaftId: 1, istRueckspiel: false);
        // Rückspiel: Team B (Heim) spielt 1:1 gegen Team A
        SpielHinzufuegen(ctx, 2, rundeId, heimId: 2, auswaertsId: 1,
            heimTore: 1, auswaertsTore: 1, siegerMannschaftId: null, istRueckspiel: true);
        return (ServiceFuer(ctx), rundeId);
    }

    [Fact]
    public async Task Gesamt_BeruecksichtigtHinUndRueckspiel()
    {
        var (service, rundeId) = await HinUndRueckspielSetupAsync();

        var tabelle = await service.TabelleBerechnenAsync(rundeId, TabellenAnsicht.Gesamt);

        var teamA = tabelle.First(e => e.Mannschaft.Id == 1);
        teamA.Spiele.Should().Be(2);
        teamA.Punkte.Should().Be(4, "1 Sieg (3) + 1 Unentschieden (1)");
    }

    [Fact]
    public async Task Hinrunde_EnthaeltNurHinspiele()
    {
        var (service, rundeId) = await HinUndRueckspielSetupAsync();

        var tabelle = await service.TabelleBerechnenAsync(rundeId, TabellenAnsicht.Hinrunde);

        var teamA = tabelle.First(e => e.Mannschaft.Id == 1);
        var teamB = tabelle.First(e => e.Mannschaft.Id == 2);
        teamA.Spiele.Should().Be(1);
        teamA.Punkte.Should().Be(3, "nur der Hinspiel-Sieg zählt");
        teamB.Punkte.Should().Be(0);
    }

    [Fact]
    public async Task Rueckrunde_EnthaeltNurRueckspiele()
    {
        var (service, rundeId) = await HinUndRueckspielSetupAsync();

        var tabelle = await service.TabelleBerechnenAsync(rundeId, TabellenAnsicht.Rueckrunde);

        tabelle.Should().AllSatisfy(e =>
        {
            e.Spiele.Should().Be(1);
            e.Punkte.Should().Be(1, "nur das Rückspiel-Unentschieden zählt");
        });
    }

    [Fact]
    public async Task Heimtabelle_ZaehltProTeamNurHeimspiele()
    {
        var (service, rundeId) = await HinUndRueckspielSetupAsync();

        var tabelle = await service.TabelleBerechnenAsync(rundeId, TabellenAnsicht.Heimtabelle);

        // Team A war nur im Hinspiel (Sieg) Heim, Team B nur im Rückspiel (Unentschieden) Heim
        var teamA = tabelle.First(e => e.Mannschaft.Id == 1);
        var teamB = tabelle.First(e => e.Mannschaft.Id == 2);
        teamA.Spiele.Should().Be(1);
        teamA.Punkte.Should().Be(3, "Team A gewinnt sein einziges Heimspiel");
        teamB.Spiele.Should().Be(1);
        teamB.Punkte.Should().Be(1, "Team B spielt sein einziges Heimspiel unentschieden");
    }

    [Fact]
    public async Task Auswaertstabelle_ZaehltProTeamNurAuswaertsspiele()
    {
        var (service, rundeId) = await HinUndRueckspielSetupAsync();

        var tabelle = await service.TabelleBerechnenAsync(rundeId, TabellenAnsicht.Auswaertstabelle);

        // Team A war nur im Rückspiel (Unentschieden) auswärts, Team B nur im Hinspiel (Niederlage) auswärts
        var teamA = tabelle.First(e => e.Mannschaft.Id == 1);
        var teamB = tabelle.First(e => e.Mannschaft.Id == 2);
        teamA.Spiele.Should().Be(1);
        teamA.Punkte.Should().Be(1, "Team A spielt sein einziges Auswärtsspiel unentschieden");
        teamB.Spiele.Should().Be(1);
        teamB.Punkte.Should().Be(0, "Team B verliert sein einziges Auswärtsspiel");
    }
}
