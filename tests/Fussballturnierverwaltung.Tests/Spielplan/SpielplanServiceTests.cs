using FluentAssertions;
using Fussballturnierverwaltung.Application.Spielplan;
using Fussballturnierverwaltung.Application.Tabelle;
using Fussballturnierverwaltung.Application.Validierung;
using Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.Infrastructure.Persistenz;
using Fussballturnierverwaltung.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fussballturnierverwaltung.Tests.Spielplan;

public class SpielplanServiceTests
{
    // ── Hilfsmethoden ─────────────────────────────────────────────────────

    private static TurnierverwaltungDbContext NeuerContext() =>
        new(new DbContextOptionsBuilder<TurnierverwaltungDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static SpielplanService ServiceFuer(TurnierverwaltungDbContext ctx)
    {
        var rundeRepo = new TurnierRundeRepository(ctx);
        var spielRepo = new SpielRepository(ctx);
        var mannschaftRepo = new MannschaftRepository(ctx);
        var baumRepo = new TurnierBaumRepository(ctx);
        var factory = new SpielplanGeneratorFactory([new KoRundeGenerator()]);
        return new SpielplanService(
            rundeRepo, spielRepo, mannschaftRepo, baumRepo,
            factory, new TurnierValidator(), new TabellenService(rundeRepo, spielRepo, new RegelwerkRepository(ctx)),
            NullLogger<SpielplanService>.Instance);
    }

    /// <summary>Legt Regelwerk (mit Entscheidungsverfahren, für KO/Finale Pflicht), Halbfinale- und Finale-Runde
    /// sowie 4 Mannschaften an. Halbfinale-Spiele werden NICHT angelegt (Aufgabe der einzelnen Tests).</summary>
    private static void GrundSetupAnlegen(
        TurnierverwaltungDbContext ctx, out int hfRundeId, out int finaleRundeId, bool hfMitRueckspiel = false)
    {
        ctx.Verbaende.Add(new Verband { Id = 1, Name = "Verband", Kurzname = "V" });
        ctx.Laender.Add(new Land { Id = 1, Name = "Land", Kurzname = "L", FifaCode = "LAN", VerbandId = 1 });
        ctx.Regelwerke.Add(new Regelwerk { Id = 1, Name = "Standard" });
        ctx.Entscheidungsverfahren.Add(new EntscheidungsVerfahren
            { Id = 1, RegelwerkId = 1, Reihenfolge = 1, VerfahrenArt = VerfahrenArt.Elfmeterschiessen });

        var hf = new TurnierRunde
        {
            Id = 1, TurnierInstanzId = 1, Name = "Halbfinale", Reihenfolge = 1,
            Rundenart = Rundenart.KoRunde, EingangsMannschaftsanzahl = 4, AusgangsMannschaftsanzahl = 2,
            RegelwerkId = 1, MitRueckspiel = hfMitRueckspiel
        };
        var finale = new TurnierRunde
        {
            Id = 2, TurnierInstanzId = 1, Name = "Finale", Reihenfolge = 2,
            Rundenart = Rundenart.Finale, EingangsMannschaftsanzahl = 2, AusgangsMannschaftsanzahl = 1,
            RegelwerkId = 1
        };
        ctx.TurnierRunden.AddRange(hf, finale);

        ctx.Mannschaften.AddRange(
            new Mannschaft { Id = 1, Name = "Team A", Kurzname = "TA", LandId = 1 },
            new Mannschaft { Id = 2, Name = "Team B", Kurzname = "TB", LandId = 1 },
            new Mannschaft { Id = 3, Name = "Team C", Kurzname = "TC", LandId = 1 },
            new Mannschaft { Id = 4, Name = "Team D", Kurzname = "TD", LandId = 1 });

        ctx.SaveChanges();
        hfRundeId = hf.Id;
        finaleRundeId = finale.Id;
    }

    private static void HalbfinalSpielAnlegen(
        TurnierverwaltungDbContext ctx, int id, int rundeId,
        int heimId, int auswaertsId, int? siegerMannschaftId)
    {
        ctx.Spiele.Add(new Spiel
        {
            Id = id, TurnierInstanzId = 1, TurnierRundeId = rundeId,
            Spielnummer = id, HeimMannschaftId = heimId, AuswaertsMannschaftId = auswaertsId,
            Status = siegerMannschaftId.HasValue ? SpielStatus.Abgeschlossen : SpielStatus.Geplant,
            IstRueckspiel = false
        });
        if (siegerMannschaftId.HasValue)
        {
            ctx.SpielErgebnisse.Add(new SpielErgebnis
            {
                Id = id, SpielId = id,
                HeimToreRegulaer = siegerMannschaftId == heimId ? 1 : 0,
                AuswaertsToreRegulaer = siegerMannschaftId == auswaertsId ? 1 : 0,
                EntscheidungArt = EntscheidungArt.RegulaereSpielzeit,
                SiegerMannschaftId = siegerMannschaftId
            });
        }
        ctx.SaveChanges();
    }

    // ── Tests: automatische Finale-Generierung ohne Auslosung (Issue #113) ─

    [Fact]
    public async Task FinaleAutomatischGenerierenWennBereitAsync_GeneriertFinale_WennHalbfinaleEntschieden()
    {
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, out int hfRundeId, out int finaleRundeId);
        HalbfinalSpielAnlegen(ctx, 1, hfRundeId, heimId: 1, auswaertsId: 2, siegerMannschaftId: 1);
        HalbfinalSpielAnlegen(ctx, 2, hfRundeId, heimId: 3, auswaertsId: 4, siegerMannschaftId: 3);
        var service = ServiceFuer(ctx);

        await service.FinaleAutomatischGenerierenWennBereitAsync(1);

        var finaleSpiele = ctx.Spiele.Where(s => s.TurnierRundeId == finaleRundeId).ToList();
        finaleSpiele.Should().HaveCount(1);
        var teilnehmer = new[] { finaleSpiele[0].HeimMannschaftId, finaleSpiele[0].AuswaertsMannschaftId };
        teilnehmer.Should().BeEquivalentTo(new int?[] { 1, 3 });
    }

