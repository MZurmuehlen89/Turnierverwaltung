using FluentAssertions;
using Fussballturnierverwaltung.Application.Spielplan;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.Infrastructure.Persistenz;
using Fussballturnierverwaltung.Infrastructure.Seed;
using Fussballturnierverwaltung.Infrastructure.Seed.Daten;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fussballturnierverwaltung.Tests.Seed;

/// <summary>Issue #465: die frühere Sammelvorlage „FIFA WM · 16 Teams · 1934–1978“ ist in die Formate 1934–1938,
/// 1958–1970 und 1974–1978 aufgeteilt.</summary>
public class FifaWm16TeamsSeedTests
{
    private const string WM = "FIFA WM";
    private const string WM_1958_BIS_1970 = "16 Teams · 1958–1970";
    private const string WM_1974_BIS_1978 = "16 Teams · 1974–1978";
    private const string WM_ALT = "16 Teams · 1934–1978";

    private static readonly List<TurnierInstanz> _vorlagen = TurnierVorlagenSeedDaten.Erstellen();

    private static TurnierInstanz Vorlage(string variante) => _vorlagen.Single(v => v.Name == WM && v.Variante == variante);

    private static List<(string? Heim, string? Auswaerts)> Paarungen(TurnierInstanz vorlage, string runde) =>
        [.. vorlage.Auslosungen.Single(a => a.TurnierRunde.Name == runde).Ergebnisse
            .OrderBy(e => e.PaarungsNummer)
            .Select(e => (e.HeimPlatzhalterName, e.AuswaertsPlatzhalterName))];

    // ── Seed-Stand ─────────────────────────────────────────────────────────────

    [Fact]
    public void Wm1934Bis1938_ReinesKo()
    {
        Vorlage(TurnierVorlagenSeedDaten.WM_16_TEAMS_1934_BIS_1938).Runden.OrderBy(r => r.Reihenfolge).Select(r => r.Name)
            .Should().Equal("Achtelfinale", "Viertelfinale", "Halbfinale", "Spiel um Platz 3", "Finale");
    }

    // Belegt für 1966: England (1A) – Argentinien (2B), Portugal (1C) – Nordkorea (2D), Halbfinale England – Portugal.
    [Fact]
    public void Wm1958Bis1970_ViertelfinaleUeberKreuzUndHalbfinaleWieImRealenBaum()
    {
        var vorlage = Vorlage(WM_1958_BIS_1970);

        vorlage.Runden.OrderBy(r => r.Reihenfolge).Select(r => r.Name)
            .Should().Equal("Gruppenphase", "Viertelfinale", "Halbfinale", "Spiel um Platz 3", "Finale");
        Paarungen(vorlage, "Viertelfinale").Should().Equal(
            ("1. Gruppe A", "2. Gruppe B"), ("1. Gruppe C", "2. Gruppe D"),
            ("1. Gruppe B", "2. Gruppe A"), ("1. Gruppe D", "2. Gruppe C"));
        Paarungen(vorlage, "Halbfinale").Should().Equal(
            ("Sieger Viertelfinale 1", "Sieger Viertelfinale 2"), ("Sieger Viertelfinale 3", "Sieger Viertelfinale 4"));
    }

    // Belegt für 1974 und 1978: Zwischenrunde A = 1A/2B/1C/2D, B = 1B/2A/1D/2C; Sieger ins Finale, Zweite um Platz 3.
    [Fact]
    public void Wm1974Bis1978_ZwischenrundeFestBesetztUndKeinHalbfinale()
    {
        var vorlage = Vorlage(WM_1974_BIS_1978);
        var zwischenrunde = vorlage.Runden.Single(r => r.Name == "Zwischenrunde");

        vorlage.Runden.OrderBy(r => r.Reihenfolge).Select(r => r.Name)
            .Should().Equal("Gruppenphase", "Zwischenrunde", "Spiel um Platz 3", "Finale");
        zwischenrunde.PaarungFestVorgegeben.Should().BeTrue();
        GruppenSpielplan.AusAuslosung(vorlage.Auslosungen.Single(a => a.TurnierRunde == zwischenrunde).Ergebnisse)
            .Select(g => g.Select(s => s.PlatzhalterName).ToList())
            .Should().BeEquivalentTo(
                new List<List<string>>
                {
                    new() { "1. Gruppe A", "2. Gruppe B", "1. Gruppe C", "2. Gruppe D" },
                    new() { "1. Gruppe B", "2. Gruppe A", "1. Gruppe D", "2. Gruppe C" },
                }, o => o.WithStrictOrdering());
        Paarungen(vorlage, "Finale").Should().Equal(("1. Gruppe A", "1. Gruppe B"));
        Paarungen(vorlage, "Spiel um Platz 3").Should().Equal(("2. Gruppe A", "2. Gruppe B"));
        zwischenrunde.Uebergangsregeln
            .Select(u => (u.ZielRunde.Name, u.GruppeIndex, u.PlatzierungVon, u.PlatzierungBis))
            .Should().BeEquivalentTo(
            [
                ("Finale", 1, 1, 1), ("Finale", 2, 1, 1),
                ("Spiel um Platz 3", 1, 2, 2), ("Spiel um Platz 3", 2, 2, 2),
            ]);
    }

