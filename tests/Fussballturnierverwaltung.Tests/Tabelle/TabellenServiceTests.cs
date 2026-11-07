using FluentAssertions;
using Fussballturnierverwaltung.Application.Tabelle;
using Fussballturnierverwaltung.Domain.Entitaeten.Ranglisten;
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
            new RegelwerkRepository(ctx),
            new RanglisteRepository(ctx),
            new AuslosungRepository(ctx));

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
        bool istRueckspiel = false,
        int? spieltag = null)
    {
        var spiel = new Spiel
        {
            Id = id, TurnierInstanzId = 1, TurnierRundeId = rundeId,
            Spielnummer = id, HeimMannschaftId = heimId, AuswaertsMannschaftId = auswaertsId,
            Status = heimTore.HasValue ? SpielStatus.Abgeschlossen : SpielStatus.Geplant,
            IstRueckspiel = istRueckspiel,
            Spieltag = spieltag
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
    public async Task Buchholz_IstSummeDerAktuellenPunkteAllerBisherigenGegner()
    {
        // A schlägt B, B schlägt C -> B hat 3 Punkte (0 aus A-Spiel, 3 aus C-Spiel)
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, out int rundeId);
        ctx.Mannschaften.Add(new Mannschaft { Id = 3, Name = "Team C", Kurzname = "TC", LandId = 1 });
        ctx.SaveChanges();

        SpielHinzufuegen(ctx, 1, rundeId, heimId: 1, auswaertsId: 2,
            heimTore: 2, auswaertsTore: 0, siegerMannschaftId: 1);
        SpielHinzufuegen(ctx, 2, rundeId, heimId: 2, auswaertsId: 3,
            heimTore: 1, auswaertsTore: 0, siegerMannschaftId: 2);
        var service = ServiceFuer(ctx);

        var tabelle = await service.TabelleBerechnenAsync(rundeId);

        var teamA = tabelle.First(e => e.Mannschaft.Id == 1);
        var teamB = tabelle.First(e => e.Mannschaft.Id == 2);
        var teamC = tabelle.First(e => e.Mannschaft.Id == 3);

        teamB.Punkte.Should().Be(3);
        teamA.Buchholz.Should().Be(teamB.Punkte);
        teamC.Buchholz.Should().Be(teamB.Punkte);
        teamB.Buchholz.Should().Be(teamA.Punkte + teamC.Punkte);
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

    // ── Gegner-Kriterien der UEFA-Ligaphase (Issue #353) ───────────────────

    // Die beiden punktgleichen Teams haben je einen Gegner, deren Werte sich je Kriterium widersprechen:
    // der eine Gegner hat mehr Punkte (6 statt 3) und mehr Tore (7 statt 3), der andere die bessere
    // Tordifferenz (+2 statt +1). Erwartet wird immer Team 2 vorn - der Losentscheid allein (kleinere ID)
    // sähe Team 1 vorn, ein vertauschtes Kriterium ebenfalls.
    [Theory]
    [InlineData(RankingRegelArt.GegnerPunkte,       2)]
    [InlineData(RankingRegelArt.GegnerTordifferenz, 1)]
    [InlineData(RankingRegelArt.GegnerErzielteTore, 2)]
    public async Task GegnerKriterium_BestimmtRangBeiPunktgleichheit(RankingRegelArt regelart, int teamMitPunktstarkemGegner)
    {
        await using var ctx = NeuerContext();
        var regeln = new[]
        {
            new RankingRegel { Id = 1, Regelart = RankingRegelArt.Punkte,       Sortierrichtung = Sortierrichtung.Absteigend },
            new RankingRegel { Id = 2, Regelart = regelart,                     Sortierrichtung = Sortierrichtung.Absteigend },
            new RankingRegel { Id = 3, Regelart = RankingRegelArt.Losentscheid, Sortierrichtung = Sortierrichtung.Aufsteigend },
        };
        ctx.RankingRegelSets.Add(new RankingRegelSet { Id = 1, Name = "Ligaphase" });
        ctx.RankingRegeln.AddRange(regeln);
        ctx.RankingRegelSetEintraege.AddRange(regeln.Select((r, i) => new RankingRegelSetEintrag
            { Id = i + 1, RankingRegelSetId = 1, RankingRegelId = r.Id, Reihenfolge = i + 1 }));
        ctx.Regelwerke.Add(new Regelwerk { Id = 1, Name = "Ligaphase", RankingRegelSetId = 1 });
        ctx.PunkteRegeln.Add(new PunkteRegel
            { Id = 1, RegelwerkId = 1, PunkteFuerSieg = 3, PunkteFuerUnentschieden = 1, PunkteFuerNiederlage = 0 });
        ctx.TurnierRunden.Add(new TurnierRunde
            { Id = 1, TurnierInstanzId = 1, Name = "Ligaphase", Rundenart = Rundenart.TopfLiga, RegelwerkId = 1, Reihenfolge = 1 });
        ctx.Mannschaften.AddRange(Enumerable.Range(1, 6).Select(id =>
            new Mannschaft { Id = id, Name = $"Team {id}", Kurzname = $"T{id}", LandId = 1 }));
        ctx.SaveChanges();

        const int PUNKTSTARKER_GEGNER = 3, TORDIFFERENZSTARKER_GEGNER = 4;
        int anderesTeam = teamMitPunktstarkemGegner == 1 ? 2 : 1;
        SpielHinzufuegen(ctx, 1, 1, heimId: teamMitPunktstarkemGegner, auswaertsId: PUNKTSTARKER_GEGNER, heimTore: 1, auswaertsTore: 0);
        SpielHinzufuegen(ctx, 2, 1, heimId: anderesTeam, auswaertsId: TORDIFFERENZSTARKER_GEGNER, heimTore: 1, auswaertsTore: 0);
        // Punktstarker Gegner: 6 Punkte, 7:6 Tore (+1)
        SpielHinzufuegen(ctx, 3, 1, heimId: PUNKTSTARKER_GEGNER, auswaertsId: 5, heimTore: 5, auswaertsTore: 4);
        SpielHinzufuegen(ctx, 4, 1, heimId: PUNKTSTARKER_GEGNER, auswaertsId: 6, heimTore: 2, auswaertsTore: 1);
        // Tordifferenzstarker Gegner: 3 Punkte, 3:1 Tore (+2)
        SpielHinzufuegen(ctx, 5, 1, heimId: TORDIFFERENZSTARKER_GEGNER, auswaertsId: 5, heimTore: 3, auswaertsTore: 0);

        var tabelle = await ServiceFuer(ctx).TabelleBerechnenAsync(1);

        var team1 = tabelle.Single(e => e.Mannschaft.Id == 1);
        var team2 = tabelle.Single(e => e.Mannschaft.Id == 2);
        team1.Punkte.Should().Be(team2.Punkte);
        team2.Rang.Should().BeLessThan(team1.Rang);
    }

    // ── Auswärtskriterien über alle Spiele (Issue #354) ────────────────────

    // Team 1 und Team 2 sind punkt-, tordifferenz-, tor- und sieggleich (je ein 2:1-Sieg und eine 0:1-Niederlage).
    // Team 2 hat auswärts gewonnen und dabei seine zwei Tore erzielt, Team 1 zu Hause - der Losentscheid allein
    // (kleinere ID) sähe Team 1 vorn.
    [Theory]
    [InlineData(RankingRegelArt.AuswaertsToreGesamt)]
    [InlineData(RankingRegelArt.AuswaertsSiege)]
    public async Task AuswaertsKriterium_BestimmtRangBeiSonstGleichenStats(RankingRegelArt regelart)
    {
        await using var ctx = NeuerContext();
        var regeln = new[]
        {
            new RankingRegel { Id = 1, Regelart = RankingRegelArt.Punkte,       Sortierrichtung = Sortierrichtung.Absteigend },
            new RankingRegel { Id = 2, Regelart = regelart,                     Sortierrichtung = Sortierrichtung.Absteigend },
            new RankingRegel { Id = 3, Regelart = RankingRegelArt.Losentscheid, Sortierrichtung = Sortierrichtung.Aufsteigend },
        };
        ctx.RankingRegelSets.Add(new RankingRegelSet { Id = 1, Name = "Ligaphase" });
        ctx.RankingRegeln.AddRange(regeln);
        ctx.RankingRegelSetEintraege.AddRange(regeln.Select((r, i) => new RankingRegelSetEintrag
            { Id = i + 1, RankingRegelSetId = 1, RankingRegelId = r.Id, Reihenfolge = i + 1 }));
        ctx.Regelwerke.Add(new Regelwerk { Id = 1, Name = "Ligaphase", RankingRegelSetId = 1 });
        ctx.PunkteRegeln.Add(new PunkteRegel
            { Id = 1, RegelwerkId = 1, PunkteFuerSieg = 3, PunkteFuerUnentschieden = 1, PunkteFuerNiederlage = 0 });
        ctx.TurnierRunden.Add(new TurnierRunde
            { Id = 1, TurnierInstanzId = 1, Name = "Ligaphase", Rundenart = Rundenart.TopfLiga, RegelwerkId = 1, Reihenfolge = 1 });
        ctx.Mannschaften.AddRange(Enumerable.Range(1, 4).Select(id =>
            new Mannschaft { Id = id, Name = $"Team {id}", Kurzname = $"T{id}", LandId = 1 }));
        ctx.SaveChanges();

        SpielHinzufuegen(ctx, 1, 1, heimId: 1, auswaertsId: 3, heimTore: 2, auswaertsTore: 1);
        SpielHinzufuegen(ctx, 2, 1, heimId: 4, auswaertsId: 1, heimTore: 1, auswaertsTore: 0);
        SpielHinzufuegen(ctx, 3, 1, heimId: 3, auswaertsId: 2, heimTore: 1, auswaertsTore: 2);
        SpielHinzufuegen(ctx, 4, 1, heimId: 2, auswaertsId: 4, heimTore: 0, auswaertsTore: 1);

        var tabelle = await ServiceFuer(ctx).TabelleBerechnenAsync(1);

        var team1 = tabelle.Single(e => e.Mannschaft.Id == 1);
        var team2 = tabelle.Single(e => e.Mannschaft.Id == 2);
        (team1.Punkte, team1.Tore, team1.Gegentore, team1.Siege)
            .Should().Be((team2.Punkte, team2.Tore, team2.Gegentore, team2.Siege));
        team2.Rang.Should().BeLessThan(team1.Rang);
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

    // ── Historischer Tabellenstand nach Spieltag (Issue #109, Phase 1 von #47) ─────

    [Fact]
    public async Task TabelleBerechnenAsync_BisSpieltag_BeruecksichtigtNurFrueherenSpieltag()
    {
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, out int rundeId);
        // Spieltag 1: Team A gewinnt 2:0
        SpielHinzufuegen(ctx, 1, rundeId, heimId: 1, auswaertsId: 2,
            heimTore: 2, auswaertsTore: 0, siegerMannschaftId: 1, spieltag: 1);
        // Spieltag 2: Team B gewinnt 3:0 (Rückspiel-Team-Konstellation ignoriert hier bewusst)
        SpielHinzufuegen(ctx, 2, rundeId, heimId: 2, auswaertsId: 1,
            heimTore: 3, auswaertsTore: 0, siegerMannschaftId: 2, spieltag: 2);
        var service = ServiceFuer(ctx);

        var stand = await service.TabelleBerechnenAsync(rundeId, bisSpieltag: 1);

        var teamA = stand.First(e => e.Mannschaft.Id == 1);
        teamA.Spiele.Should().Be(1, "nur Spieltag 1 zählt für den Snapshot");
        teamA.Punkte.Should().Be(3);
    }

    [Fact]
    public async Task TabelleBerechnenAsync_OhneBisSpieltag_EntsprichtAktuellemStand()
    {
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, out int rundeId);
        SpielHinzufuegen(ctx, 1, rundeId, heimId: 1, auswaertsId: 2,
            heimTore: 2, auswaertsTore: 0, siegerMannschaftId: 1, spieltag: 1);
        SpielHinzufuegen(ctx, 2, rundeId, heimId: 2, auswaertsId: 1,
            heimTore: 3, auswaertsTore: 0, siegerMannschaftId: 2, spieltag: 2);
        var service = ServiceFuer(ctx);

        var stand = await service.TabelleBerechnenAsync(rundeId);

        var teamA = stand.First(e => e.Mannschaft.Id == 1);
        teamA.Spiele.Should().Be(2, "ohne bisSpieltag zaehlen alle Spieltage");
    }

    [Fact]
    public async Task GruppenTabellenBerechnenAsync_BisSpieltag_FiltertNurDieGruppentabelle()
    {
        await using var ctx = NeuerContext();
        ctx.Regelwerke.Add(new Regelwerk { Id = 1, Name = "Standard" });
        ctx.PunkteRegeln.Add(new PunkteRegel
            { Id = 1, RegelwerkId = 1, PunkteFuerSieg = 3, PunkteFuerUnentschieden = 1, PunkteFuerNiederlage = 0 });
        ctx.TurnierRunden.Add(new TurnierRunde
        {
            Id = 1, TurnierInstanzId = 1, Name = "Gruppenphase", Rundenart = Rundenart.Gruppenphase,
            RegelwerkId = 1, Reihenfolge = 1, GruppenAnzahl = 1, AusgangsMannschaftsanzahl = 2
        });
        ctx.Mannschaften.AddRange(
            new Mannschaft { Id = 1, Name = "Team A", Kurzname = "TA", LandId = 1 },
            new Mannschaft { Id = 2, Name = "Team B", Kurzname = "TB", LandId = 1 });
        ctx.SaveChanges();

        SpielHinzufuegen(ctx, 1, rundeId: 1, heimId: 1, auswaertsId: 2,
            heimTore: 2, auswaertsTore: 0, siegerMannschaftId: 1, spieltag: 1);
        SpielHinzufuegen(ctx, 2, rundeId: 1, heimId: 2, auswaertsId: 1,
            heimTore: 3, auswaertsTore: 0, siegerMannschaftId: 2, spieltag: 2);
        var service = ServiceFuer(ctx);

        var gruppen = await service.GruppenTabellenBerechnenAsync(1, bisSpieltag: 1);

        gruppen.Should().HaveCount(1);
        var teamA = gruppen[0].Tabelle.First(e => e.Mannschaft.Id == 1);
        teamA.Spiele.Should().Be(1, "nur Spieltag 1 fliesst in den Snapshot ein");
        teamA.Punkte.Should().Be(3);
        teamA.Qualifikationsstatus.Should().Be(Qualifikationsstatus.Offen, "historischer Stand ohne Status (#480)");
        teamA.BesterRang.Should().BeNull();
    }

    [Fact]
    public async Task GruppenTabellenBerechnenAsync_WorstCaseNutztDefiniertesRankingRegelSetStattPunkte()
    {
        // Regelwerk sortiert ausschliesslich nach ErzielteTore (keine Punkte-Regel enthalten). Team B hat mit
        // Abstand die meisten PUNKTE (3, gleichauf mit A) aber deutlich weniger TORE als Team A (2 vs. 5/6) -
        // B darf trotz der Punktgleichheit nie als "SicherQualifiziert" durchgehen, weil das Regelwerk einzig
        // Tore zaehlt. Ein offenes, fuer B fremdes Spiel (A vs C) erzwingt den simulierten Pfad
        // (ExtremRangErmitteln) - der Test prueft damit, dass dort exakt das definierte RankingRegelSet
        // angewendet wird und nicht etwa eine Punkte-basierte Rueckfalllogik.
        await using var ctx = NeuerContext();
        var regelSet = new RankingRegelSet { Id = 1, Name = "NurTore" };
        var regel = new RankingRegel { Id = 1, Regelart = RankingRegelArt.ErzielteTore, Sortierrichtung = Sortierrichtung.Absteigend };
        ctx.RankingRegelSets.Add(regelSet);
        ctx.RankingRegeln.Add(regel);
        ctx.RankingRegelSetEintraege.Add(new RankingRegelSetEintrag { Id = 1, RankingRegelSetId = 1, RankingRegelId = 1, Reihenfolge = 1 });

        ctx.Regelwerke.Add(new Regelwerk { Id = 1, Name = "NurToreRegelwerk", RankingRegelSetId = 1 });
        ctx.PunkteRegeln.Add(new PunkteRegel
            { Id = 1, RegelwerkId = 1, PunkteFuerSieg = 3, PunkteFuerUnentschieden = 1, PunkteFuerNiederlage = 0 });
        ctx.TurnierRunden.Add(new TurnierRunde
        {
            Id = 1, TurnierInstanzId = 1, Name = "Gruppenphase", Rundenart = Rundenart.Gruppenphase,
            RegelwerkId = 1, Reihenfolge = 1, GruppenAnzahl = 1, AusgangsMannschaftsanzahl = 1
        });
        ctx.Mannschaften.AddRange(
            new Mannschaft { Id = 1, Name = "Team A", Kurzname = "TA", LandId = 1 },
            new Mannschaft { Id = 2, Name = "Team B", Kurzname = "TB", LandId = 1 },
            new Mannschaft { Id = 3, Name = "Team C", Kurzname = "TC", LandId = 1 },
            new Mannschaft { Id = 4, Name = "Team D", Kurzname = "TD", LandId = 1 });
        ctx.SaveChanges();

        SpielHinzufuegen(ctx, 1, rundeId: 1, heimId: 1, auswaertsId: 4,
            heimTore: 5, auswaertsTore: 0, siegerMannschaftId: 1, spieltag: 1);   // A schlägt D 5:0 (viele Tore, 3 Pkt)
        SpielHinzufuegen(ctx, 2, rundeId: 1, heimId: 2, auswaertsId: 3,
            heimTore: 2, auswaertsTore: 0, siegerMannschaftId: 2, spieltag: 1);   // B schlägt C 2:0 (wenig Tore, 3 Pkt)
        SpielHinzufuegen(ctx, 3, rundeId: 1, heimId: 1, auswaertsId: 3,
            heimTore: null, auswaertsTore: null, spieltag: 2);                   // A - C noch offen (fremd für B)
        var service = ServiceFuer(ctx);

        var gruppen = await service.GruppenTabellenBerechnenAsync(1);

        var tabelle = gruppen[0].Tabelle;
        var teamA = tabelle.First(e => e.Mannschaft.Id == 1);
        var teamB = tabelle.First(e => e.Mannschaft.Id == 2);
        teamA.Punkte.Should().Be(3);
        teamB.Punkte.Should().Be(3, "A und B stehen bei den Punkten gleichauf");
        teamA.Tore.Should().Be(5);
        teamB.Tore.Should().Be(2, "B hat trotz gleicher Punkte deutlich weniger Tore als A");

        teamB.Qualifikationsstatus.Should().Be(Qualifikationsstatus.SicherAusgeschieden,
            "das Regelwerk sortiert nur nach ErzielteTore - A liegt dort (5/6 Tore) in jeder Simulation vor B (2 Tore), " +
            "obwohl B bei den Punkten gleichauf mit A liegt");
    }

    // ── SicherPositioniert: exakter Rang fixiert vs. nur Zonen-Zugehoerigkeit (Issue #202) ─────

    [Fact]
    public async Task GruppenTabellenBerechnenAsync_OhneOffeneSpiele_IstSicherPositioniertFuerAlle()
    {
        await using var ctx = NeuerContext();
        ctx.Regelwerke.Add(new Regelwerk { Id = 1, Name = "Standard" });
        ctx.PunkteRegeln.Add(new PunkteRegel
            { Id = 1, RegelwerkId = 1, PunkteFuerSieg = 3, PunkteFuerUnentschieden = 1, PunkteFuerNiederlage = 0 });
        ctx.TurnierRunden.Add(new TurnierRunde
        {
            Id = 1, TurnierInstanzId = 1, Name = "Gruppenphase", Rundenart = Rundenart.Gruppenphase,
            RegelwerkId = 1, Reihenfolge = 1, GruppenAnzahl = 1, AusgangsMannschaftsanzahl = 1
        });
        ctx.Mannschaften.AddRange(
            new Mannschaft { Id = 1, Name = "Team A", Kurzname = "TA", LandId = 1 },
            new Mannschaft { Id = 2, Name = "Team B", Kurzname = "TB", LandId = 1 });
        ctx.SaveChanges();

        SpielHinzufuegen(ctx, 1, rundeId: 1, heimId: 1, auswaertsId: 2,
            heimTore: 1, auswaertsTore: 0, siegerMannschaftId: 1, spieltag: 1);
        var service = ServiceFuer(ctx);

        var gruppen = await service.GruppenTabellenBerechnenAsync(1);

        gruppen[0].Tabelle.Should().AllSatisfy(e =>
            e.SicherPositioniert.Should().BeTrue("keine offenen Spiele mehr - jeder Rang ist final"));
    }

    [Fact]
    public async Task GruppenTabellenBerechnenAsync_SicherQualifiziertAberRangInnerhalbDerZoneNochOffen()
    {
        // 3 Teams, Top 2 qualifizieren. A und B haben Team C bereits so deutlich abgehaengt, dass beide in
        // jedem Fall unter den Top 2 landen (SicherQualifiziert) - aber das direkte Duell A vs. B ist noch
        // offen und entscheidet, wer am Ende Rang 1 bzw. Rang 2 wird. Beide duerfen daher trotz gesicherter
        // Qualifikationszone noch NICHT SicherPositioniert sein.
        await using var ctx = NeuerContext();
        ctx.Regelwerke.Add(new Regelwerk { Id = 1, Name = "Standard" });
        ctx.PunkteRegeln.Add(new PunkteRegel
            { Id = 1, RegelwerkId = 1, PunkteFuerSieg = 3, PunkteFuerUnentschieden = 1, PunkteFuerNiederlage = 0 });
        ctx.TurnierRunden.Add(new TurnierRunde
        {
            Id = 1, TurnierInstanzId = 1, Name = "Gruppenphase", Rundenart = Rundenart.Gruppenphase,
            RegelwerkId = 1, Reihenfolge = 1, GruppenAnzahl = 1, AusgangsMannschaftsanzahl = 2
        });
        ctx.Mannschaften.AddRange(
            new Mannschaft { Id = 1, Name = "Team A", Kurzname = "TA", LandId = 1 },
            new Mannschaft { Id = 2, Name = "Team B", Kurzname = "TB", LandId = 1 },
            new Mannschaft { Id = 3, Name = "Team C", Kurzname = "TC", LandId = 1 });
        ctx.SaveChanges();

        SpielHinzufuegen(ctx, 1, rundeId: 1, heimId: 1, auswaertsId: 3,
            heimTore: 5, auswaertsTore: 0, siegerMannschaftId: 1, spieltag: 1); // A schlägt C 5:0
        SpielHinzufuegen(ctx, 2, rundeId: 1, heimId: 2, auswaertsId: 3,
            heimTore: 5, auswaertsTore: 0, siegerMannschaftId: 2, spieltag: 1); // B schlägt C 5:0
        SpielHinzufuegen(ctx, 3, rundeId: 1, heimId: 1, auswaertsId: 2,
            heimTore: null, auswaertsTore: null, spieltag: 2);                 // A - B noch offen
        var service = ServiceFuer(ctx);

        var gruppen = await service.GruppenTabellenBerechnenAsync(1);

        var tabelle = gruppen[0].Tabelle;
        var teamA = tabelle.First(e => e.Mannschaft.Id == 1);
        var teamB = tabelle.First(e => e.Mannschaft.Id == 2);
        teamA.Qualifikationsstatus.Should().Be(Qualifikationsstatus.SicherQualifiziert,
            "Team C kann A selbst bei einer Niederlage im direkten Duell nicht mehr einholen");
        teamB.Qualifikationsstatus.Should().Be(Qualifikationsstatus.SicherQualifiziert,
            "Team C kann B selbst bei einer Niederlage im direkten Duell nicht mehr einholen");
        teamA.SicherPositioniert.Should().BeFalse("ob A Rang 1 oder Rang 2 wird, entscheidet erst das offene Duell gegen B");
        teamB.SicherPositioniert.Should().BeFalse("ob B Rang 1 oder Rang 2 wird, entscheidet erst das offene Duell gegen A");
    }

    [Fact]
    public async Task GruppenTabellenBerechnenAsync_WorstCasePrueftAuchDeutlichenTordifferenzUmschwung()
    {
        // Nutzer-Fund beim manuellen #202-Test: 4 Teams, Rang 2 (X) hat keine offenen Spiele mehr außer dem
        // direkten Duell gegen Rang 4 (Z). Aktuell: W=7, X=4, C=1, Z=1 Punkte. Gewinnt Z gegen X, ziehen beide
        // auf 4 Punkte gleich - die Tordifferenz entscheidet. Vor dem Fix simulierte ExtremRangErmitteln jedes
        // offene Spiel nur mit einem knappen 1:0, das reicht nicht, um Z' schlechtere Ausgangs-Tordifferenz
        // (-1 vs. X' +3) noch zu drehen - X galt faelschlich als SicherQualifiziert. Ein deutlicher Sieg (die
        // in der Praxis jederzeit moeglich ist) wuerde das aber sehr wohl tun.
        await using var ctx = NeuerContext();
        ctx.Regelwerke.Add(new Regelwerk { Id = 1, Name = "Standard" });
        ctx.PunkteRegeln.Add(new PunkteRegel
            { Id = 1, RegelwerkId = 1, PunkteFuerSieg = 3, PunkteFuerUnentschieden = 1, PunkteFuerNiederlage = 0 });
        ctx.TurnierRunden.Add(new TurnierRunde
        {
            Id = 1, TurnierInstanzId = 1, Name = "Gruppenphase", Rundenart = Rundenart.Gruppenphase,
            RegelwerkId = 1, Reihenfolge = 1, GruppenAnzahl = 1, AusgangsMannschaftsanzahl = 2
        });
        ctx.Mannschaften.AddRange(
            new Mannschaft { Id = 1, Name = "Team W", Kurzname = "TW", LandId = 1 },
            new Mannschaft { Id = 2, Name = "Team X", Kurzname = "TX", LandId = 1 },
            new Mannschaft { Id = 3, Name = "Team C", Kurzname = "TC", LandId = 1 },
            new Mannschaft { Id = 4, Name = "Team Z", Kurzname = "TZ", LandId = 1 });
        ctx.SaveChanges();

        SpielHinzufuegen(ctx, 1, rundeId: 1, heimId: 1, auswaertsId: 2,
            heimTore: 1, auswaertsTore: 1, siegerMannschaftId: null, spieltag: 1); // W - X 1:1
        SpielHinzufuegen(ctx, 2, rundeId: 1, heimId: 1, auswaertsId: 3,
            heimTore: 1, auswaertsTore: 0, siegerMannschaftId: 1, spieltag: 1);    // W schlägt C 1:0
        SpielHinzufuegen(ctx, 3, rundeId: 1, heimId: 1, auswaertsId: 4,
            heimTore: 1, auswaertsTore: 0, siegerMannschaftId: 1, spieltag: 2);    // W schlägt Z 1:0
        SpielHinzufuegen(ctx, 4, rundeId: 1, heimId: 2, auswaertsId: 3,
            heimTore: 3, auswaertsTore: 0, siegerMannschaftId: 2, spieltag: 2);    // X schlägt C 3:0
        SpielHinzufuegen(ctx, 5, rundeId: 1, heimId: 3, auswaertsId: 4,
            heimTore: 0, auswaertsTore: 0, siegerMannschaftId: null, spieltag: 3); // C - Z 0:0
        SpielHinzufuegen(ctx, 6, rundeId: 1, heimId: 2, auswaertsId: 4,
            heimTore: null, auswaertsTore: null, spieltag: 3);                    // X - Z noch offen
        var service = ServiceFuer(ctx);

        var gruppen = await service.GruppenTabellenBerechnenAsync(1);

        var tabelle = gruppen[0].Tabelle;
        tabelle.First(e => e.Mannschaft.Id == 1).Punkte.Should().Be(7);
        tabelle.First(e => e.Mannschaft.Id == 2).Punkte.Should().Be(4);
        tabelle.First(e => e.Mannschaft.Id == 3).Punkte.Should().Be(1);
        tabelle.First(e => e.Mannschaft.Id == 4).Punkte.Should().Be(1);

        var teamX = tabelle.First(e => e.Mannschaft.Id == 2);
        teamX.Qualifikationsstatus.Should().Be(Qualifikationsstatus.Offen,
            "Team Z kann bei einem ausreichend deutlichen Sieg im direkten Duell mit Team X gleichziehen " +
            "und es per Tordifferenz noch verdrängen - nicht nur bei einem knappen 1:0");
    }

    [Fact]
    public async Task GruppenTabellenBerechnenAsync_ExtremesErgebnisInFremdemSpielKannDrittesTeamGefaehrden()
    {
        // Spiegelbildlicher Fall zum vorigen Test: hier ist ein EXTREMES (nicht ein knappes) Ergebnis in einem
        // FREMDEN Spiel das für Team B gefährliche Szenario - ein reiner "fremde Spiele bleiben knapp"-Ansatz
        // würde das übersehen (mit genau dieser Konstellation nachweislich zu einem falschen "SicherQualifiziert"
        // gefuehrt, bevor ExtremRangErmitteln auch fuer fremde Spiele beide Tordifferenz-Varianten durchprobiert).
        //
        // 4 Teams, nur Rang 1 qualifiziert. Team B hat kein offenes Spiel mehr (5 Punkte, GD+3, fertig).
        // Team A (2 Punkte, GD0) und Team D (1 Punkt, GD-1) haben noch gegeneinander zu spielen (fremd für B).
        // Gewinnt A, zieht A exakt auf B's 5 Punkte gleich - Tordifferenz entscheidet:
        //   - Gewinnt A nur knapp (1:0), bleibt B's GD (+3) vor A's GD (+1) -> B bleibt Erster.
        //   - Gewinnt A deutlich (bei entsprechend hoher Tordifferenz gegen D), überholt A's GD Bs GD -> A wird
        //     Erster, B rutscht auf Rang 2 - und ist bei nur einem Aufstiegsplatz raus.
        await using var ctx = NeuerContext();
        ctx.Regelwerke.Add(new Regelwerk { Id = 1, Name = "Standard" });
        ctx.PunkteRegeln.Add(new PunkteRegel
            { Id = 1, RegelwerkId = 1, PunkteFuerSieg = 3, PunkteFuerUnentschieden = 1, PunkteFuerNiederlage = 0 });
        ctx.TurnierRunden.Add(new TurnierRunde
        {
            Id = 1, TurnierInstanzId = 1, Name = "Gruppenphase", Rundenart = Rundenart.Gruppenphase,
            RegelwerkId = 1, Reihenfolge = 1, GruppenAnzahl = 1, AusgangsMannschaftsanzahl = 1
        });
        ctx.Mannschaften.AddRange(
            new Mannschaft { Id = 1, Name = "Team A", Kurzname = "TA", LandId = 1 },
            new Mannschaft { Id = 2, Name = "Team B", Kurzname = "TB", LandId = 1 },
            new Mannschaft { Id = 3, Name = "Team C", Kurzname = "TC", LandId = 1 },
            new Mannschaft { Id = 4, Name = "Team D", Kurzname = "TD", LandId = 1 });
        ctx.SaveChanges();

        SpielHinzufuegen(ctx, 1, rundeId: 1, heimId: 1, auswaertsId: 2,
            heimTore: 1, auswaertsTore: 1, siegerMannschaftId: null, spieltag: 1); // A - B 1:1
        SpielHinzufuegen(ctx, 2, rundeId: 1, heimId: 1, auswaertsId: 3,
            heimTore: 1, auswaertsTore: 1, siegerMannschaftId: null, spieltag: 1); // A - C 1:1
        SpielHinzufuegen(ctx, 3, rundeId: 1, heimId: 2, auswaertsId: 3,
            heimTore: 3, auswaertsTore: 0, siegerMannschaftId: 2, spieltag: 2);    // B schlägt C 3:0
        SpielHinzufuegen(ctx, 4, rundeId: 1, heimId: 2, auswaertsId: 4,
            heimTore: 1, auswaertsTore: 1, siegerMannschaftId: null, spieltag: 2); // B - D 1:1
        SpielHinzufuegen(ctx, 5, rundeId: 1, heimId: 3, auswaertsId: 4,
            heimTore: 1, auswaertsTore: 0, siegerMannschaftId: 3, spieltag: 3);    // C schlägt D 1:0
        SpielHinzufuegen(ctx, 6, rundeId: 1, heimId: 1, auswaertsId: 4,
            heimTore: null, auswaertsTore: null, spieltag: 3);                    // A - D noch offen
        var service = ServiceFuer(ctx);

        var gruppen = await service.GruppenTabellenBerechnenAsync(1);

        var tabelle = gruppen[0].Tabelle;
        tabelle.First(e => e.Mannschaft.Id == 2).Punkte.Should().Be(5, "B: 1 Unentschieden + 1 Sieg + 1 Unentschieden");

        var teamB = tabelle.First(e => e.Mannschaft.Id == 2);
        teamB.Qualifikationsstatus.Should().Be(Qualifikationsstatus.Offen,
            "gewinnt Team A deutlich gegen Team D, überholt Team A Team B per Tordifferenz - " +
            "obwohl das fremde Spiel A-D Team B selbst gar nicht betrifft");
    }

    [Fact]
    public async Task GruppenTabellenBerechnenAsync_SicherAusgeschiedenAberSchlussrangNochOffen()
    {
        // 4 Teams, Top 2 qualifizieren. C und D sind beide schon rechnerisch aus dem Rennen um die Top 2
        // (SicherAusgeschieden) - aber ihr direktes Duell entscheidet noch, wer Rang 3 bzw. Rang 4 wird.
        // Symmetrischer Fall zu SicherQualifiziert: auch auf der Ausscheider-Seite darf SicherPositioniert
        // erst gelten, wenn der exakte Schlussrang feststeht (Issue #202, Nutzer-Entscheidung: symmetrisch).
        await using var ctx = NeuerContext();
        ctx.Regelwerke.Add(new Regelwerk { Id = 1, Name = "Standard" });
        ctx.PunkteRegeln.Add(new PunkteRegel
            { Id = 1, RegelwerkId = 1, PunkteFuerSieg = 3, PunkteFuerUnentschieden = 1, PunkteFuerNiederlage = 0 });
        ctx.TurnierRunden.Add(new TurnierRunde
        {
            Id = 1, TurnierInstanzId = 1, Name = "Gruppenphase", Rundenart = Rundenart.Gruppenphase,
            RegelwerkId = 1, Reihenfolge = 1, GruppenAnzahl = 1, AusgangsMannschaftsanzahl = 2
        });
        ctx.Mannschaften.AddRange(
            new Mannschaft { Id = 1, Name = "Team A", Kurzname = "TA", LandId = 1 },
            new Mannschaft { Id = 2, Name = "Team B", Kurzname = "TB", LandId = 1 },
            new Mannschaft { Id = 3, Name = "Team C", Kurzname = "TC", LandId = 1 },
            new Mannschaft { Id = 4, Name = "Team D", Kurzname = "TD", LandId = 1 });
        ctx.SaveChanges();

        SpielHinzufuegen(ctx, 1, rundeId: 1, heimId: 1, auswaertsId: 3,
            heimTore: 5, auswaertsTore: 0, siegerMannschaftId: 1, spieltag: 1); // A schlägt C 5:0
        SpielHinzufuegen(ctx, 2, rundeId: 1, heimId: 1, auswaertsId: 4,
            heimTore: 5, auswaertsTore: 0, siegerMannschaftId: 1, spieltag: 1); // A schlägt D 5:0
        SpielHinzufuegen(ctx, 3, rundeId: 1, heimId: 2, auswaertsId: 3,
            heimTore: 5, auswaertsTore: 0, siegerMannschaftId: 2, spieltag: 2); // B schlägt C 5:0
        SpielHinzufuegen(ctx, 4, rundeId: 1, heimId: 2, auswaertsId: 4,
            heimTore: 5, auswaertsTore: 0, siegerMannschaftId: 2, spieltag: 2); // B schlägt D 5:0
        SpielHinzufuegen(ctx, 5, rundeId: 1, heimId: 3, auswaertsId: 4,
            heimTore: null, auswaertsTore: null, spieltag: 3);                 // C - D noch offen
        var service = ServiceFuer(ctx);

        var gruppen = await service.GruppenTabellenBerechnenAsync(1);

        var tabelle = gruppen[0].Tabelle;
        var teamC = tabelle.First(e => e.Mannschaft.Id == 3);
        var teamD = tabelle.First(e => e.Mannschaft.Id == 4);
        teamC.Qualifikationsstatus.Should().Be(Qualifikationsstatus.SicherAusgeschieden);
        teamD.Qualifikationsstatus.Should().Be(Qualifikationsstatus.SicherAusgeschieden);
        teamC.SicherPositioniert.Should().BeFalse("ob C Rang 3 oder Rang 4 wird, entscheidet erst das offene Duell gegen D");
        teamD.SicherPositioniert.Should().BeFalse("ob D Rang 3 oder Rang 4 wird, entscheidet erst das offene Duell gegen C");
    }

    // ── Qualifikationsstatus: Worst-Case muss alle Ausgänge fremder Spiele prüfen (Issue #201) ─────

    [Fact]
    public async Task GruppenTabellenBerechnenAsync_WorstCasePrueftAlleAusgaengeFremderSpiele()
    {
        // 4er-Gruppe, Top 2 qualifizieren. Team B hat alle 3 Spiele absolviert (4 Punkte, GD -1), Team D ebenso
        // (2 Punkte). Team A (4 Punkte, GD +2) und Team C (3 Punkte) haben noch gegeneinander zu spielen (fuer
        // Team B ein "fremdes" Spiel, A ist Heimmannschaft). Gewinnt Team C dieses Spiel (Auswaertssieg), kommt
        // es auf 6 Punkte und schiebt Team B auf Rang 3 - Team B darf daher nicht "SicherQualifiziert" sein.
        // Die alte Implementierung simulierte fremde offene Spiele immer pauschal als Heimsieg und haette diesen
        // Auswaertssieg-Fall nie geprueft.
        await using var ctx = NeuerContext();
        ctx.Regelwerke.Add(new Regelwerk { Id = 1, Name = "Standard" });
        ctx.PunkteRegeln.Add(new PunkteRegel
            { Id = 1, RegelwerkId = 1, PunkteFuerSieg = 3, PunkteFuerUnentschieden = 1, PunkteFuerNiederlage = 0 });
        ctx.TurnierRunden.Add(new TurnierRunde
        {
            Id = 1, TurnierInstanzId = 1, Name = "Gruppenphase", Rundenart = Rundenart.Gruppenphase,
            RegelwerkId = 1, Reihenfolge = 1, GruppenAnzahl = 1, AusgangsMannschaftsanzahl = 2
        });
        ctx.Mannschaften.AddRange(
            new Mannschaft { Id = 1, Name = "Team A", Kurzname = "TA", LandId = 1 },
            new Mannschaft { Id = 2, Name = "Team B", Kurzname = "TB", LandId = 1 },
            new Mannschaft { Id = 3, Name = "Team C", Kurzname = "TC", LandId = 1 },
            new Mannschaft { Id = 4, Name = "Team D", Kurzname = "TD", LandId = 1 });
        ctx.SaveChanges();

        SpielHinzufuegen(ctx, 1, rundeId: 1, heimId: 1, auswaertsId: 2,
            heimTore: 2, auswaertsTore: 0, siegerMannschaftId: 1, spieltag: 1);      // A schlägt B 2:0
        SpielHinzufuegen(ctx, 2, rundeId: 1, heimId: 2, auswaertsId: 3,
            heimTore: 1, auswaertsTore: 0, siegerMannschaftId: 2, spieltag: 1);      // B schlägt C 1:0
        SpielHinzufuegen(ctx, 3, rundeId: 1, heimId: 2, auswaertsId: 4,
            heimTore: 1, auswaertsTore: 1, siegerMannschaftId: null, spieltag: 2);   // B - D 1:1
        SpielHinzufuegen(ctx, 4, rundeId: 1, heimId: 1, auswaertsId: 4,
            heimTore: 1, auswaertsTore: 1, siegerMannschaftId: null, spieltag: 2);   // A - D 1:1
        SpielHinzufuegen(ctx, 5, rundeId: 1, heimId: 3, auswaertsId: 4,
            heimTore: 1, auswaertsTore: 0, siegerMannschaftId: 3, spieltag: 2);      // C schlägt D 1:0
        SpielHinzufuegen(ctx, 6, rundeId: 1, heimId: 1, auswaertsId: 3,
            heimTore: null, auswaertsTore: null, spieltag: 3);                      // A - C noch offen
        var service = ServiceFuer(ctx);

        var gruppen = await service.GruppenTabellenBerechnenAsync(1);

        var vorTabelle = gruppen[0].Tabelle;
        vorTabelle.First(e => e.Mannschaft.Id == 1).Punkte.Should().Be(4, "Team A: 1 Sieg + 1 Unentschieden");
        vorTabelle.First(e => e.Mannschaft.Id == 2).Punkte.Should().Be(4, "Team B: 1 Sieg + 1 Unentschieden");
        vorTabelle.First(e => e.Mannschaft.Id == 3).Punkte.Should().Be(3, "Team C: 1 Sieg (bisher)");

        var teamB = vorTabelle.First(e => e.Mannschaft.Id == 2);
        teamB.Qualifikationsstatus.Should().Be(Qualifikationsstatus.Offen,
            "Team C kann durch einen Auswaertssieg gegen Team A (fremdes Spiel) auf 6 Punkte kommen und Team B auf Rang 3 verdraengen");
    }

    [Fact]
    public async Task GruppenTabellenBerechnenAsync_WorstCasePrueftAuchUnentschiedenImFremdenSpiel()
    {
        // Reproduziert einen beim manuellen #206-Test gefundenen Bug (Fund im echten Testturnier,
        // Gruppe "Arsenal/Dortmund/Polen/PSG"): Team A hat alle 3 Spiele absolviert, ausschliesslich
        // Unentschieden (3 Punkte). Team C hat ebenfalls alle Spiele absolviert und ist mit 7 Punkten
        // uneinholbar Erster. Team B und Team D haben noch gegeneinander zu spielen (aus Sicht von
        // Team A ein "fremdes" Spiel) und stehen bei je 1 Punkt. Endet dieses Spiel unentschieden,
        // bleiben beide bei 2 Punkten - Team A haelt Rang 2. Gewinnt hingegen eines der beiden Teams,
        // erreicht es 4 Punkte und verdraengt Team A auf Rang 3. Die alte ExtremRangErmitteln-
        // Implementierung probierte fuer fremde Spiele nur die 4 Sieg-Varianten (Heim/Auswaerts x
        // knapp/extrem), NIE ein Unentschieden - Team A galt dadurch faelschlich als SicherPositioniert
        // auf Rang 3 (statt korrekt: Rang unsicher zwischen 2 und 3, je nach Ausgang).
        await using var ctx = NeuerContext();
        ctx.Regelwerke.Add(new Regelwerk { Id = 1, Name = "Standard" });
        ctx.PunkteRegeln.Add(new PunkteRegel
            { Id = 1, RegelwerkId = 1, PunkteFuerSieg = 3, PunkteFuerUnentschieden = 1, PunkteFuerNiederlage = 0 });
        ctx.TurnierRunden.Add(new TurnierRunde
        {
            Id = 1, TurnierInstanzId = 1, Name = "Gruppenphase", Rundenart = Rundenart.Gruppenphase,
            RegelwerkId = 1, Reihenfolge = 1, GruppenAnzahl = 1, AusgangsMannschaftsanzahl = 2
        });
        ctx.Mannschaften.AddRange(
            new Mannschaft { Id = 1, Name = "Team A", Kurzname = "TA", LandId = 1 },
            new Mannschaft { Id = 2, Name = "Team B", Kurzname = "TB", LandId = 1 },
            new Mannschaft { Id = 3, Name = "Team C", Kurzname = "TC", LandId = 1 },
            new Mannschaft { Id = 4, Name = "Team D", Kurzname = "TD", LandId = 1 });
        ctx.SaveChanges();

        SpielHinzufuegen(ctx, 1, rundeId: 1, heimId: 1, auswaertsId: 2,
            heimTore: 1, auswaertsTore: 1, spieltag: 1);                                 // A - B 1:1
        SpielHinzufuegen(ctx, 2, rundeId: 1, heimId: 3, auswaertsId: 4,
            heimTore: 2, auswaertsTore: 1, siegerMannschaftId: 3, spieltag: 1);           // C schlägt D 2:1
        SpielHinzufuegen(ctx, 3, rundeId: 1, heimId: 4, auswaertsId: 1,
            heimTore: 0, auswaertsTore: 0, spieltag: 2);                                  // D - A 0:0
        SpielHinzufuegen(ctx, 4, rundeId: 1, heimId: 2, auswaertsId: 3,
            heimTore: 0, auswaertsTore: 1, siegerMannschaftId: 3, spieltag: 2);           // C schlägt B 1:0
        SpielHinzufuegen(ctx, 5, rundeId: 1, heimId: 1, auswaertsId: 3,
            heimTore: 1, auswaertsTore: 1, spieltag: 2);                                  // A - C 1:1
        SpielHinzufuegen(ctx, 6, rundeId: 1, heimId: 4, auswaertsId: 2,
            heimTore: null, auswaertsTore: null, spieltag: 3);                            // D - B noch offen
        var service = ServiceFuer(ctx);

        var gruppen = await service.GruppenTabellenBerechnenAsync(1);

        var teamA = gruppen[0].Tabelle.First(e => e.Mannschaft.Id == 1);
        teamA.Punkte.Should().Be(3, "drei Unentschieden");
        teamA.SicherPositioniert.Should().BeFalse(
            "ein Unentschieden im fremden Spiel D-B haelt Team A auf Rang 2, ein Sieg von D oder B verdraengt es auf Rang 3");
        teamA.Qualifikationsstatus.Should().Be(Qualifikationsstatus.Offen,
            "Team A kann sowohl noch auf Rang 2 qualifizieren (bei Unentschieden) als auch auf Rang 3 ausscheiden (bei Sieg)");
    }

    // ── UebergreifendeRangfolgeBerechnenAsync (Issue #184) ──────────────────

    [Fact]
    public async Task UebergreifendeRangfolgeBerechnenAsync_SortiertGruppendritteNachTordifferenz()
    {
        // Drei Gruppen zu je drei Teams (via je zwei verkettenden Spielen einer Kette A-B, B-C statt
        // vollem Round-Robin - reicht fuer GruppenAusSpielenAbleiten, um die Gruppenzugehoerigkeit
        // ueber Paarungen zu erkennen). Alle drei Gruppendritten stehen bei 1 Punkt, aber mit
        // unterschiedlicher Tordifferenz (-3 / -2 / -1) - der Cross-Group-Vergleich muss exakt danach sortieren.
        await using var ctx = NeuerContext();
        ctx.Regelwerke.Add(new Regelwerk { Id = 1, Name = "Standard" });
        ctx.PunkteRegeln.Add(new PunkteRegel
            { Id = 1, RegelwerkId = 1, PunkteFuerSieg = 3, PunkteFuerUnentschieden = 1, PunkteFuerNiederlage = 0 });
        ctx.TurnierRunden.Add(new TurnierRunde
        {
            Id = 1, TurnierInstanzId = 1, Name = "Gruppenphase", Rundenart = Rundenart.Gruppenphase,
            RegelwerkId = 1, Reihenfolge = 1, GruppenAnzahl = 3, AusgangsMannschaftsanzahl = 9
        });
        for (int i = 1; i <= 9; i++)
            ctx.Mannschaften.Add(new Mannschaft { Id = i, Name = $"Team {i}", Kurzname = $"T{i}", LandId = 1 });
        ctx.SaveChanges();

        // Gruppe 1 (Team 1/2/3) - Gruppendritter (Team 2): Punkte 1, Tordifferenz -3.
        SpielHinzufuegen(ctx, 1, rundeId: 1, heimId: 1, auswaertsId: 2, heimTore: 3, auswaertsTore: 0, siegerMannschaftId: 1);
        SpielHinzufuegen(ctx, 2, rundeId: 1, heimId: 2, auswaertsId: 3, heimTore: 1, auswaertsTore: 1);

        // Gruppe 2 (Team 4/5/6) - Gruppendritter (Team 5): Punkte 1, Tordifferenz -2.
        SpielHinzufuegen(ctx, 3, rundeId: 1, heimId: 4, auswaertsId: 5, heimTore: 2, auswaertsTore: 0, siegerMannschaftId: 4);
        SpielHinzufuegen(ctx, 4, rundeId: 1, heimId: 5, auswaertsId: 6, heimTore: 0, auswaertsTore: 0);

        // Gruppe 3 (Team 7/8/9) - Gruppendritter (Team 8): Punkte 1, Tordifferenz -1.
        SpielHinzufuegen(ctx, 5, rundeId: 1, heimId: 7, auswaertsId: 8, heimTore: 1, auswaertsTore: 0, siegerMannschaftId: 7);
        SpielHinzufuegen(ctx, 6, rundeId: 1, heimId: 8, auswaertsId: 9, heimTore: 2, auswaertsTore: 2);
        var service = ServiceFuer(ctx);

        var rangfolge = await service.UebergreifendeRangfolgeBerechnenAsync(1, platzierung: 3);

        rangfolge.Select(e => e.Mannschaft.Id).Should().Equal(new[] { 8, 5, 2 },
            "Tordifferenz -1 (Team 8) schlägt -2 (Team 5) schlägt -3 (Team 2), obwohl alle drei nur 1 Punkt haben");
        rangfolge.Select(e => e.Rang).Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task UebergreifendeRangfolgeBerechnenAsync_DirekterVergleichZwischenGruppenFremdenTeamsFaelltDurch()
    {
        // Gleiches Szenario wie oben, aber das Regelwerk sortiert zuerst nach DirekterVergleichPunkte -
        // zwischen Kandidaten aus unterschiedlichen Gruppen gibt es nie ein gemeinsames Spiel, das Kriterium
        // muss also wirkungslos durchfallen (cmp=0) und ErzielteTore (zweites Kriterium) entscheidet. Die
        // erwartete Reihenfolge nach ERZIELTEN TOREN weicht bewusst von der Tordifferenz-Reihenfolge des
        // vorigen Tests ab, um zu beweisen, dass tatsächlich das zweite Kriterium greift.
        await using var ctx = NeuerContext();
        var regelSet = new RankingRegelSet { Id = 1, Name = "DirektDannTore" };
        var regeln = new[]
        {
            new RankingRegel { Id = 1, Regelart = RankingRegelArt.DirekterVergleichPunkte, Sortierrichtung = Sortierrichtung.Absteigend },
            new RankingRegel { Id = 2, Regelart = RankingRegelArt.ErzielteTore,             Sortierrichtung = Sortierrichtung.Absteigend },
        };
        ctx.RankingRegelSets.Add(regelSet);
        ctx.RankingRegeln.AddRange(regeln);
        ctx.RankingRegelSetEintraege.AddRange(
            new RankingRegelSetEintrag { Id = 1, RankingRegelSetId = 1, RankingRegelId = 1, Reihenfolge = 1 },
            new RankingRegelSetEintrag { Id = 2, RankingRegelSetId = 1, RankingRegelId = 2, Reihenfolge = 2 });

        ctx.Regelwerke.Add(new Regelwerk { Id = 1, Name = "Standard", RankingRegelSetId = 1 });
        ctx.PunkteRegeln.Add(new PunkteRegel
            { Id = 1, RegelwerkId = 1, PunkteFuerSieg = 3, PunkteFuerUnentschieden = 1, PunkteFuerNiederlage = 0 });
        ctx.TurnierRunden.Add(new TurnierRunde
        {
            Id = 1, TurnierInstanzId = 1, Name = "Gruppenphase", Rundenart = Rundenart.Gruppenphase,
            RegelwerkId = 1, Reihenfolge = 1, GruppenAnzahl = 3, AusgangsMannschaftsanzahl = 9
        });
        for (int i = 1; i <= 9; i++)
            ctx.Mannschaften.Add(new Mannschaft { Id = i, Name = $"Team {i}", Kurzname = $"T{i}", LandId = 1 });
        ctx.SaveChanges();

        // Volles Round-Robin je Gruppe (statt nur einer Kette wie im vorigen Test) - DirekterVergleichPunkte
        // wirkt INNERHALB einer Gruppe auf echte gemeinsame Spiele, daher braucht die Gruppenrangfolge selbst
        // konsistente Kopf-an-Kopf-Ergebnisse (kein Unentschieden/keine Zyklen), sonst wäre schon die Ermittlung
        // "wer ist Gruppendritter" unter diesem Regelwerk mehrdeutig.

        // Gruppe 1: 1 > 2 > 3 (alle Duelle eindeutig). Gruppendritter Team 3: erzielte Tore 0.
        SpielHinzufuegen(ctx, 1, rundeId: 1, heimId: 1, auswaertsId: 2, heimTore: 2, auswaertsTore: 0, siegerMannschaftId: 1);
        SpielHinzufuegen(ctx, 2, rundeId: 1, heimId: 1, auswaertsId: 3, heimTore: 3, auswaertsTore: 0, siegerMannschaftId: 1);
        SpielHinzufuegen(ctx, 3, rundeId: 1, heimId: 2, auswaertsId: 3, heimTore: 1, auswaertsTore: 0, siegerMannschaftId: 2);

        // Gruppe 2: 4 > 5 > 6. Gruppendritter Team 6: erzielte Tore 1 (verliert 0:1, dann 1:2).
        SpielHinzufuegen(ctx, 4, rundeId: 1, heimId: 4, auswaertsId: 5, heimTore: 2, auswaertsTore: 0, siegerMannschaftId: 4);
        SpielHinzufuegen(ctx, 5, rundeId: 1, heimId: 4, auswaertsId: 6, heimTore: 1, auswaertsTore: 0, siegerMannschaftId: 4);
        SpielHinzufuegen(ctx, 6, rundeId: 1, heimId: 5, auswaertsId: 6, heimTore: 2, auswaertsTore: 1, siegerMannschaftId: 5);

        // Gruppe 3: 7 > 8 > 9. Gruppendritter Team 9: erzielte Tore 2 (verliert 0:1, dann 2:3).
        SpielHinzufuegen(ctx, 7, rundeId: 1, heimId: 7, auswaertsId: 8, heimTore: 1, auswaertsTore: 0, siegerMannschaftId: 7);
        SpielHinzufuegen(ctx, 8, rundeId: 1, heimId: 7, auswaertsId: 9, heimTore: 1, auswaertsTore: 0, siegerMannschaftId: 7);
        SpielHinzufuegen(ctx, 9, rundeId: 1, heimId: 8, auswaertsId: 9, heimTore: 3, auswaertsTore: 2, siegerMannschaftId: 8);
        var service = ServiceFuer(ctx);

        var rangfolge = await service.UebergreifendeRangfolgeBerechnenAsync(1, platzierung: 3);

        rangfolge.Select(e => e.Mannschaft.Id).Should().Equal(new[] { 9, 6, 3 },
            "erzielte Tore 2 (Team 9) schlägt 1 (Team 6) schlägt 0 (Team 3) - direkter Vergleich liefert zwischen " +
            "gruppenfremden Teams stets 0 und faellt korrekt auf das zweite Kriterium durch");
    }

    // ── Issue #427: Rang-Sicherheitsprüfung mit vielen offenen Spielen ──────

    /// <summary>Fünfergruppe mit Hin- und Rückspiel vor dem ersten Ergebnis: je Team 12 fremde offene Spiele, also
    /// 6^12 Kombinationen je Richtung. Ohne Abbruch an der Ranggrenze wäre das nicht berechenbar (die Zahl passte
    /// zudem nicht in int) — der Test liefe dann nicht durch.</summary>
    [Fact]
    public async Task GruppenTabellenBerechnenAsync_VorDemErstenErgebnis_IstFuerJedesTeamNochJederRangMoeglich()
    {
        await using var ctx = NeuerContext();
        ctx.Regelwerke.Add(new Regelwerk { Id = 1, Name = "Standard" });
        ctx.PunkteRegeln.Add(new PunkteRegel
            { Id = 1, RegelwerkId = 1, PunkteFuerSieg = 3, PunkteFuerUnentschieden = 1, PunkteFuerNiederlage = 0 });
        ctx.TurnierRunden.Add(new TurnierRunde
        {
            Id = 1, TurnierInstanzId = 1, Name = "Gruppenphase", Rundenart = Rundenart.Gruppenphase,
            RegelwerkId = 1, Reihenfolge = 1, GruppenAnzahl = 1, AusgangsMannschaftsanzahl = 2, MitRueckspiel = true
        });
        for (int i = 1; i <= 5; i++)
            ctx.Mannschaften.Add(new Mannschaft { Id = i, Name = $"Team {i}", Kurzname = $"T{i}", LandId = 1 });
        ctx.SaveChanges();

        int spielId = 0;
        for (int heim = 1; heim <= 5; heim++)
            for (int auswaerts = 1; auswaerts <= 5; auswaerts++)
                if (heim != auswaerts)
                    SpielHinzufuegen(ctx, ++spielId, rundeId: 1, heim, auswaerts, heimTore: null, auswaertsTore: null,
                        istRueckspiel: heim > auswaerts);
        var service = ServiceFuer(ctx);

        var gruppen = await service.GruppenTabellenBerechnenAsync(1);

        gruppen.Single().Tabelle.Should().HaveCount(5).And.AllSatisfy(e =>
        {
            e.BesterRang.Should().Be(1);
            e.SchlechtesterRang.Should().Be(5);
            e.SicherPositioniert.Should().BeFalse();
            e.Qualifikationsstatus.Should().Be(Qualifikationsstatus.Offen);
        });
    }

    // ── Koeffizient (Issue #354) ──────────────────────────────────────────

    [Theory]
    [InlineData(true,  new[] { 3, 1, 2, 4 })]
    [InlineData(false, new[] { 1, 2, 3, 4 })]
    public async Task Koeffizient_EntscheidetNachDerStandardRangliste(
        bool mitStandardRangliste, int[] erwarteteReihenfolge)
    {
        await using var ctx = NeuerContext();
        var regeln = new[]
        {
            new RankingRegel { Id = 1, Regelart = RankingRegelArt.Punkte,       Sortierrichtung = Sortierrichtung.Absteigend },
            new RankingRegel { Id = 2, Regelart = RankingRegelArt.Koeffizient,  Sortierrichtung = Sortierrichtung.Aufsteigend },
            new RankingRegel { Id = 3, Regelart = RankingRegelArt.Losentscheid, Sortierrichtung = Sortierrichtung.Aufsteigend },
        };
        ctx.RankingRegelSets.Add(new RankingRegelSet { Id = 1, Name = "Ligaphase" });
        ctx.RankingRegeln.AddRange(regeln);
        ctx.RankingRegelSetEintraege.AddRange(regeln.Select((r, i) => new RankingRegelSetEintrag
            { Id = i + 1, RankingRegelSetId = 1, RankingRegelId = r.Id, Reihenfolge = i + 1 }));
        ctx.Regelwerke.Add(new Regelwerk { Id = 1, Name = "Ligaphase", RankingRegelSetId = 1 });
        ctx.PunkteRegeln.Add(new PunkteRegel
            { Id = 1, RegelwerkId = 1, PunkteFuerSieg = 3, PunkteFuerUnentschieden = 1, PunkteFuerNiederlage = 0 });
        ctx.TurnierRunden.Add(new TurnierRunde
        {
            Id = 1, TurnierInstanzId = 1, Name = "Ligaphase", Rundenart = Rundenart.TopfLiga, RegelwerkId = 1, Reihenfolge = 1,
            StandardRanglisteId = mitStandardRangliste ? 2 : null
        });
        ctx.Mannschaften.AddRange(Enumerable.Range(1, 4).Select(id =>
            new Mannschaft { Id = id, Name = $"Team {id}", Kurzname = $"T{id}", LandId = 1 }));
        // Nur die Standard-Rangliste der Runde zählt, nicht die andere; in ihr fehlen Team 2 und 4.
        ctx.Ranglisten.AddRange(
            RanglisteMit(1, new DateOnly(2025, 6, 1), (1, 1), (2, 2), (3, 3), (4, 4)),
            RanglisteMit(2, new DateOnly(2026, 6, 1), (3, 1), (1, 2)));
        ctx.SaveChanges();

        // Alle vier Teams punkt- und torgleich.
        SpielHinzufuegen(ctx, 1, 1, heimId: 1, auswaertsId: 2, heimTore: 1, auswaertsTore: 1);
        SpielHinzufuegen(ctx, 2, 1, heimId: 3, auswaertsId: 4, heimTore: 1, auswaertsTore: 1);

        var tabelle = await ServiceFuer(ctx).TabelleBerechnenAsync(1);

        tabelle.OrderBy(e => e.Rang).Select(e => e.Mannschaft.Id).Should().Equal(erwarteteReihenfolge);

        static Rangliste RanglisteMit(int id, DateOnly datum, params (int MannschaftId, int Rang)[] plaetze) => new()
        {
            Id = id, Name = $"Koeffizient {datum.Year}", Datum = datum, Quelle = "Test",
            Eintraege = [.. plaetze.Select(p => new RanglistenEintrag { MannschaftId = p.MannschaftId, Rang = p.Rang })]
        };
    }

    // ── Stand nach Spieltag in der Gruppenphase und Verlauf (Issue #480) ────

    // Zwei Gruppen (1/2/3 und 4/5/6). Spieltag 1: 1-2 3:0, 4-5 1:0 (Team 3 und 6 spielfrei);
    // Spieltag 2: 2-3 5:0, 5-6 0:0.
    private static async Task<TabellenService> ZweiGruppenMitZweiSpieltagenAsync(TurnierverwaltungDbContext ctx)
    {
        ctx.Regelwerke.Add(new Regelwerk { Id = 1, Name = "Standard" });
        ctx.PunkteRegeln.Add(new PunkteRegel
            { Id = 1, RegelwerkId = 1, PunkteFuerSieg = 3, PunkteFuerUnentschieden = 1, PunkteFuerNiederlage = 0 });
        ctx.TurnierRunden.Add(new TurnierRunde
        {
            Id = 1, TurnierInstanzId = 1, Name = "Gruppenphase", Rundenart = Rundenart.Gruppenphase,
            RegelwerkId = 1, Reihenfolge = 1, GruppenAnzahl = 2, AusgangsMannschaftsanzahl = 6
        });
        for (int i = 1; i <= 6; i++)
            ctx.Mannschaften.Add(new Mannschaft { Id = i, Name = $"Team {i}", Kurzname = $"T{i}", LandId = 1 });
        await ctx.SaveChangesAsync();

        SpielHinzufuegen(ctx, 1, 1, heimId: 1, auswaertsId: 2, heimTore: 3, auswaertsTore: 0, siegerMannschaftId: 1, spieltag: 1);
        SpielHinzufuegen(ctx, 2, 1, heimId: 4, auswaertsId: 5, heimTore: 1, auswaertsTore: 0, siegerMannschaftId: 4, spieltag: 1);
        SpielHinzufuegen(ctx, 3, 1, heimId: 2, auswaertsId: 3, heimTore: 5, auswaertsTore: 0, siegerMannschaftId: 2, spieltag: 2);
        SpielHinzufuegen(ctx, 4, 1, heimId: 5, auswaertsId: 6, heimTore: 0, auswaertsTore: 0, spieltag: 2);
        return ServiceFuer(ctx);
    }

    [Fact]
    public async Task UebergreifendeRangfolgeBerechnenAsync_BisSpieltag_RechnetDieGruppenZumSelbenStand()
    {
        // Nach Spieltag 1 sind die Dritten Team 2 (TD -3) und Team 5 (TD -1) — die spielfreien Teams 3 und 6
        // stehen mit 0:0 davor, wie in den Gruppentabellen. Aktuell sind es Team 3 (0 Punkte) und Team 5 (1 Punkt).
        await using var ctx = NeuerContext();
        var service = await ZweiGruppenMitZweiSpieltagenAsync(ctx);

        var nachSpieltag1 = await service.UebergreifendeRangfolgeBerechnenAsync(1, platzierung: 3, bisSpieltag: 1);
        var aktuell = await service.UebergreifendeRangfolgeBerechnenAsync(1, platzierung: 3);

        nachSpieltag1.Select(e => e.Mannschaft.Id).Should().Equal(5, 2);
        nachSpieltag1.Single(e => e.Mannschaft.Id == 2).Spiele.Should().Be(1, "nur Spieltag 1 zählt");
        aktuell.Select(e => e.Mannschaft.Id).Should().Equal(5, 3);
    }

    [Fact]
    public async Task VerlaeufeBerechnenAsync_Gruppenphase_JeGruppeEinVerlauf()
    {
        await using var ctx = NeuerContext();
        var service = await ZweiGruppenMitZweiSpieltagenAsync(ctx);

        var verlaeufe = await service.VerlaeufeBerechnenAsync(1);

        verlaeufe.Select(v => v.GruppenNummer).Should().Equal(1, 2);
        verlaeufe[0].Mannschaften.Select(m => m.Mannschaft.Id).Should().BeEquivalentTo([1, 2, 3]);
        verlaeufe[0].Mannschaften.Single(m => m.Mannschaft.Id == 3).Punkte.Select(p => p.Platz).Should().Equal(2, 3);
    }

    [Fact]
    public async Task VerlaeufeBerechnenAsync_Liga_EinVerlaufOhneGruppe()
    {
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, out int rundeId);
        SpielHinzufuegen(ctx, 1, rundeId, 1, 2, 2, 0, siegerMannschaftId: 1, spieltag: 1);
        SpielHinzufuegen(ctx, 2, rundeId, 2, 1, 1, 0, siegerMannschaftId: 2, spieltag: 2);
        var service = ServiceFuer(ctx);

        var verlauf = (await service.VerlaeufeBerechnenAsync(rundeId)).Should().ContainSingle().Subject;

        verlauf.GruppenNummer.Should().BeNull();
        verlauf.Mannschaften.Single(m => m.Mannschaft.Id == 2).Punkte.Select(p => p.Platz).Should().Equal(2, 2);
    }

    // ── Schweizer System: Team mit Freilos ohne Spiel (Issue #608) ──────────

    /// <summary>Schweizer System mit drei Teams: Team 1 schlägt Team 2 an Spieltag 1 mit 1:0, Team 3 hat laut
    /// Auslosung ein Freilos und steht noch in keinem Spiel.</summary>
    private static async Task<TabellenService> SchweizerMitFreilosAsync(TurnierverwaltungDbContext ctx)
    {
        GrundSetupAnlegen(ctx, out int rundeId);
        ctx.TurnierRunden.Single().Rundenart = Rundenart.SchweizerSystem;
        ctx.Mannschaften.Add(new Mannschaft { Id = 3, Name = "Team C", Kurzname = "TC", LandId = 1 });
        await ctx.SaveChangesAsync();
        await Spielplan.SpielplanTestdaten.SchweizerAuslosungAnlegen(ctx, rundeId, (1, 2), (3, null));
        SpielHinzufuegen(ctx, 1, rundeId, 1, 2, 1, 0, siegerMannschaftId: 1, spieltag: 1);
        return ServiceFuer(ctx);
    }

    [Fact]
    public async Task TabelleBerechnenAsync_SchweizerSystem_FreilosTeamStehtOhneSpielInDerTabelle()
    {
        await using var ctx = NeuerContext();
        var service = await SchweizerMitFreilosAsync(ctx);

        var tabelle = await service.TabelleBerechnenAsync(1);

        tabelle.Select(e => e.Mannschaft.Id).Should().Equal(1, 3, 2);
        var freilos = tabelle.Single(e => e.Mannschaft.Id == 3);
        freilos.Spiele.Should().Be(0);
        freilos.Punkte.Should().Be(0);
    }

    [Fact]
    public async Task TabelleBerechnenAsync_SchweizerSystemBisSpieltag_FreilosTeamStehtInDerTabelle()
    {
        await using var ctx = NeuerContext();
        var service = await SchweizerMitFreilosAsync(ctx);

        var tabelle = await service.TabelleBerechnenAsync(1, bisSpieltag: 1);

        tabelle.Select(e => e.Mannschaft.Id).Should().BeEquivalentTo([1, 2, 3]);
    }

    [Fact]
    public async Task LigaTabelleMitSicherheitBerechnenAsync_SchweizerSystem_FreilosTeamStehtInDerTabelle()
    {
        await using var ctx = NeuerContext();
        var service = await SchweizerMitFreilosAsync(ctx);

        var tabelle = await service.LigaTabelleMitSicherheitBerechnenAsync(1);

        tabelle.Select(e => e.Mannschaft.Id).Should().BeEquivalentTo([1, 2, 3]);
    }

    [Fact]
    public async Task VerlaeufeBerechnenAsync_SchweizerSystem_FreilosTeamIstAmErstenSpieltagSpielfrei()
    {
        await using var ctx = NeuerContext();
        var service = await SchweizerMitFreilosAsync(ctx);

        var verlauf = (await service.VerlaeufeBerechnenAsync(1)).Should().ContainSingle().Subject;

        var punkt = verlauf.Mannschaften.Single(m => m.Mannschaft.Id == 3).Punkte.Should().ContainSingle().Subject;
        punkt.Platz.Should().Be(2);
        punkt.SpieleAmSpieltag.Should().BeEmpty();
    }

    [Fact]
    public async Task TabelleBerechnenAsync_Liga_ZiehtKeineTeamsAusDerAuslosungHinzu()
    {
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, out int rundeId);
        ctx.Mannschaften.Add(new Mannschaft { Id = 3, Name = "Team C", Kurzname = "TC", LandId = 1 });
        await ctx.SaveChangesAsync();
        await Spielplan.SpielplanTestdaten.SchweizerAuslosungAnlegen(ctx, rundeId, (1, 2), (3, null));
        SpielHinzufuegen(ctx, 1, rundeId, 1, 2, 1, 0, siegerMannschaftId: 1, spieltag: 1);

        var tabelle = await ServiceFuer(ctx).TabelleBerechnenAsync(rundeId);

        tabelle.Select(e => e.Mannschaft.Id).Should().BeEquivalentTo([1, 2]);
    }
}
