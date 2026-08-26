using FluentAssertions;
using Fussballturnierverwaltung.Application.Ranglisten;
using Fussballturnierverwaltung.Application.Validierung;
using Fussballturnierverwaltung.Infrastructure.Persistenz;
using Fussballturnierverwaltung.Infrastructure.Ranglisten;
using Fussballturnierverwaltung.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fussballturnierverwaltung.Tests.Ranglisten;

public class RanglistenQuelleServiceTests
{
    private const string HtmlMitZweiTabellen = """
        <table id="klein"><tr><th>H</th></tr><tr><td>Eins</td></tr></table>
        <table id="gross">
        <tr><th>Rang</th><th>Team</th><th>Punkte</th></tr>
        <tr><td>1</td><td>Argentina</td><td>1832</td></tr>
        <tr><td>2</td><td>France</td><td>1795</td></tr>
        </table>
        """;

    private static RanglistenQuelleService Service(string html) =>
        new(
            new RanglistenQuelleRepository(new TurnierverwaltungDbContext(
                new DbContextOptionsBuilder<TurnierverwaltungDbContext>()
                    .UseInMemoryDatabase(Guid.NewGuid().ToString())
                    .Options)),
            new RanglistenQuelleValidator(),
            new FakeHtmlHerunterlader(html),
            new RanglistenHtmlAuswerter(),
            NullLogger<RanglistenQuelleService>.Instance);

    [Fact]
    public async Task WebseitePruefenAsync_LiefertAlleTabellenDerSeite()
    {
        var service = Service(HtmlMitZweiTabellen);

        var tabellen = await service.WebseitePruefenAsync("https://example.com/ranking");

        tabellen.Should().HaveCount(2);
        tabellen[1].SpaltenAnzahl.Should().Be(3);
        tabellen[1].ErkannteKopfzeile.Should().BeTrue();
    }

    [Fact]
    public async Task WebseitePruefenAsync_OhneTabellen_LiefertLeereListe()
    {
        var service = Service("<html><body>Keine Tabelle hier.</body></html>");

        var tabellen = await service.WebseitePruefenAsync("https://example.com/ranking");

        tabellen.Should().BeEmpty();
    }
}