    [Fact]
    public async Task InitialisierenAsync_RegelwerkeJeRundeLautReglement()
    {
        var optionen = NeueDatenbank();
        await using var ctx = new TurnierverwaltungDbContext(optionen);
        await new SeedDatenService(ctx, NullLogger<SeedDatenService>.Instance).InitialisierenAsync();

        VerfahrenArt[] mitWiederholung =
            [VerfahrenArt.VerlaengerungKlassisch, VerfahrenArt.Wiederholungsspiel, VerfahrenArt.Losentscheid];
        foreach (string runde in new[] { "Achtelfinale", "Halbfinale", "Finale" })
            (await VerfahrenAsync(ctx, TurnierVorlagenSeedDaten.WM_16_TEAMS_1934_BIS_1938, runde)).Should().Equal(mitWiederholung);

        foreach (string runde in new[] { "Viertelfinale", "Halbfinale", "Spiel um Platz 3" })
            (await VerfahrenAsync(ctx, WM_1958_BIS_1970, runde))
                .Should().Equal([VerfahrenArt.VerlaengerungKlassisch, VerfahrenArt.Losentscheid], runde);
        (await VerfahrenAsync(ctx, WM_1958_BIS_1970, "Finale")).Should().Equal(mitWiederholung);
        (await PunkteFuerSiegAsync(ctx, WM_1958_BIS_1970, "Gruppenphase")).Should().Be(2);

        (await VerfahrenAsync(ctx, WM_1974_BIS_1978, "Finale"))
            .Should().Equal(VerfahrenArt.VerlaengerungKlassisch, VerfahrenArt.Elfmeterschiessen);
        (await PunkteFuerSiegAsync(ctx, WM_1974_BIS_1978, "Zwischenrunde")).Should().Be(2);
    }

    // ── Nachrüstung bestehender Datenbanken ─────────────────────────────────────

    [Fact]
    public async Task InitialisierenAsync_TeiltAlteSammelvorlageInBestehenderDatenbankAuf()
    {
        var optionen = NeueDatenbank();
        await using (var ctx = new TurnierverwaltungDbContext(optionen))
        {
            await new SeedDatenService(ctx, NullLogger<SeedDatenService>.Instance).InitialisierenAsync();
            await AlteSammelvorlageHerstellenAsync(ctx);
        }

        await using (var ctx = new TurnierverwaltungDbContext(optionen))
        {
            await new SeedDatenService(ctx, NullLogger<SeedDatenService>.Instance).InitialisierenAsync();

            var wm16 = await ctx.TurnierInstanzen.Include(i => i.Auslosungen)
                .Where(i => i.Name == WM && i.Status == TurnierStatus.Vorlage && i.Variante.StartsWith("16 Teams"))
                .ToListAsync();
            wm16.Select(v => v.Variante).Should().BeEquivalentTo(
                TurnierVorlagenSeedDaten.WM_16_TEAMS_1934_BIS_1938, WM_1958_BIS_1970, WM_1974_BIS_1978);
            wm16.Single(v => v.Variante == WM_1958_BIS_1970).Auslosungen.Should().HaveCount(4, "Viertelfinale bis Finale");
            wm16.Single(v => v.Variante == WM_1974_BIS_1978).Auslosungen.Should().HaveCount(3, "Zwischenrunde, Platz 3, Finale");

            var bestand = wm16.Single(v => v.Variante == TurnierVorlagenSeedDaten.WM_16_TEAMS_1934_BIS_1938);
            bestand.Enddatum.Should().Be(new DateOnly(1938, 12, 31));
            bestand.Beschreibung.Should().Contain("Verlängerung, dann Wiederholungsspiel");
            (await VerfahrenAsync(ctx, TurnierVorlagenSeedDaten.WM_16_TEAMS_1934_BIS_1938, "Finale")).Should().Equal(
                VerfahrenArt.VerlaengerungKlassisch, VerfahrenArt.Wiederholungsspiel, VerfahrenArt.Losentscheid);
        }
    }

