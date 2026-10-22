using FluentAssertions;
using Fussballturnierverwaltung.Application.Spielplan;

namespace Fussballturnierverwaltung.Tests.Spielplan;

public class SpielplanVerlustVorschauTests
{
    private static KaskadenSpielSchritt Folge(string runde, bool hatteErgebnis) =>
        new(SpielId: 1, RundeId: 2, RundeName: runde, HatteErgebnis: hatteErgebnis,
            NeuesHeimMannschaftId: null, NeuesAuswaertsMannschaftId: null,
            HeimPlatzhalterName: null, AuswaertsPlatzhalterName: null);

    [Fact]
    public void Aus_OhneSpielplan_KeinHinweisNichtsBetroffen() =>
        SpielplanVerlustVorschau.Aus(0, 0, []).Should().Be(SpielplanVerlust.Keiner);

    // #405: frisch ausgelost, noch ohne Ergebnis – Hinweis ja, aber keine Rückfrage beim Zurücksetzen nötig.
    [Fact]
    public void Aus_SpieleOhneErgebnis_HinweisAberNichtBetroffen()
    {
        var verlust = SpielplanVerlustVorschau.Aus(8, 0, []);

        verlust.Hinweis.Should().Be("\n\nDer Spielplan der Runde wird mitgelöscht: 8 Spiel(e), noch ohne Ergebnis.");
        verlust.DatenBetroffen.Should().BeFalse();
    }

    [Fact]
    public void Aus_SpieleMitErgebnis_Betroffen()
    {
        var verlust = SpielplanVerlustVorschau.Aus(8, 3, []);

        verlust.Hinweis.Should().Contain("8 Spiel(e), davon 3 mit eingetragenem Ergebnis.");
        verlust.DatenBetroffen.Should().BeTrue();
    }

    // #306: aufgelöste Paarungen in Folgerunden gehen auch ohne Ergebnis verloren.
    [Fact]
    public void Aus_FolgerundenOhneErgebnis_NenntRundenEinmalUndIstBetroffen()
    {
        var verlust = SpielplanVerlustVorschau.Aus(8, 0,
            [Folge("Viertelfinale", false), Folge("Viertelfinale", false), Folge("Halbfinale", false)]);

        verlust.Hinweis.Should().EndWith(
            "\n\nZusätzlich werden bereits feststehende Paarungen in Folgerunden zurückgesetzt (Viertelfinale, Halbfinale).");
        verlust.DatenBetroffen.Should().BeTrue();
    }

    [Fact]
    public void Aus_FolgerundenMitErgebnis_ZaehltErgebnisse() =>
        SpielplanVerlustVorschau.Aus(8, 0, [Folge("Viertelfinale", true), Folge("Viertelfinale", false), Folge("Halbfinale", true)])
            .Hinweis.Should().EndWith(
                "\n\nZusätzlich werden 2 Ergebnis(se) in Folgerunden zurückgesetzt, deren Paarungen davon abhängen " +
                "(Viertelfinale, Halbfinale).");
}
