using FluentAssertions;
using Fussballturnierverwaltung.Application.Auslosungen;
using Fussballturnierverwaltung.Domain.Entitaeten.Ranglisten;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.Infrastructure.Persistenz;
using Fussballturnierverwaltung.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Fussballturnierverwaltung.Tests.Auslosungen;

/// <summary>Quoten samt Ranglisten laden (#573).</summary>
public class KontingentvorgabeServiceTests
{
    private const int TURNIER = 1;
    private const int ERSTE_RUNDE = 5;

    // Runden bewusst ungeordnet: die erste ist die mit der kleinsten Reihenfolge.
    private static TurnierInstanz Turnier(int id, MannschaftsartFilter art) => new()
    {
        Id = id, MannschaftsartFilter = art,
        Runden = [new TurnierRunde { Id = 6, Reihenfolge = 2 }, new TurnierRunde { Id = ERSTE_RUNDE, Reihenfolge = 1 }],
    };

    private static async Task<(TurnierverwaltungDbContext Context, KontingentvorgabeService Service)> AufbauenAsync()
    {
        var context = new TurnierverwaltungDbContext(
            new DbContextOptionsBuilder<TurnierverwaltungDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        var pyramide = new Rangliste
        {
            Id = 1, Name = "Pyramide DE",
            Abschnitte = [new RanglistenAbschnitt { Name = "Bundesliga", Spielklasse = 1, BisPlatz = 2 }],
            Eintraege = [new RanglistenEintrag { MannschaftId = 11, Rang = 1 }],
        };
        var koeffizient = new Rangliste { Id = 2, Name = "Koeffizient", Eintraege = [new RanglistenEintrag { MannschaftId = 11, Rang = 3 }] };
        var klubKoeffizient = new Rangliste { Id = 3, Name = "Klub-Koeffizient", Eintraege = [new RanglistenEintrag { MannschaftId = 11, Rang = 7 }] };
        context.Ranglisten.AddRange(pyramide, koeffizient, klubKoeffizient);
        context.Verbaende.AddRange(
            new Verband { Id = 1, Name = "UEFA", Kurzname = "UEFA", RanglisteId = 2, VereinsRanglisteId = 3 },
            new Verband { Id = 2, Name = "CAF", Kurzname = "CAF", RanglisteId = 2 });
        context.Laender.AddRange(
            new Land { Id = 10, Name = "Deutschland", VerbandId = 1, RanglisteId = 1 },
            new Land { Id = 11, Name = "Portugal", VerbandId = 1 });
        context.TurnierInstanzen.Add(new TurnierInstanz { Id = TURNIER, Name = "Pokal", MannschaftsartFilter = MannschaftsartFilter.Verein });
        context.LandesQuoten.AddRange(
            new LandesQuote { TurnierInstanzId = TURNIER, LandId = 10, Anzahl = 2, Spielklasse = 1, AbPlatz = 1 },
            new LandesQuote { TurnierInstanzId = TURNIER, LandId = 11, Anzahl = 1 });
        context.VerbandsQuoten.AddRange(
            new VerbandsQuote { TurnierInstanzId = TURNIER, VerbandId = 1, Anzahl = 3 },
            new VerbandsQuote { TurnierInstanzId = TURNIER, VerbandId = 2, Anzahl = 1 });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var service = new KontingentvorgabeService(
            new LandesQuoteRepository(context), new VerbandsQuoteRepository(context), new RanglisteRepository(context));
        return (context, service);
    }

    [Fact]
    public async Task Laden_QuotenMitRanglistenSamtEintraegenUndAbschnitten()
    {
        var (context, service) = await AufbauenAsync();
        await using var _ = context;

        var vorgabe = await service.LadenAsync(Turnier(TURNIER, MannschaftsartFilter.Verein), ERSTE_RUNDE);

        var deutschland = vorgabe.Landesquoten.Single(q => q.LandId == 10);
        (deutschland.LandName, deutschland.Anzahl, deutschland.Spielklasse, deutschland.AbPlatz).Should().Be(("Deutschland", 2, 1, 1));
        deutschland.Rangliste!.Abschnitte.Should().ContainSingle();
        deutschland.Rangliste.Eintraege.Should().ContainSingle();
        vorgabe.Landesquoten.Single(q => q.LandId == 11).Rangliste.Should().BeNull();

        // Vereinswettbewerb: die Vereinsrangliste des Verbands, ohne Rückgriff auf seine Rangliste (#574).
        vorgabe.Verbandsquoten.Select(q => $"{q.Name} {q.Anzahl} {q.Rangliste?.Id}").Should().BeEquivalentTo("UEFA 3 3", "CAF 1 ");
        vorgabe.Verbandsquoten.Single(q => q.Name == "UEFA").Rangliste!.Eintraege.Should().ContainSingle();
    }

    [Fact]
    public async Task Laden_Nationalmannschaften_RanglisteDesVerbands()
    {
        var (context, service) = await AufbauenAsync();
        await using var _ = context;

        var vorgabe = await service.LadenAsync(Turnier(TURNIER, MannschaftsartFilter.Nationalmannschaft), ERSTE_RUNDE);

        // Beide Verbände verweisen auf dieselbe Rangliste.
        vorgabe.Verbandsquoten.Select(q => $"{q.Name} {q.Anzahl} {q.Rangliste?.Id}").Should().BeEquivalentTo("UEFA 3 2", "CAF 1 2");
    }

    [Fact]
    public async Task Laden_OhneVereinswettbewerb_KeineLandesquoten()
    {
        var (context, service) = await AufbauenAsync();
        await using var _ = context;

        var vorgabe = await service.LadenAsync(Turnier(TURNIER, MannschaftsartFilter.Nationalmannschaft), ERSTE_RUNDE);

        vorgabe.Landesquoten.Should().BeEmpty();
        vorgabe.Verbandsquoten.Should().HaveCount(2);
    }

    [Fact]
    public async Task Laden_TurnierOhneQuoten_Leer()
    {
        var (context, service) = await AufbauenAsync();
        await using var _ = context;

        var vorgabe = await service.LadenAsync(Turnier(99, MannschaftsartFilter.Verein), ERSTE_RUNDE);

        vorgabe.Leer.Should().BeTrue();
    }

    [Fact]
    public async Task Laden_Folgerunde_Leer()
    {
        var (context, service) = await AufbauenAsync();
        await using var _ = context;

        var vorgabe = await service.LadenAsync(Turnier(TURNIER, MannschaftsartFilter.Verein), turnierRundeId: 6);

        vorgabe.Leer.Should().BeTrue();
    }
}
