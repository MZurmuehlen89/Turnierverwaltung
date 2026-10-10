using FluentAssertions;
using Fussballturnierverwaltung.Infrastructure.Persistenz;
using Fussballturnierverwaltung.Infrastructure.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fussballturnierverwaltung.Tests.Seed;

public class SeedProtokollTests
{
    private const string FILTER_ERGAENZT = "Turniervorlagen um Verband-/Land-/Mannschaftsartfilter ergänzt.";

    [Fact]
    public async Task InitialisierenAsync_LeereDatenbank_ProtokolliertJedenSchrittDerEtwasAngelegtHat()
    {
        var optionen = NeueDatenbank();
        await using var ctx = new TurnierverwaltungDbContext(optionen);

        var protokoll = await new SeedDatenService(ctx, NullLogger<SeedDatenService>.Instance).InitialisierenAsync();

        protokoll.Select(e => e.Text).Should().Equal(
            "Verbände angelegt.",
            "Länder nachgerüstet.",
            "Verbands-/Länder-Bilder nachgerüstet.",
            "Mannschaften nachgerüstet.",
            "Mannschafts-Wappen nachgerüstet.",
            "RankingRegeln nachgerüstet.",
            "RankingRegelSets nachgerüstet.",
            "Regelwerke nachgerüstet.",
            "Ranglisten-Quellen nachgerüstet.",
            "neue Turniervorlagen nachgerüstet.",
            FILTER_ERGAENZT);
        protokoll.Should().OnlyContain(e => e.Anzahl > 0);
    }

    [Fact]
    public async Task InitialisierenAsync_VollstaendigeDatenbank_LegtNichtsMehrAn()
    {
        var optionen = NeueDatenbank();
        await using (var ctx = new TurnierverwaltungDbContext(optionen))
            await new SeedDatenService(ctx, NullLogger<SeedDatenService>.Instance).InitialisierenAsync();

        await using var zweiterLauf = new TurnierverwaltungDbContext(optionen);
        var protokoll = await new SeedDatenService(zweiterLauf, NullLogger<SeedDatenService>.Instance).InitialisierenAsync();

        // Ist-Stand, kein Soll: die Filter-Ergänzung zählt die drei Vorlagen ohne Filter (Mini-Turniere, Freies
        // Turnier) bei jedem Lauf mit, obwohl sich an ihnen nichts ändert.
        protokoll.Select(e => (e.Anzahl, e.Text)).Should().Equal((3, FILTER_ERGAENZT));
    }

    private static DbContextOptions<TurnierverwaltungDbContext> NeueDatenbank() =>
        new DbContextOptionsBuilder<TurnierverwaltungDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
}
