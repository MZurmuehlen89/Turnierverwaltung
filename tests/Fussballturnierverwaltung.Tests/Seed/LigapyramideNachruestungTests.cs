using FluentAssertions;
using Fussballturnierverwaltung.Application.Auslosungen;
using Fussballturnierverwaltung.Infrastructure.Persistenz;
using Fussballturnierverwaltung.Infrastructure.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fussballturnierverwaltung.Tests.Seed;

/// <summary>Seed der Ligapyramide Deutschland 2025/26 (Issue #393).</summary>
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
