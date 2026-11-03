using FluentAssertions;
using Fussballturnierverwaltung.Application.Auslosungen;
using Fussballturnierverwaltung.Application.Ranglisten;
using Fussballturnierverwaltung.Domain.Entitaeten.Ranglisten;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.Infrastructure.Persistenz;
using Fussballturnierverwaltung.Infrastructure.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fussballturnierverwaltung.Tests.Seed;

/// <summary>Seed der Ligapyramiden: Deutschland 2025/26 (Issue #393), England, Spanien, Italien, Frankreich (#575),
/// erste Ligen weiterer Länder (#580), oberste Ligen außerhalb Europas (#582).</summary>
public class LigapyramideNachruestungTests
{
    private const string RANGLISTE = "Ligapyramide Deutschland 2025/26";

    [Fact]
    public async Task LeereDatenbank_PyramideMitAllenDeutschenVereinenAbschnittenUndProfigrenze()
    {
        var optionen = NeueDatenbank();
        await SeedLaufAsync(optionen);

        await using var ctx = new TurnierverwaltungDbContext(optionen);
        var rangliste = await ctx.Ranglisten.Include(r => r.Eintraege).Include(r => r.Abschnitte)
            .SingleAsync(r => r.Name == RANGLISTE);
        rangliste.RanglistenQuelleId.Should().BeNull("von Hand gepflegt, keine Import-Quelle");
        rangliste.ProfigrenzeSpielklasse.Should().Be(2);
        rangliste.Eintraege.Should().HaveCount(64, "jeder Verein der Seed-Daten wird gefunden");
        rangliste.Eintraege.Select(e => e.Rang).Should().BeEquivalentTo(Enumerable.Range(1, 64));
        rangliste.Abschnitte.OrderBy(a => a.BisPlatz).Select(a => (a.Name, a.Spielklasse, a.BisPlatz)).Should().Equal(
            ("Bundesliga", 1, 18), ("2. Bundesliga", 2, 36), ("3. Liga", 3, 54),
            ("Regionalliga Nordost", 4, 58), ("Regionalliga Südwest", 4, 60),
            ("Regionalliga Bayern", 4, 62), ("Regionalliga West", 4, 64));

        var klassen = Spielklassen.Aus(rangliste);
        var id = await ctx.Mannschaften.ToDictionaryAsync(m => m.Name, m => m.Id);
        klassen.HatHeimrechtGegen(id["Wuppertaler SV"], id["Bayern München"]).Should().BeTrue();
        klassen.HatHeimrechtGegen(id["Dynamo Dresden"], id["Bayern München"]).Should().BeFalse("beide Profiklubs");
        klassen.HatHeimrechtGegen(id["Wuppertaler SV"], id["MSV Duisburg"]).Should().BeFalse("beide Amateurklubs");
    }

    // #580: erste Liga mit echten Tabellenplätzen; Vereine, die der Seed nicht kennt, bleiben Lücken.
    [Fact]
    public async Task LeereDatenbank_ErsteLigaDerNiederlandeMitLueckenAmLand()
    {
        var optionen = NeueDatenbank();
        await SeedLaufAsync(optionen);

        await using var ctx = new TurnierverwaltungDbContext(optionen);
        var rangliste = await ctx.Ranglisten.Include(r => r.Eintraege).ThenInclude(e => e.Mannschaft)
            .Include(r => r.Abschnitte)
            .SingleAsync(r => r.Name == "Ligapyramide Niederlande 2024/25");
        rangliste.ProfigrenzeSpielklasse.Should().BeNull();
        rangliste.Abschnitte.Select(a => (a.Name, a.Spielklasse, a.BisPlatz)).Should().Equal(("Eredivisie", 1, 18));
        rangliste.Eintraege.OrderBy(e => e.Rang).Select(e => (e.Rang, e.Mannschaft.Name)).Should().Equal(
            (1, "PSV Eindhoven"), (2, "Ajax Amsterdam"), (3, "Feyenoord Rotterdam"), (4, "FC Utrecht"),
            (5, "AZ Alkmaar"), (6, "FC Twente Enschede"), (7, "Go Ahead Eagles"));
        Kontingente.Bereich(rangliste, spielklasse: 1, abPlatz: 1, anzahl: 2).Should().NotBeNull();

        var niederlande = await ctx.Laender.SingleAsync(l => l.FifaCode == "NED");
        niederlande.RanglisteId.Should().Be(rangliste.Id);
    }

