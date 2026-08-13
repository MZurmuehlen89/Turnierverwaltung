using FluentAssertions;
using Fussballturnierverwaltung.Application.Spielplan;
using Fussballturnierverwaltung.Application.Spielplan.Eingaben;
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

public class UebergangsregelServiceTests
{
    private static TurnierverwaltungDbContext NeuerContext() =>
        new(new DbContextOptionsBuilder<TurnierverwaltungDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static UebergangsregelService ServiceFuer(TurnierverwaltungDbContext ctx)
    {
        var rundeRepo = new TurnierRundeRepository(ctx);
        var spielRepo = new SpielRepository(ctx);
        var mannschaftRepo = new MannschaftRepository(ctx);
        var baumRepo = new TurnierBaumRepository(ctx);
        var uebergangsregelRepo = new UebergangsregelRepository(ctx);
        var factory = new SpielplanGeneratorFactory([new KoRundeGenerator(), new RoundRobinGenerator()]);
        var spielplanService = new SpielplanService(
            rundeRepo, spielRepo, mannschaftRepo, baumRepo, uebergangsregelRepo,
            factory, new TurnierValidator(), new TabellenService(rundeRepo, spielRepo, new RegelwerkRepository(ctx)),
            NullLogger<SpielplanService>.Instance);

        return new UebergangsregelService(
            uebergangsregelRepo, rundeRepo, new UebergangsregelValidator(),
            spielplanService, NullLogger<UebergangsregelService>.Instance);
    }

    private static void GrundSetupAnlegen(TurnierverwaltungDbContext ctx, out int gruppenphaseRundeId, out int koRundeId)
    {
        ctx.Regelwerke.Add(new Regelwerk { Id = 1, Name = "Standard" });
        var gruppenphase = new TurnierRunde
        {
            Id = 1, TurnierInstanzId = 1, Name = "Gruppenphase", Reihenfolge = 1,
            Rundenart = Rundenart.Gruppenphase, EingangsMannschaftsanzahl = 8, AusgangsMannschaftsanzahl = 4,
            RegelwerkId = 1, GruppenAnzahl = 2
        };
        var ko = new TurnierRunde
        {
            Id = 2, TurnierInstanzId = 1, Name = "Halbfinale", Reihenfolge = 2,
            Rundenart = Rundenart.KoRunde, EingangsMannschaftsanzahl = 4, AusgangsMannschaftsanzahl = 2,
            RegelwerkId = 1
        };
        ctx.TurnierRunden.AddRange(gruppenphase, ko);
        ctx.SaveChanges();
        gruppenphaseRundeId = gruppenphase.Id;
        koRundeId = ko.Id;
    }

    [Fact]
    public async Task ErstellenAsync_GueltigeEingabe_LegtRegelAn()
    {
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, out int gruppenphaseRundeId, out int koRundeId);
        var service = ServiceFuer(ctx);

        var regel = await service.ErstellenAsync(new UebergangsregelEingabe(
            gruppenphaseRundeId, koRundeId, GruppeIndex: 1, PlatzierungVon: 1, PlatzierungBis: 2));

        regel.Id.Should().BeGreaterThan(0);
        (await service.AlleNachQuellRundeIdAsync(gruppenphaseRundeId)).Should().HaveCount(1);
    }

    [Fact]
    public async Task ErstellenAsync_UngueltigeEingabe_WirftValidationException()
    {
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, out int gruppenphaseRundeId, out int koRundeId);
        var service = ServiceFuer(ctx);

        var eingabe = new UebergangsregelEingabe(
            gruppenphaseRundeId, koRundeId, GruppeIndex: null, PlatzierungVon: 1, PlatzierungBis: 1);

