using FluentAssertions;
using Fussballturnierverwaltung.Application.Ranglisten;
using Fussballturnierverwaltung.Application.Ranglisten.Eingaben;
using Fussballturnierverwaltung.Application.Validierung;
using Fussballturnierverwaltung.Infrastructure.Persistenz;
using Fussballturnierverwaltung.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fussballturnierverwaltung.Tests.Ranglisten;

/// <summary>Speichern der Liga-Abschnitte mit der Rangliste (#477): Schlüssel-Abgleich, und der Import (ohne
/// Abschnitte) lässt sie stehen.</summary>
public class RanglisteServiceAbschnitteTests
{
    private readonly TurnierverwaltungDbContext _context = new(
        new DbContextOptionsBuilder<TurnierverwaltungDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private RanglisteService Service() =>
        new(new RanglisteRepository(_context), new RanglisteValidator(), NullLogger<RanglisteService>.Instance);

    private static readonly DateOnly _datum = new(2025, 7, 1);

    private async Task<int> RanglisteMitAbschnittenAsync()
    {
        var rangliste = await Service().ErstellenAsync(new RanglisteErstellenEingabe("Pyramide", _datum, "", [],
            Abschnitte: [new(null, "Bundesliga", 1, 18), new(null, "2. Bundesliga", 2, 36)]));
        return rangliste.Id;
    }

    [Fact]
    public async Task Erstellen_LegtAbschnitteAn()
    {
        var id = await RanglisteMitAbschnittenAsync();

        _context.RanglistenAbschnitte.Where(a => a.RanglisteId == id).Select(a => a.Name)
            .Should().BeEquivalentTo("Bundesliga", "2. Bundesliga");
    }

    [Fact]
    public async Task Aktualisieren_AendertBestehende_EntferntFehlende_LegtNeueAn()
    {
        var id = await RanglisteMitAbschnittenAsync();
        var bundesliga = _context.RanglistenAbschnitte.Single(a => a.Name == "Bundesliga");

        await Service().AktualisierenAsync(id, new RanglisteAktualisierenEingabe("Pyramide", _datum, "", [],
            Abschnitte: [new(bundesliga.Id, "1. Bundesliga", 1, 18), new(null, "3. Liga", 3, 56)]));

        var abschnitte = _context.RanglistenAbschnitte.Where(a => a.RanglisteId == id).OrderBy(a => a.BisPlatz).ToList();
        abschnitte.Select(a => (a.Name, a.BisPlatz)).Should().Equal(("1. Bundesliga", 18), ("3. Liga", 56));
        abschnitte[0].Id.Should().Be(bundesliga.Id);
    }

    [Fact]
    public async Task Aktualisieren_OhneAbschnitte_LaesstSieStehen()
    {
        var id = await RanglisteMitAbschnittenAsync();

        await Service().AktualisierenAsync(id, new RanglisteAktualisierenEingabe("Pyramide", _datum, "Import", []));

        _context.RanglistenAbschnitte.Count(a => a.RanglisteId == id).Should().Be(2);
    }

    [Fact]
    public async Task Aktualisieren_UngueltigeAbschnitte_WirftValidationException()
    {
        var id = await RanglisteMitAbschnittenAsync();

        var aktion = () => Service().AktualisierenAsync(id, new RanglisteAktualisierenEingabe("Pyramide", _datum, "", [],
            Abschnitte: [new(null, "Bundesliga", 1, 18), new(null, "", 2, 10)]));

        await aktion.Should().ThrowAsync<ValidationException>();
    }
}
