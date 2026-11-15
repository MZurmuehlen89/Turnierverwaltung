using FluentAssertions;
using Fussballturnierverwaltung.Application.Stammdaten;
using Fussballturnierverwaltung.Application.Stammdaten.Eingaben;
using Fussballturnierverwaltung.Application.Validierung;
using Fussballturnierverwaltung.Infrastructure.Persistenz;
using Fussballturnierverwaltung.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fussballturnierverwaltung.Tests.Stammdaten;

/// <summary>Der Kurzname eines Verbands wird großgeschrieben gespeichert (#49): PostgreSQL unterscheidet beim
/// eindeutigen Index Groß/klein, „uefa“ wäre sonst neben „UEFA“ möglich.</summary>
public class VerbandKurznameTests
{
    private readonly TurnierverwaltungDbContext _context = new(new DbContextOptionsBuilder<TurnierverwaltungDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .Options);

    private VerbandService Service() =>
        new(new VerbandRepository(_context), new VerbandValidator(), NullLogger<VerbandService>.Instance);

    [Fact]
    public async Task Erstellen_SchreibtKurznameGross()
    {
        var verband = await Service().ErstellenAsync(new VerbandErstellenEingabe("UEFA", " uefa "));

        verband.Kurzname.Should().Be("UEFA");
    }

    [Fact]
    public async Task Aktualisieren_SchreibtKurznameGross()
    {
        var verband = await Service().ErstellenAsync(new VerbandErstellenEingabe("Confederación", "CONMEBOL"));

        await Service().AktualisierenAsync(verband.Id, new VerbandAktualisierenEingabe("Confederación", "Conmebol"));

        (await _context.Verbaende.SingleAsync()).Kurzname.Should().Be("CONMEBOL");
    }
}
