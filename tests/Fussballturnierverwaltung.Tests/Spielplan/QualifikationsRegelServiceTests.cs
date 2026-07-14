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

public class QualifikationsRegelServiceTests
{
    private static TurnierverwaltungDbContext NeuerContext() =>
        new(new DbContextOptionsBuilder<TurnierverwaltungDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static QualifikationsRegelService ServiceFuer(TurnierverwaltungDbContext ctx)
    {
        var rundeRepo = new TurnierRundeRepository(ctx);
        var spielRepo = new SpielRepository(ctx);
        var mannschaftRepo = new MannschaftRepository(ctx);
        var baumRepo = new TurnierBaumRepository(ctx);
        var qualifikationsRegelRepo = new QualifikationsRegelRepository(ctx);
        var factory = new SpielplanGeneratorFactory([new KoRundeGenerator(), new RoundRobinGenerator()]);
        var spielplanService = new SpielplanService(
            rundeRepo, spielRepo, mannschaftRepo, baumRepo, qualifikationsRegelRepo,
            factory, new TurnierValidator(), new TabellenService(rundeRepo, spielRepo, new RegelwerkRepository(ctx)),
            NullLogger<SpielplanService>.Instance);

        return new QualifikationsRegelService(
            qualifikationsRegelRepo, rundeRepo, new QualifikationsRegelValidator(),
            spielplanService, NullLogger<QualifikationsRegelService>.Instance);
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

        var regel = await service.ErstellenAsync(new QualifikationsRegelEingabe(
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

        var eingabe = new QualifikationsRegelEingabe(
            gruppenphaseRundeId, koRundeId, GruppeIndex: null, PlatzierungVon: 1, PlatzierungBis: 1);

        await Assert.ThrowsAsync<ValidationException>(() => service.ErstellenAsync(eingabe));
    }

    [Fact]
    public async Task AktualisierenAsync_AendertPlatzierungsbereich()
    {
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, out int gruppenphaseRundeId, out int koRundeId);
        var service = ServiceFuer(ctx);
        var regel = await service.ErstellenAsync(new QualifikationsRegelEingabe(
            gruppenphaseRundeId, koRundeId, GruppeIndex: 1, PlatzierungVon: 1, PlatzierungBis: 1));

        await service.AktualisierenAsync(regel.Id, new QualifikationsRegelEingabe(
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
        var regel = await service.ErstellenAsync(new QualifikationsRegelEingabe(
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
        var regel = await service.ErstellenAsync(new QualifikationsRegelEingabe(
            gruppenphaseRundeId, koRundeId, GruppeIndex: 1, PlatzierungVon: 1, PlatzierungBis: 2));

        var qualifikanten = await service.QualifikantenErmittelnAsync(regel.Id);

        qualifikanten[1].Should().Be(1);
        qualifikanten[2].Should().Be(2);
    }
}
