using FluentAssertions;
using Fussballturnierverwaltung.Application.Stammdaten;
using Fussballturnierverwaltung.Application.Validierung;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Infrastructure.Persistenz;
using Fussballturnierverwaltung.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fussballturnierverwaltung.Tests.Stammdaten;

/// <summary>Löschen von Verband und Land mit noch zugeordneten Datensätzen (#559).</summary>
public class LandUndVerbandLoeschenTests
{
    private static TurnierverwaltungDbContext NeuerContext() =>
        new(new DbContextOptionsBuilder<TurnierverwaltungDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static VerbandService VerbandServiceFuer(TurnierverwaltungDbContext ctx) =>
        new(new VerbandRepository(ctx), new VerbandValidator(), NullLogger<VerbandService>.Instance);

    private static LandService LandServiceFuer(TurnierverwaltungDbContext ctx) =>
        new(new LandRepository(ctx), new LandValidator(), NullLogger<LandService>.Instance);

    // Verband 1 (UEFA) mit Deutschland und Quoten in zwei Turnieren; Verband 2 (OFC) ohne alles.
    // Deutschland hat zwei Mannschaften und zwei Kontingente im selben Turnier; Malta hat nichts.
    private static void Anlegen(TurnierverwaltungDbContext ctx)
    {
        ctx.Verbaende.AddRange(
            new Verband { Id = 1, Name = "UEFA", Kurzname = "UEFA" },
            new Verband { Id = 2, Name = "OFC", Kurzname = "OFC" });
        ctx.Laender.AddRange(
            new Land { Id = 1, Name = "Deutschland", Kurzname = "GER", FifaCode = "GER", VerbandId = 1 },
            new Land { Id = 2, Name = "Malta", Kurzname = "MLT", FifaCode = "MLT", VerbandId = 1 });
        ctx.Mannschaften.AddRange(
            new Mannschaft { Id = 1, Name = "FC Bayern", Kurzname = "FCB", LandId = 1 },
            new Mannschaft { Id = 2, Name = "Borussia Dortmund", Kurzname = "BVB", LandId = 1 });
        ctx.TurnierInstanzen.AddRange(
            new TurnierInstanz { Id = 1, Name = "Champions League", SaisonOderJahr = "2026" },
            new TurnierInstanz { Id = 2, Name = "Klub-WM", SaisonOderJahr = "2029" });
        ctx.VerbandsQuoten.AddRange(
            new VerbandsQuote { TurnierInstanzId = 1, VerbandId = 1, Anzahl = 36 },
            new VerbandsQuote { TurnierInstanzId = 2, VerbandId = 1, Anzahl = 12 });
        ctx.LandesQuoten.AddRange(
            new LandesQuote { TurnierInstanzId = 1, LandId = 1, Anzahl = 4, Spielklasse = 1, AbPlatz = 1 },
            new LandesQuote { TurnierInstanzId = 1, LandId = 1, Anzahl = 1, Pokalsieger = true });
        ctx.SaveChanges();
        ctx.ChangeTracker.Clear();
    }

    [Fact]
    public async Task VerbandMitLaendern_LoeschSperreNenntLaenderUndTurniere()
    {
        await using var ctx = NeuerContext();
        Anlegen(ctx);

        var sperre = await VerbandServiceFuer(ctx).LoeschSperreAsync(1);

        sperre.Should().Be(
            "Der Verband „UEFA“ kann nicht gelöscht werden. Noch zugeordnet: 2 Länder, Verbandsquote in 2 Turnieren.");
    }

    [Fact]
    public async Task VerbandMitLaendern_LoeschenWirftSperrtextUndLaesstVerbandStehen()
    {
        await using var ctx = NeuerContext();
        Anlegen(ctx);

        var loeschen = () => VerbandServiceFuer(ctx).LoeschenAsync(1);

        await loeschen.Should().ThrowAsync<InvalidOperationException>().WithMessage("Der Verband „UEFA“*");
        ctx.ChangeTracker.Clear();
        (await ctx.Verbaende.AnyAsync(v => v.Id == 1)).Should().BeTrue();
    }

    [Fact]
    public async Task VerbandOhneBezuege_WirdGeloescht()
    {
        await using var ctx = NeuerContext();
        Anlegen(ctx);
        var service = VerbandServiceFuer(ctx);

        (await service.LoeschSperreAsync(2)).Should().BeNull();
        await service.LoeschenAsync(2);

        (await ctx.Verbaende.AnyAsync(v => v.Id == 2)).Should().BeFalse();
    }

    [Fact]
    public async Task LandMitMannschaften_ZaehltKontingenteEinesTurniersEinmal()
    {
        await using var ctx = NeuerContext();
        Anlegen(ctx);

        var sperre = await LandServiceFuer(ctx).LoeschSperreAsync(1);

        sperre.Should().Be(
            "Das Land „Deutschland“ kann nicht gelöscht werden. Noch zugeordnet: 2 Mannschaften, Landesquote in 1 Turnier.");
    }

    [Fact]
    public async Task LandMitMannschaften_LoeschenWirftSperrtext()
    {
        await using var ctx = NeuerContext();
        Anlegen(ctx);

        var loeschen = () => LandServiceFuer(ctx).LoeschenAsync(1);

        await loeschen.Should().ThrowAsync<InvalidOperationException>().WithMessage("Das Land „Deutschland“*");
    }

    [Fact]
    public async Task LandOhneBezuege_WirdGeloescht()
    {
        await using var ctx = NeuerContext();
        Anlegen(ctx);

        await LandServiceFuer(ctx).LoeschenAsync(2);

        (await ctx.Laender.AnyAsync(l => l.Id == 2)).Should().BeFalse();
    }

    // Die Seite hält einen DbContext für alle Formulare: Ein abgelehntes Löschen darf ihn nicht im Zustand
    // „gelöscht“ zurücklassen, sonst scheitert jedes weitere Speichern der Seite (#559, Beleg gegen SQL Server).
    [Fact]
    public async Task AbgelehntesLoeschen_LaesstKontextUnveraendertUndFolgespeichernGelingt()
    {
        await using var ctx = NeuerContext();
        Anlegen(ctx);
        var laender = await new LandRepository(ctx).AlleFindenMitVerbandAsync();
        var repository = new VerbandRepository(ctx);
        var verband = (await repository.FindenNachIdAsync(1))!;

        var loeschen = () => repository.LoeschenAsync(verband);

        await loeschen.Should().ThrowAsync<InvalidOperationException>();
        ctx.ChangeTracker.Entries().Should().OnlyContain(e => e.State == EntityState.Unchanged);
        var deutschland = laender.Single(l => l.Id == 1);
        deutschland.Name = "Deutschland (umbenannt)";
        await new LandRepository(ctx).AktualisierenAsync(deutschland);
        ctx.ChangeTracker.Clear();
        (await ctx.Laender.SingleAsync(l => l.Id == 1)).Name.Should().Be("Deutschland (umbenannt)");
        (await ctx.Verbaende.AnyAsync(v => v.Id == 1)).Should().BeTrue();
    }
}
