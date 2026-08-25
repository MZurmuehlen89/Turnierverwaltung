using FluentAssertions;
using Fussballturnierverwaltung.Domain.Entitaeten.Ranglisten;
using Fussballturnierverwaltung.Infrastructure.Ranglisten;

namespace Fussballturnierverwaltung.Tests.Ranglisten;

public class RanglistenHtmlAuswerterTests
{
    private readonly RanglistenHtmlAuswerter _auswerter = new();

    private const string WikipediaArtigeTabelle = """
        <html><body>
        <table class="wikitable">
        <tr><th>Rang</th><th>Team</th><th>Punkte</th></tr>
        <tr><td>1</td><td>Argentina[a]</td><td>1,832.71</td></tr>
        <tr><td>2</td><td>France</td><td>1,795.19</td></tr>
        <tr><td>3</td><td>Zu kurz</td></tr>
        <tr><td>4</td><td>Spain</td><td>1,700</td></tr>
        </table>
        </body></html>
        """;

    private static RanglistenQuelle Quelle(
        int mannschaftSpalte = 1, int? punkteSpalte = 2, string? selektor = null, int tabellenIndex = 0) => new()
    {
        Name = "Test-Quelle",
        Url = "https://example.com/ranking",
        TabellenIndex = tabellenIndex,
        TabellenSelektor = selektor,
        MannschaftSpalte = mannschaftSpalte,
        PunkteSpalte = punkteSpalte,
        KopfzeileUeberspringen = true
    };

    [Fact]
    public void GueltigeTabelle_LiefertAlleVollstaendigenZeilen()
    {
        var ergebnis = _auswerter.Auswerten(WikipediaArtigeTabelle, Quelle());

        ergebnis.Should().HaveCount(3);
        ergebnis[0].RohName.Should().Be("Argentina");
        ergebnis[0].Punkte.Should().Be(1833);
        ergebnis[1].RohName.Should().Be("France");
        ergebnis[2].RohName.Should().Be("Spain");
        ergebnis[2].Punkte.Should().Be(1700);
    }

    [Fact]
    public void FussnotenMarker_WerdenEntfernt()
    {
        var ergebnis = _auswerter.Auswerten(WikipediaArtigeTabelle, Quelle());
        ergebnis[0].RohName.Should().NotContain("[a]");
    }

    [Fact]
    public void ZuKurzeZeile_WirdUebersprungen()
    {
        var ergebnis = _auswerter.Auswerten(WikipediaArtigeTabelle, Quelle());
        ergebnis.Should().NotContain(z => z.RohName == "Zu kurz");
    }

    [Fact]
    public void OhnePunkteSpalte_PunkteSindImmerNull()
    {
        var ergebnis = _auswerter.Auswerten(WikipediaArtigeTabelle, Quelle(punkteSpalte: null));
        ergebnis.Should().OnlyContain(z => z.Punkte == null);
    }

    [Fact]
    public void KeinePassendeTabelle_WirftException()
    {
        var quelle = Quelle(tabellenIndex: 5);
        var act = () => _auswerter.Auswerten("<html><body><table></table></body></html>", quelle);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void TabellenSelektor_HatVorrangVorIndex()
    {
        const string html = """
            <table id="falsch"><tr><th>H</th></tr><tr><td>Falsch</td></tr></table>
            <table id="richtig"><tr><th>H</th><th>T</th></tr><tr><td>1</td><td>Richtig</td></tr></table>
            """;
        var ergebnis = _auswerter.Auswerten(html, Quelle(punkteSpalte: null, selektor: "#richtig"));
        ergebnis.Should().ContainSingle().Which.RohName.Should().Be("Richtig");
    }
}