    // #575: vier Profiligen; was der Seed nicht kennt, bleibt Lücke. Die Pyramide ist Standard-Rangliste des FA Cups —
    // die 20 freien Plätze neben Premier League und Championship gehen an League One 1–20.
    [Fact]
    public async Task LeereDatenbank_EnglandMitVierProfiligenAmLandUndImFaCup()
    {
        var optionen = NeueDatenbank();
        await SeedLaufAsync(optionen);

        await using var ctx = new TurnierverwaltungDbContext(optionen);
        var rangliste = await ctx.Ranglisten.Include(r => r.Eintraege).ThenInclude(e => e.Mannschaft)
            .Include(r => r.Abschnitte)
            .SingleAsync(r => r.Name == "Ligapyramide England 2024/25");
        rangliste.ProfigrenzeSpielklasse.Should().Be(4);
        rangliste.Abschnitte.OrderBy(a => a.BisPlatz).Select(a => (a.Name, a.Spielklasse, a.BisPlatz)).Should().Equal(
            ("Premier League", 1, 20), ("Championship", 2, 44), ("League One", 3, 68), ("League Two", 4, 92));
        rangliste.Eintraege.Should().HaveCount(20 + 24 + 20 + 4);
        var verein = rangliste.Eintraege.ToDictionary(e => e.Rang, e => e.Mannschaft.Name);
        (verein[1], verein[21], verein[45], verein[64], verein[73]).Should().Be(
            ("FC Liverpool", "Leeds United", "Birmingham City", "Burton Albion", "AFC Wimbledon"));
        verein.Keys.Should().NotContain([65, 66, 67, 68, 72]);
        Kontingente.Bereich(rangliste, spielklasse: 2, abPlatz: 1, anzahl: 24).Should().NotBeNull();

        (await ctx.Laender.SingleAsync(l => l.FifaCode == "ENG")).RanglisteId.Should().Be(rangliste.Id);
        var faCup = await ctx.TurnierInstanzen.Include(i => i.Runden)
            .SingleAsync(i => i.Status == TurnierStatus.Vorlage && i.Name == "FA Cup");
        faCup.Runden.Where(r => r.Rundenart == Rundenart.KoRunde).Should().OnlyContain(r => r.StandardRanglisteId == rangliste.Id);
    }

    // #582: Gastländer — die MLS führt die kanadischen Vereine, die Pyramide gehört trotzdem nur den USA.
    [Fact]
    public async Task LeereDatenbank_MlsMitKanadischenVereinenAmLandUsa()
    {
        var optionen = NeueDatenbank();
        await SeedLaufAsync(optionen);

        await using var ctx = new TurnierverwaltungDbContext(optionen);
        var rangliste = await ctx.Ranglisten.Include(r => r.Eintraege).ThenInclude(e => e.Mannschaft).ThenInclude(m => m.Land)
            .SingleAsync(r => r.Name == "Ligapyramide USA 2024");
        rangliste.Eintraege.Should().HaveCount(29, "jeder Verein der MLS steht im Seed");
        var verein = rangliste.Eintraege.ToDictionary(e => e.Rang, e => (e.Mannschaft.Name, e.Mannschaft.Land.FifaCode));
        (verein[1], verein[14], verein[17], verein[22]).Should().Be(
            (("Inter Miami CF", "USA"), ("Vancouver Whitecaps", "CAN"), ("CF Montréal", "CAN"), ("Toronto FC", "CAN")));

        (await ctx.Laender.SingleAsync(l => l.FifaCode == "USA")).RanglisteId.Should().Be(rangliste.Id);
        (await ctx.Laender.SingleAsync(l => l.FifaCode == "CAN")).RanglisteId.Should().BeNull();
    }

    [Fact]
    public async Task FehlendePyramide_LandMitEigenerRangliste_BehaeltSie()
    {
        var optionen = NeueDatenbank();
        await SeedLaufAsync(optionen);
        int eigene;
        await using (var ctx = new TurnierverwaltungDbContext(optionen))
        {
            var rangliste = new Rangliste { Name = "Eigene Rangliste Niederlande" };
            ctx.Ranglisten.Add(rangliste);
            (await ctx.Laender.SingleAsync(l => l.FifaCode == "NED")).Rangliste = rangliste;
            ctx.Ranglisten.Remove(await ctx.Ranglisten.SingleAsync(r => r.Name == "Ligapyramide Niederlande 2024/25"));
            await ctx.SaveChangesAsync();
            eigene = rangliste.Id;
        }

        var protokoll = await SeedLaufAsync(optionen);

        protokoll.Select(p => (p.Anzahl, p.Text)).Should().Equal((1, "Ligapyramiden nachgerüstet."));
        await using var pruef = new TurnierverwaltungDbContext(optionen);
        (await pruef.Laender.SingleAsync(l => l.FifaCode == "NED")).RanglisteId.Should().Be(eigene);
    }

    [Fact]
    public async Task ZweiterLauf_ErgaenztNichts()
    {
        var optionen = NeueDatenbank();
        await SeedLaufAsync(optionen);

        var protokoll = await SeedLaufAsync(optionen);

        protokoll.Should().BeEmpty();
    }

    private static async Task<IReadOnlyList<SeedProtokollEintrag>> SeedLaufAsync(
        DbContextOptions<TurnierverwaltungDbContext> optionen)
    {
        await using var ctx = new TurnierverwaltungDbContext(optionen);
        return await new SeedDatenService(ctx, NullLogger<SeedDatenService>.Instance).InitialisierenAsync();
    }

    private static DbContextOptions<TurnierverwaltungDbContext> NeueDatenbank() =>
        new DbContextOptionsBuilder<TurnierverwaltungDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
}
