using FluentAssertions;
using Fussballturnierverwaltung.Application.Ranglisten;
using Fussballturnierverwaltung.Application.Validierung;
using Fussballturnierverwaltung.Domain.Entitaeten.Ranglisten;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Infrastructure.Persistenz;
using Fussballturnierverwaltung.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fussballturnierverwaltung.Tests.Ranglisten;

/// <summary>Liga je Verein aus den Abschnitten der Ligapyramiden — Untertitel im Spielkopf (Issue #384).</summary>
public class LigazugehoerigkeitTests
{
    // Bundesliga Plätze 1–2, 2. Bundesliga 3–4; Mannschaft-Id = Platz, sofern nicht anders angegeben.
    private static Rangliste Pyramide(DateOnly datum, params (int MannschaftId, int Rang)[] eintraege) => new()
    {
        Datum = datum,
        Abschnitte =
        [
            new RanglistenAbschnitt { Name = "2. Bundesliga", Spielklasse = 2, BisPlatz = 4 },
            new RanglistenAbschnitt { Name = "Bundesliga", Spielklasse = 1, BisPlatz = 2 },
        ],
        Eintraege = eintraege.Select(e => new RanglistenEintrag { MannschaftId = e.MannschaftId, Rang = e.Rang }).ToList(),
    };

    private static readonly DateOnly _saison2025 = new(2025, 7, 1);
    private static readonly DateOnly _saison2026 = new(2026, 7, 1);

    [Fact]
    public void Aus_OrdnetPlaetzeDenAbschnittenZu()
    {
        var liga = Ligazugehoerigkeit.Aus([Pyramide(_saison2025, (1, 1), (2, 2), (3, 3), (4, 4))]);

        new[] { 1, 2, 3, 4 }.Select(liga.LigaVon)
            .Should().Equal("Bundesliga", "Bundesliga", "2. Bundesliga", "2. Bundesliga");
    }

    [Fact]
    public void Aus_PlatzHinterDemLetztenAbschnitt_OhneLiga() =>
        Ligazugehoerigkeit.Aus([Pyramide(_saison2025, (5, 5))]).LigaVon(5).Should().BeNull();

    [Fact]
    public void Aus_VereinInKeinerPyramide_OhneLiga() =>
        Ligazugehoerigkeit.Aus([Pyramide(_saison2025, (1, 1))]).LigaVon(99).Should().BeNull();

    [Fact]
    public void Aus_DoppelterEintrag_BesterPlatzZaehlt() =>
        Ligazugehoerigkeit.Aus([Pyramide(_saison2025, (7, 4), (7, 1))]).LigaVon(7).Should().Be("Bundesliga");

    [Fact]
    public void Aus_RanglisteOhneAbschnitte_ZaehltNicht()
    {
        var koeffizient = new Rangliste { Datum = _saison2026, Eintraege = [new RanglistenEintrag { MannschaftId = 1, Rang = 1 }] };

        Ligazugehoerigkeit.Aus([koeffizient]).LigaVon(1).Should().BeNull();
    }

    [Fact]
    public void Keine_KenntKeinenVerein() => Ligazugehoerigkeit.Keine.LigaVon(1).Should().BeNull();

    [Fact]
    public async Task LigazugehoerigkeitAbrufen_NurDiePyramideDesEigenenLandes()
    {
        await using var context = new TurnierverwaltungDbContext(
            new DbContextOptionsBuilder<TurnierverwaltungDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        // Pyramide 1 gehört Deutschland, Pyramide 2 (neuer) verweist kein Land. Verein 4 aus England steht in der
        // deutschen Pyramide, sein Land verweist aber nicht auf sie.
        var deutsch = Pyramide(_saison2025, (1, 1), (3, 3), (4, 2));
        deutsch.Id = 1;
        var verwaist = Pyramide(_saison2026, (3, 1));
        verwaist.Id = 2;
        context.Ranglisten.AddRange(deutsch, verwaist);
        context.Verbaende.Add(new Verband { Id = 1, Name = "UEFA", Kurzname = "UEFA" });
        context.Laender.AddRange(
            new Land { Id = 1, Name = "Deutschland", Kurzname = "GER", FifaCode = "GER", VerbandId = 1, RanglisteId = 1 },
            new Land { Id = 2, Name = "England", Kurzname = "ENG", FifaCode = "ENG", VerbandId = 1 });
        context.Mannschaften.AddRange(
            new Mannschaft { Id = 1, Name = "Bayern", LandId = 1 },
            new Mannschaft { Id = 3, Name = "Hertha", LandId = 1 },
            new Mannschaft { Id = 4, Name = "Arsenal", LandId = 2 });
        await context.SaveChangesAsync();
        var service = new RanglisteService(new RanglisteRepository(context), new RanglisteValidator(), NullLogger<RanglisteService>.Instance);

        var liga = await service.LigazugehoerigkeitAbrufenAsync();

        new[] { 1, 3, 4 }.Select(liga.LigaVon).Should().Equal("Bundesliga", "2. Bundesliga", null);
    }
}