    [Fact]
    public async Task InitialisierenAsync_LaesstAngepassteSammelvorlageBisAufDieVarianteUnangetastet()
    {
        var optionen = NeueDatenbank();
        var eigenesEnddatum = new DateOnly(1950, 12, 31);
        await using (var ctx = new TurnierverwaltungDbContext(optionen))
        {
            await new SeedDatenService(ctx, NullLogger<SeedDatenService>.Instance).InitialisierenAsync();
            var alt = await AlteSammelvorlageHerstellenAsync(ctx);
            alt.Enddatum = eigenesEnddatum;
            alt.Beschreibung = "eigene Beschreibung";
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = new TurnierverwaltungDbContext(optionen))
        {
            await new SeedDatenService(ctx, NullLogger<SeedDatenService>.Instance).InitialisierenAsync();

            var bestand = await ctx.TurnierInstanzen.SingleAsync(i =>
                i.Name == WM && i.Variante == TurnierVorlagenSeedDaten.WM_16_TEAMS_1934_BIS_1938);
            bestand.Enddatum.Should().Be(eigenesEnddatum);
            bestand.Beschreibung.Should().Be("eigene Beschreibung");
        }
    }

    // Stand vor #465: eine Sammelvorlage "16 Teams · 1934–1978" (reines K.-o., Wiederholungsspiel + Los); die Vorlagen
    // 1958–1970 und 1974–1978 gibt es noch nicht.
    private static async Task<TurnierInstanz> AlteSammelvorlageHerstellenAsync(TurnierverwaltungDbContext ctx)
    {
        foreach (string variante in new[] { WM_1958_BIS_1970, WM_1974_BIS_1978 })
        {
            var neu = await ctx.TurnierInstanzen.Include(i => i.Runden).ThenInclude(r => r.Uebergangsregeln)
                .Include(i => i.Auslosungen)
                .SingleAsync(i => i.Name == WM && i.Variante == variante);
            ctx.Uebergangsregeln.RemoveRange(neu.Runden.SelectMany(r => r.Uebergangsregeln));
            ctx.Auslosungen.RemoveRange(neu.Auslosungen);
            ctx.TurnierInstanzen.Remove(neu);
        }

        int altesRegelwerkId = (await ctx.Regelwerke
            .SingleAsync(r => r.Name == TurnierVorlagenSeedDaten.HISTORISCH_WIEDERHOLUNGSSPIEL_LOS)).Id;
        var wm = await ctx.TurnierInstanzen.Include(i => i.Runden)
            .SingleAsync(i => i.Name == WM && i.Variante == TurnierVorlagenSeedDaten.WM_16_TEAMS_1934_BIS_1938);
        wm.Variante = WM_ALT;
        wm.Enddatum = new DateOnly(1978, 12, 31);
        wm.Beschreibung = TurnierVorlagenSeedDaten.AlteBeschreibungen[(WM, TurnierVorlagenSeedDaten.WM_16_TEAMS_1934_BIS_1938)];
        foreach (var runde in wm.Runden) runde.RegelwerkId = altesRegelwerkId;
        await ctx.SaveChangesAsync();
        return wm;
    }

    // ── Hilfen ─────────────────────────────────────────────────────────────────

    private static DbContextOptions<TurnierverwaltungDbContext> NeueDatenbank() =>
        new DbContextOptionsBuilder<TurnierverwaltungDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

    private static Task<TurnierRunde> RundeAsync(TurnierverwaltungDbContext ctx, string variante, string rundenName) =>
        ctx.TurnierRunden.SingleAsync(r =>
            r.Name == rundenName && r.TurnierInstanz.Name == WM && r.TurnierInstanz.Variante == variante
            && r.TurnierInstanz.Status == TurnierStatus.Vorlage);

    private static async Task<List<VerfahrenArt>> VerfahrenAsync(
        TurnierverwaltungDbContext ctx, string variante, string rundenName)
    {
        var runde = await RundeAsync(ctx, variante, rundenName);
        return await ctx.Regelwerke
            .Where(r => r.Id == runde.RegelwerkId)
            .SelectMany(r => r.Entscheidungsverfahren)
            .OrderBy(e => e.Reihenfolge)
            .Select(e => e.VerfahrenArt)
            .ToListAsync();
    }

    private static async Task<int> PunkteFuerSiegAsync(TurnierverwaltungDbContext ctx, string variante, string rundenName)
    {
        var runde = await RundeAsync(ctx, variante, rundenName);
        return await ctx.Regelwerke
            .Where(r => r.Id == runde.RegelwerkId)
            .SelectMany(r => r.PunkteRegeln)
            .Select(p => p.PunkteFuerSieg)
            .SingleAsync();
    }
}