    [Fact]
    public async Task FinaleAutomatischGenerierenWennBereitAsync_GeneriertNicht_WennHalbfinaleNochOffen()
    {
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, out int hfRundeId, out int finaleRundeId);
        HalbfinalSpielAnlegen(ctx, 1, hfRundeId, heimId: 1, auswaertsId: 2, siegerMannschaftId: 1);
        HalbfinalSpielAnlegen(ctx, 2, hfRundeId, heimId: 3, auswaertsId: 4, siegerMannschaftId: null);
        var service = ServiceFuer(ctx);

        await service.FinaleAutomatischGenerierenWennBereitAsync(1);

        ctx.Spiele.Where(s => s.TurnierRundeId == finaleRundeId).Should().BeEmpty();
    }

    [Fact]
    public async Task FinaleAutomatischGenerierenWennBereitAsync_GeneriertNichtErneut_WennBereitsVorhanden()
    {
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, out int hfRundeId, out int finaleRundeId);
        HalbfinalSpielAnlegen(ctx, 1, hfRundeId, heimId: 1, auswaertsId: 2, siegerMannschaftId: 1);
        HalbfinalSpielAnlegen(ctx, 2, hfRundeId, heimId: 3, auswaertsId: 4, siegerMannschaftId: 3);
        // Finale wurde bereits (z.B. manuell) generiert
        ctx.Spiele.Add(new Spiel
        {
            Id = 99, TurnierInstanzId = 1, TurnierRundeId = finaleRundeId,
            Spielnummer = 1, HeimMannschaftId = 1, AuswaertsMannschaftId = 3, Status = SpielStatus.Geplant
        });
        ctx.SaveChanges();
        var service = ServiceFuer(ctx);

        await service.FinaleAutomatischGenerierenWennBereitAsync(1);

        ctx.Spiele.Where(s => s.TurnierRundeId == finaleRundeId).Should().HaveCount(1,
            "es darf kein zweites Finalspiel automatisch hinzugefügt werden");
    }

    [Fact]
    public async Task FinaleAutomatischGenerierenWennBereitAsync_GeneriertNicht_BeiHalbfinaleMitRueckspiel()
    {
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, out int hfRundeId, out int finaleRundeId, hfMitRueckspiel: true);
        HalbfinalSpielAnlegen(ctx, 1, hfRundeId, heimId: 1, auswaertsId: 2, siegerMannschaftId: 1);
        HalbfinalSpielAnlegen(ctx, 2, hfRundeId, heimId: 3, auswaertsId: 4, siegerMannschaftId: 3);
        var service = ServiceFuer(ctx);

        await service.FinaleAutomatischGenerierenWennBereitAsync(1);

        ctx.Spiele.Where(s => s.TurnierRundeId == finaleRundeId).Should().BeEmpty(
            "Aggregat-Sieger-Ermittlung über zwei Spielbeine ist bewusst nicht Teil dieser Phase");
    }

    [Fact]
    public async Task FinaleAutomatischGenerierenWennBereitAsync_EntferntFinale_WennHalbfinalErgebnisZurueckgesetztWird()
    {
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, out int hfRundeId, out int finaleRundeId);
        HalbfinalSpielAnlegen(ctx, 1, hfRundeId, heimId: 1, auswaertsId: 2, siegerMannschaftId: 1);
        HalbfinalSpielAnlegen(ctx, 2, hfRundeId, heimId: 3, auswaertsId: 4, siegerMannschaftId: 3);
        var service = ServiceFuer(ctx);
        await service.FinaleAutomatischGenerierenWennBereitAsync(1);
        ctx.Spiele.Where(s => s.TurnierRundeId == finaleRundeId).Should().HaveCount(1, "Vorbedingung: Finale wurde generiert");

        // Ergebnis eines Halbfinalspiels zurückgesetzt (z.B. "Ergebnis löschen")
        var spiel2Ergebnis = ctx.SpielErgebnisse.First(e => e.SpielId == 2);
        ctx.SpielErgebnisse.Remove(spiel2Ergebnis);
        var spiel2 = ctx.Spiele.First(s => s.Id == 2);
        spiel2.Status = SpielStatus.Geplant;
        ctx.SaveChanges();

        await service.FinaleAutomatischGenerierenWennBereitAsync(1);

        ctx.Spiele.Where(s => s.TurnierRundeId == finaleRundeId).Should().BeEmpty(
            "das noch nicht gespielte Finale muss entfernt werden, wenn das Halbfinale nicht mehr entschieden ist");
    }

    [Fact]
    public async Task FinaleAutomatischGenerierenWennBereitAsync_RegeneriertFinale_WennHalbfinaleAndersAusgeht()
    {
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, out int hfRundeId, out int finaleRundeId);
        HalbfinalSpielAnlegen(ctx, 1, hfRundeId, heimId: 1, auswaertsId: 2, siegerMannschaftId: 1);
        HalbfinalSpielAnlegen(ctx, 2, hfRundeId, heimId: 3, auswaertsId: 4, siegerMannschaftId: 3);
        var service = ServiceFuer(ctx);
        await service.FinaleAutomatischGenerierenWennBereitAsync(1);

        // Halbfinale neu simuliert - diesmal gewinnen Team B und Team D statt Team A und Team C
        var erg1 = ctx.SpielErgebnisse.First(e => e.SpielId == 1);
        erg1.SiegerMannschaftId = 2; erg1.HeimToreRegulaer = 0; erg1.AuswaertsToreRegulaer = 1;
        var erg2 = ctx.SpielErgebnisse.First(e => e.SpielId == 2);
        erg2.SiegerMannschaftId = 4; erg2.HeimToreRegulaer = 0; erg2.AuswaertsToreRegulaer = 1;
        ctx.SaveChanges();

        await service.FinaleAutomatischGenerierenWennBereitAsync(1);

        var finaleSpiele = ctx.Spiele.Where(s => s.TurnierRundeId == finaleRundeId).ToList();
        finaleSpiele.Should().HaveCount(1);
        var teilnehmer = new[] { finaleSpiele[0].HeimMannschaftId, finaleSpiele[0].AuswaertsMannschaftId };
        teilnehmer.Should().BeEquivalentTo(new int?[] { 2, 4 });
    }

    [Fact]
    public async Task FinaleAutomatischGenerierenWennBereitAsync_RuehrtBereitsGespieltesFinaleNichtAn()
    {
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, out int hfRundeId, out int finaleRundeId);
        HalbfinalSpielAnlegen(ctx, 1, hfRundeId, heimId: 1, auswaertsId: 2, siegerMannschaftId: 1);
        HalbfinalSpielAnlegen(ctx, 2, hfRundeId, heimId: 3, auswaertsId: 4, siegerMannschaftId: 3);
        var service = ServiceFuer(ctx);
        await service.FinaleAutomatischGenerierenWennBereitAsync(1);

        // Finale wurde bereits gespielt
        var finaleSpielId = ctx.Spiele.First(s => s.TurnierRundeId == finaleRundeId).Id;
        ctx.Spiele.First(s => s.Id == finaleSpielId).Status = SpielStatus.Abgeschlossen;
        ctx.SpielErgebnisse.Add(new SpielErgebnis
        {
            Id = finaleSpielId, SpielId = finaleSpielId,
            HeimToreRegulaer = 2, AuswaertsToreRegulaer = 1,
            EntscheidungArt = EntscheidungArt.RegulaereSpielzeit, SiegerMannschaftId = 1
        });
        ctx.SaveChanges();

        // Halbfinale nachträglich geändert
        var erg2 = ctx.SpielErgebnisse.First(e => e.SpielId == 2);
        erg2.SiegerMannschaftId = 4;
        ctx.SaveChanges();

        await service.FinaleAutomatischGenerierenWennBereitAsync(1);

        var finaleSpiele = ctx.Spiele.Where(s => s.TurnierRundeId == finaleRundeId).ToList();
        finaleSpiele.Should().HaveCount(1, "ein bereits gespieltes Finale darf nie automatisch verändert werden");
        finaleSpiele[0].Id.Should().Be(finaleSpielId);
        var teilnehmer = new[] { finaleSpiele[0].HeimMannschaftId, finaleSpiele[0].AuswaertsMannschaftId };
        teilnehmer.Should().BeEquivalentTo(new int?[] { 1, 3 }, "die ursprüngliche, bereits gespielte Paarung bleibt erhalten");
    }
}