        await Assert.ThrowsAsync<ValidationException>(() => service.ErstellenAsync(eingabe));
    }

    [Fact]
    public async Task AktualisierenAsync_AendertPlatzierungsbereich()
    {
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, out int gruppenphaseRundeId, out int koRundeId);
        var service = ServiceFuer(ctx);
        var regel = await service.ErstellenAsync(new UebergangsregelEingabe(
            gruppenphaseRundeId, koRundeId, GruppeIndex: 1, PlatzierungVon: 1, PlatzierungBis: 1));

        await service.AktualisierenAsync(regel.Id, new UebergangsregelEingabe(
            gruppenphaseRundeId, koRundeId, GruppeIndex: 1, PlatzierungVon: 1, PlatzierungBis: 2));

        var regeln = await service.AlleNachQuellRundeIdAsync(gruppenphaseRundeId);
        regeln.Single().PlatzierungBis.Should().Be(2);
    }

    [Fact]
    public async Task LoeschenAsync_EntferntRegel()
    {
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, out int gruppenphaseRundeId, out int koRundeId);
        var service = ServiceFuer(ctx);
        var regel = await service.ErstellenAsync(new UebergangsregelEingabe(
            gruppenphaseRundeId, koRundeId, GruppeIndex: 1, PlatzierungVon: 1, PlatzierungBis: 1));

        await service.LoeschenAsync(regel.Id);

        (await service.AlleNachQuellRundeIdAsync(gruppenphaseRundeId)).Should().BeEmpty();
    }

    [Fact]
    public async Task QualifikantenErmittelnAsync_LoestGruppenrangAufSobaldEntschieden()
    {
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, out int gruppenphaseRundeId, out int koRundeId);
        ctx.Mannschaften.AddRange(
            new Mannschaft { Id = 1, Name = "Team A", Kurzname = "TA", LandId = 1 },
            new Mannschaft { Id = 2, Name = "Team B", Kurzname = "TB", LandId = 1 });
        ctx.Spiele.Add(new Spiel
        {
            Id = 1, TurnierInstanzId = 1, TurnierRundeId = gruppenphaseRundeId, Spielnummer = 1,
            HeimMannschaftId = 1, AuswaertsMannschaftId = 2, Status = SpielStatus.Abgeschlossen
        });
        ctx.SpielErgebnisse.Add(new SpielErgebnis
            { Id = 1, SpielId = 1, HeimToreRegulaer = 2, AuswaertsToreRegulaer = 0, EntscheidungArt = EntscheidungArt.RegulaereSpielzeit });
        ctx.SaveChanges();
        var service = ServiceFuer(ctx);
        var regel = await service.ErstellenAsync(new UebergangsregelEingabe(
            gruppenphaseRundeId, koRundeId, GruppeIndex: 1, PlatzierungVon: 1, PlatzierungBis: 2));

        var qualifikanten = await service.QualifikantenErmittelnAsync(regel.Id);

        qualifikanten[1].Should().Be(1);
        qualifikanten[2].Should().Be(2);
    }

    [Fact]
    public async Task ErstellenAsync_WuerdeZyklusSchliessen_WirftValidationException()
    {
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, out int gruppenphaseRundeId, out int koRundeId);
        var service = ServiceFuer(ctx);
        await service.ErstellenAsync(new UebergangsregelEingabe(
            gruppenphaseRundeId, koRundeId, GruppeIndex: 1, PlatzierungVon: 1, PlatzierungBis: 2));

        // Zweite Regel in Gegenrichtung (koRunde -> gruppenphaseRundeId) würde einen Zyklus schließen.
        var zyklischeEingabe = new UebergangsregelEingabe(
            koRundeId, gruppenphaseRundeId, GruppeIndex: 1, PlatzierungVon: 1, PlatzierungBis: 1);

        await Assert.ThrowsAsync<ValidationException>(() => service.ErstellenAsync(zyklischeEingabe));
    }

    // ── BlockSpeichernAsync (Issue #180) ────────────────────────────────────────

    [Fact]
    public async Task BlockSpeichernAsync_UngueltigerBatch_PersistiertKeineZeile()
    {
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, out int gruppenphaseRundeId, out int koRundeId);
        var service = ServiceFuer(ctx);

        // koRunde (Zielrunde) erwartet 4 Mannschaften. Jede Zeile für sich (Platz 1-3, Gruppengröße 4) wäre
        // gültig, aber zusammen ergeben sie 6 > 4 — erst über den gesamten Batch erkennbar.
        var zeilen = new[]
        {
            new UebergangsregelZeileEingabe(null, GruppeIndex: 1, PlatzierungVon: 1, PlatzierungBis: 3),
            new UebergangsregelZeileEingabe(null, GruppeIndex: 2, PlatzierungVon: 1, PlatzierungBis: 3),
        };

        await Assert.ThrowsAsync<ValidationException>(
            () => service.BlockSpeichernAsync(gruppenphaseRundeId, koRundeId, zeilen, zuLoeschendeIds: []));

        // Beweis für die Atomarität des Fixes: keine der beiden Zeilen wurde vor dem Validierungsfehler geschrieben.
        (await service.AlleNachQuellRundeIdAsync(gruppenphaseRundeId)).Should().BeEmpty();
    }

    [Fact]
    public async Task BlockSpeichernAsync_GueltigerBatch_ErstelltAktualisiertUndLoeschtInEinemAufruf()
    {
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, out int gruppenphaseRundeId, out int koRundeId);
        var service = ServiceFuer(ctx);

        var bleibt = await service.ErstellenAsync(new UebergangsregelEingabe(
            gruppenphaseRundeId, koRundeId, GruppeIndex: 1, PlatzierungVon: 1, PlatzierungBis: 1));
        var wirdGeloescht = await service.ErstellenAsync(new UebergangsregelEingabe(
            gruppenphaseRundeId, koRundeId, GruppeIndex: 2, PlatzierungVon: 1, PlatzierungBis: 1));

        var zeilen = new[]
        {
            new UebergangsregelZeileEingabe(bleibt.Id, GruppeIndex: 1, PlatzierungVon: 1, PlatzierungBis: 2),
            new UebergangsregelZeileEingabe(null, GruppeIndex: 2, PlatzierungVon: 1, PlatzierungBis: 2),
        };

        var ergebnis = await service.BlockSpeichernAsync(
            gruppenphaseRundeId, koRundeId, zeilen, zuLoeschendeIds: [wirdGeloescht.Id]);

        ergebnis.Should().HaveCount(2);
        var gespeichert = await service.AlleNachQuellRundeIdAsync(gruppenphaseRundeId);
        gespeichert.Should().HaveCount(2);
        gespeichert.Should().Contain(r => r.Id == bleibt.Id && r.PlatzierungBis == 2);
        gespeichert.Should().NotContain(r => r.Id == wirdGeloescht.Id);
    }
}
