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

    [Fact]
    public void DeutschesKommaAlsDezimaltrenner_WirdKorrektGeparst()
    {
        const string html = """
            <table><tr><th>H</th><th>Team</th><th>Punkte</th></tr>
            <tr><td>1</td><td>Spanien</td><td>1995,88</td></tr>
            </table>
            """;
        var ergebnis = _auswerter.Auswerten(html, Quelle());
        ergebnis[0].Punkte.Should().Be(1996);
    }

    [Fact]
    public void DeutscherPunktAlsTausendertrenner_WirdKorrektGeparst()
    {
        const string html = """
            <table><tr><th>H</th><th>Team</th><th>Punkte</th></tr>
            <tr><td>1</td><td>Spanien</td><td>1.995,88</td></tr>
            </table>
            """;
        var ergebnis = _auswerter.Auswerten(html, Quelle());
        ergebnis[0].Punkte.Should().Be(1996);
    }

    [Fact]
    public void VerdoppelterLandName_WirdAufEinfachReduziert()
    {
        const string html = """
            <table><tr><th>H</th><th>Land</th><th>Punkte</th></tr>
            <tr><td>1</td><td>Spanien Spanien**</td><td>1995,88</td></tr>
            </table>
            """;
        var ergebnis = _auswerter.Auswerten(html, Quelle());
        ergebnis[0].RohName.Should().Be("Spanien");
    }

    [Fact]
    public void MeisterAufsteigerMarker_WirdEntfernt()
    {
        const string html = """
            <table><tr><th>H</th><th>Verein</th><th>Punkte</th></tr>
            <tr><td>1</td><td>FC Bayern München (M)</td><td>89</td></tr>
            <tr><td>2</td><td>SV Elversberg (R↑)</td><td>62</td></tr>
            <tr><td>3</td><td>FC Barcelona (M, P)</td><td>94</td></tr>
            </table>
            """;
        var ergebnis = _auswerter.Auswerten(html, Quelle());
        ergebnis[0].RohName.Should().Be("FC Bayern München");
        ergebnis[1].RohName.Should().Be("SV Elversberg");
        ergebnis[2].RohName.Should().Be("FC Barcelona");
    }

    [Fact]
    public void UnterschiedlicheNamensteile_WerdenNichtFaelschlichReduziert()
    {
        const string html = """
            <table><tr><th>H</th><th>Land</th><th>Punkte</th></tr>
            <tr><td>1</td><td>Vereinigte Staaten USA</td><td>2000</td></tr>
            </table>
            """;
        var ergebnis = _auswerter.Auswerten(html, Quelle());
        ergebnis[0].RohName.Should().Be("Vereinigte Staaten USA");
    }

    [Fact]
    public void TabellenErmitteln_FindetAlleTabellenMitVorschau()
    {
        var ergebnis = _auswerter.TabellenErmitteln(WikipediaArtigeTabelle);

        ergebnis.Should().ContainSingle();
        ergebnis[0].Index.Should().Be(0);
        ergebnis[0].ZeilenAnzahl.Should().Be(5);
        ergebnis[0].SpaltenAnzahl.Should().Be(3);
    }

    [Fact]
    public void TabellenErmitteln_MehrereTabellen_WerdenAlleGefunden()
    {
        const string html = """
            <table id="erste"><tr><th>H</th></tr><tr><td>Eins</td></tr></table>
            <table id="zweite"><tr><th>H</th><th>T</th></tr><tr><td>1</td><td>Zwei</td></tr></table>
            """;
        var ergebnis = _auswerter.TabellenErmitteln(html);

        ergebnis.Should().HaveCount(2);
        ergebnis[0].Index.Should().Be(0);
        ergebnis[1].Index.Should().Be(1);
        ergebnis[1].SpaltenAnzahl.Should().Be(2);
    }

    [Fact]
    public void TabellenErmitteln_ErkennKopfzeileAnhandVonTh()
    {
        var ergebnis = _auswerter.TabellenErmitteln(WikipediaArtigeTabelle);
        ergebnis[0].ErkannteKopfzeile.Should().BeTrue();
        ergebnis[0].VorschauZeilen[0].Should().Equal("Rang", "Team", "Punkte");
    }

    [Fact]
    public void TabellenErmitteln_OhneKopfzeile_WirdNichtErkannt()
    {
        const string html = "<table><tr><td>1</td><td>Ohne Kopfzeile</td></tr></table>";
        var ergebnis = _auswerter.TabellenErmitteln(html);
        ergebnis[0].ErkannteKopfzeile.Should().BeFalse();
    }

    [Fact]
    public void TabellenErmitteln_VorschauZeilen_WerdenGedeckelt()
    {
        var zeilen = string.Concat(Enumerable.Range(1, 20).Select(i => $"<tr><td>{i}</td></tr>"));
        var html = $"<table>{zeilen}</table>";

        var ergebnis = _auswerter.TabellenErmitteln(html);

        ergebnis[0].ZeilenAnzahl.Should().Be(20);
        ergebnis[0].VorschauZeilen.Should().HaveCount(11, "Kopfzeile + 10 Datenzeilen (#477)");
    }

    // Ausschnitt der echten Seite kassiesa.net/uefa/data/method5/trank2026.html (Stand 28.09.2026): Rang und
    // Tendenz-Spalte vor dem Klubnamen, Summe als <th> in Spalte 9, gleiche Punkte ohne Rangnummer (#349).
    private const string KassiesKlubrangliste = """
        <html><body>
        <table class="t1">
        <tr class="groupheader"><th></th><th></th><th></th><th></th><th>21/22</th><th>22/23</th><th>23/24</th>
            <th>24/25</th><th>25/26</th><th>Total<br>Points</th><th>Country<br>Part</th></tr>
        <tr class="clubline"><td>1</td><td></td><td class="aleft">Bayern München</td><td>Ger</td><td>26.00</td>
            <td>27.00</td><td>28.00</td><td>27.25</td><td>39.25</td><th class="lgray">147.500</th><td>18.580</td></tr>
        <tr class="clubline"><td>7</td><td></td><td class="aleft">Arsenal</td><td>Eng</td><td></td><td>17.00</td>
            <td>22.00</td><td>36.00</td><td>44.00</td><th class="lgray">119.000</th><td>23.903</td></tr>
        <tr class="clubline"><td></td><td></td><td class="aleft">Atalanta</td><td>Ita</td><td>14.00</td><td>9.00</td>
            <td>26.00</td><td>17.00</td><td>19.00</td><th class="lgray">85.000</th><td>19.989</td></tr>
        </table>
        </body></html>
        """;

    [Fact]
    public void SeedQuelleUefaKlubKoeffizient_LiestKlubnamenUndSummeAusKassiesTabelle()
    {
        var quelle = Fussballturnierverwaltung.Infrastructure.Seed.Daten.RanglistenQuellenSeedDaten.Erstellen()
            .Single(q => q.Name == Fussballturnierverwaltung.Infrastructure.Seed.Daten.RanglistenQuellenSeedDaten.UEFA_KLUB_KOEFFIZIENT);

        var ergebnis = _auswerter.Auswerten(KassiesKlubrangliste, quelle);

        ergebnis.Select(z => z.RohName).Should().Equal("Bayern München", "Arsenal", "Atalanta");
        ergebnis.Select(z => z.Punkte).Should().Equal(148, 119, 85);
    }
}
