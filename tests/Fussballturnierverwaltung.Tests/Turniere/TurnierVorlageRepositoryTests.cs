using FluentAssertions;
using Fussballturnierverwaltung.Application.Turniere.Eingaben;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.Infrastructure.Persistenz;
using Fussballturnierverwaltung.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Fussballturnierverwaltung.Tests.Turniere;

public class TurnierVorlageRepositoryTests
{
    private static TurnierverwaltungDbContext NeuerContext() =>
        new(new DbContextOptionsBuilder<TurnierverwaltungDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static async Task<int> VorlageAnlegenAsync(TurnierverwaltungDbContext ctx)
    {
        var vorlage = new TurnierVorlage { Name = "Test-Format", ErforderlicheMannschaftsanzahl = 8 };
        ctx.TurnierVorlagen.Add(vorlage);
        await ctx.SaveChangesAsync();
        return vorlage.Id;
    }

    private static List<VorlagenRunde> ZweiRunden(int vorlageId) =>
    [
        new() { TurnierVorlageId = vorlageId, Name = "Gruppenphase", Reihenfolge = 1, Rundenart = Rundenart.Gruppenphase, EingangsMannschaftsanzahl = 8, AusgangsMannschaftsanzahl = 4, GruppenAnzahl = 2 },
        new() { TurnierVorlageId = vorlageId, Name = "Halbfinale", Reihenfolge = 2, Rundenart = Rundenart.KoRunde, EingangsMannschaftsanzahl = 4, AusgangsMannschaftsanzahl = 2 }
    ];

    [Fact]
    public async Task RundenErsetzenAsync_SpeichertGruppenAnzahlDerGruppenphaseRunde()
    {
        using var ctx = NeuerContext();
        var vorlageId = await VorlageAnlegenAsync(ctx);
        var repo = new TurnierVorlageRepository(ctx);

        await repo.RundenErsetzenAsync(vorlageId, ZweiRunden(vorlageId), []);

        var geladen = await repo.FindenNachIdMitRundenAsync(vorlageId);
        geladen!.VorlagenRunden.Single(r => r.Reihenfolge == 1).GruppenAnzahl.Should().Be(2);
    }

    [Fact]
    public async Task RundenErsetzenAsync_LoestReihenfolgeAufReeleIdsDerNeuAngelegtenRundenAuf()
    {
        using var ctx = NeuerContext();
        var vorlageId = await VorlageAnlegenAsync(ctx);
        var repo = new TurnierVorlageRepository(ctx);
        var runden = ZweiRunden(vorlageId);

        await repo.RundenErsetzenAsync(vorlageId, runden,
            [new VorlagenUebergangsRegelEingabe(QuellReihenfolge: 1, ZielReihenfolge: 2, GruppeIndex: null, PlatzierungVon: 1, PlatzierungBis: 2)]);

        var gespeicherteRegeln = await ctx.VorlagenUebergangsregeln.ToListAsync();
        gespeicherteRegeln.Should().ContainSingle();
        var regel = gespeicherteRegeln[0];
        regel.QuellVorlagenRundeId.Should().Be(runden[0].Id);
        regel.ZielVorlagenRundeId.Should().Be(runden[1].Id);
        regel.PlatzierungVon.Should().Be(1);
        regel.PlatzierungBis.Should().Be(2);
    }

    [Fact]
    public async Task RundenErsetzenAsync_ZweiterAufrufErsetztAlteRundenUndRegelnVollstaendig()
    {
        using var ctx = NeuerContext();
        var vorlageId = await VorlageAnlegenAsync(ctx);
        var repo = new TurnierVorlageRepository(ctx);
        await repo.RundenErsetzenAsync(vorlageId, ZweiRunden(vorlageId),
            [new VorlagenUebergangsRegelEingabe(1, 2, null, 1, 2)]);

        // Zweiter Sync mit nur einer Runde (kein gültiges Ziel mehr für die alte Regel) — darf keine
        // verwaisten Uebergangsregeln aus dem ersten Durchlauf zurücklassen.
        var einzelneRunde = new List<VorlagenRunde>
        {
            new() { TurnierVorlageId = vorlageId, Name = "Liga", Reihenfolge = 1, Rundenart = Rundenart.Liga, EingangsMannschaftsanzahl = 8, AusgangsMannschaftsanzahl = 8 }
        };
        await repo.RundenErsetzenAsync(vorlageId, einzelneRunde, []);

        (await ctx.VorlagenUebergangsregeln.ToListAsync()).Should().BeEmpty();
        (await ctx.VorlagenRunden.Where(r => r.TurnierVorlageId == vorlageId).ToListAsync()).Should().ContainSingle();
    }

    [Fact]
    public async Task FindenNachIdMitRundenAsync_LaedtAusgehendeUebergangsregelnMitZielrunde()
    {
        using var ctx = NeuerContext();
        var vorlageId = await VorlageAnlegenAsync(ctx);
        var repo = new TurnierVorlageRepository(ctx);
        await repo.RundenErsetzenAsync(vorlageId, ZweiRunden(vorlageId),
            [new VorlagenUebergangsRegelEingabe(1, 2, null, 1, 2)]);

        var geladen = await repo.FindenNachIdMitRundenAsync(vorlageId);

        geladen.Should().NotBeNull();
        var quellRunde = geladen!.VorlagenRunden.Single(r => r.Reihenfolge == 1);
        quellRunde.AusgehendeUebergangsregeln.Should().ContainSingle();
        quellRunde.AusgehendeUebergangsregeln.Single().ZielVorlagenRunde.Reihenfolge.Should().Be(2);
    }
}
