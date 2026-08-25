using FluentAssertions;
using Fussballturnierverwaltung.Application.Ranglisten.Eingaben;
using Fussballturnierverwaltung.Application.Validierung;

namespace Fussballturnierverwaltung.Tests.Validierung;

public class RanglistenQuelleValidatorTests
{
    private readonly RanglistenQuelleValidator _validator = new();

    private static RanglistenQuelleErstellenEingabe GueltigeEingabe(
        string? url = null, int mannschaftSpalte = 1, int? punkteSpalte = 2) =>
        new("FIFA-Weltrangliste (Wikipedia)",
            url ?? "https://en.wikipedia.org/wiki/FIFA_World_Ranking",
            TabellenIndex: 0,
            TabellenSelektor: null,
            MannschaftSpalte: mannschaftSpalte,
            PunkteSpalte: punkteSpalte,
            KopfzeileUeberspringen: true);

    [Fact]
    public void GueltigeEingabe_IstGueltig()
    {
        _validator.Validieren(GueltigeEingabe()).IstGueltig.Should().BeTrue();
    }

    [Fact]
    public void LeererName_IstUngueltig()
    {
        var eingabe = GueltigeEingabe() with { Name = "" };
        _validator.Validieren(eingabe).IstUngueltig.Should().BeTrue();
    }

    [Fact]
    public void LeereUrl_IstUngueltig()
    {
        var ergebnis = _validator.Validieren(GueltigeEingabe(url: ""));
        ergebnis.IstUngueltig.Should().BeTrue();
    }

    [Fact]
    public void UngueltigeUrl_IstUngueltig()
    {
        var ergebnis = _validator.Validieren(GueltigeEingabe(url: "keine-url"));
        ergebnis.IstUngueltig.Should().BeTrue();
        ergebnis.Fehler.Should().ContainMatch("*URL*");
    }

    [Fact]
    public void FtpUrl_IstUngueltig()
    {
        var ergebnis = _validator.Validieren(GueltigeEingabe(url: "ftp://example.com/daten.csv"));
        ergebnis.IstUngueltig.Should().BeTrue();
    }

    [Fact]
    public void OhnePunkteSpalte_IstGueltig()
    {
        _validator.Validieren(GueltigeEingabe(punkteSpalte: null)).IstGueltig.Should().BeTrue();
    }

    [Fact]
    public void MannschaftSpalteGleichPunkteSpalte_IstUngueltig()
    {
        var ergebnis = _validator.Validieren(GueltigeEingabe(mannschaftSpalte: 1, punkteSpalte: 1));
        ergebnis.IstUngueltig.Should().BeTrue();
    }

    [Fact]
    public void NegativeMannschaftSpalte_IstUngueltig()
    {
        var ergebnis = _validator.Validieren(GueltigeEingabe(mannschaftSpalte: -1));
        ergebnis.IstUngueltig.Should().BeTrue();
    }

    [Fact]
    public void AktualisierenEingabe_WirdGleichValidiert()
    {
        var eingabe = new RanglistenQuelleAktualisierenEingabe(
            "", "https://example.com", 0, null, 0, null, true);
        _validator.Validieren(eingabe).IstUngueltig.Should().BeTrue();
    }
}
