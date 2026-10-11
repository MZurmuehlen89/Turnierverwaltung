using FluentAssertions;
using Fussballturnierverwaltung.Application.Spielplan;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Tests.Spielplan;

public class PlatzhalterNamenTests
{
    private static TurnierRunde Runde(string name, Rundenart art, int reihenfolge, int eingang = 0) =>
        new() { Id = reihenfolge, Name = name, Rundenart = art, Reihenfolge = reihenfolge, EingangsMannschaftsanzahl = eingang };

    // ── Erkennen ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Sieger Gruppe A", "A", 1)]
    [InlineData("sieger gruppe a", "a", 1)]
    [InlineData("Sieger Gruppe 12", "12", 1)]
    [InlineData("2. Gruppe B", "B", 2)]
    public void Erkennen_Gruppenrang(string name, string kennung, int rang) =>
        PlatzhalterNamen.Erkennen(name).Should().Be(new PlatzhalterBezug.GruppenRang(kennung, rang));

    [Fact]
    public void Erkennen_Kombinationsplatz_GehtDemGruppenrangVor() =>
        PlatzhalterNamen.Erkennen("3. Gruppe A/D/E/F").Should().Be(new PlatzhalterBezug.KombinationsPlatz(3));

    [Fact]
    public void Erkennen_BestePlatzierung() =>
        PlatzhalterNamen.Erkennen("2. bester 3. Platz").Should().Be(new PlatzhalterBezug.BestePlatzierung(2, 3));

    [Theory]
    [InlineData("Sieger Halbfinale 1", "Halbfinale", 1, false)]
    [InlineData("Verlierer Halbfinale 2", "Halbfinale", 2, true)]
    [InlineData("Sieger Playoff-Runde 4", "Playoff-Runde", 4, false)]
    [InlineData("Sieger 1. Runde 3", "1. Runde", 3, false)]
    [InlineData("verlierer hf 1", "hf", 1, true)]
    public void Erkennen_KoSpiel_DieLetzteZahlIstDieSpielnummer(string name, string kuerzel, int spielNummer, bool verlierer) =>
        PlatzhalterNamen.Erkennen(name).Should().Be(new PlatzhalterBezug.KoSpiel(kuerzel, spielNummer, verlierer));

    [Fact]
    public void Erkennen_Tabellenplatz() =>
        PlatzhalterNamen.Erkennen("Platz 3 (Ligaphase)").Should().Be(new PlatzhalterBezug.TabellenPlatz(3, "Ligaphase"));

    [Theory]
    [InlineData("")]
    [InlineData("Bayern München")]
    [InlineData("Sieger Halbfinale")]
    [InlineData("Platz drei (Ligaphase)")]
    public void Erkennen_UnbekannterText_IstNull(string name) =>
        PlatzhalterNamen.Erkennen(name).Should().BeNull();

    [Fact]
    public void KoSpielErkennen_KenntNurDieKoMuster()
    {
        PlatzhalterNamen.KoSpielErkennen("Sieger Halbfinale 1").Should().Be(new PlatzhalterBezug.KoSpiel("Halbfinale", 1, false));
        PlatzhalterNamen.KoSpielErkennen("2. Gruppe B").Should().BeNull();
        PlatzhalterNamen.KoSpielErkennen("Sieger Gruppe A").Should().BeNull();
    }

    // ── KoQuellrunde ────────────────────────────────────────────────────────

    private static readonly TurnierRunde _achtelfinale  = Runde("Achtelfinale", Rundenart.KoRunde, 1, eingang: 16);
    private static readonly TurnierRunde _viertelfinale = Runde("Viertelfinale", Rundenart.KoRunde, 2, eingang: 8);
    private static readonly TurnierRunde _halbfinale    = Runde("Halbfinale", Rundenart.KoRunde, 3, eingang: 4);
    private static readonly TurnierRunde _platzDrei     = Runde("Spiel um Platz 3", Rundenart.SpielUmPlatzDrei, 4, eingang: 2);
    private static readonly TurnierRunde _finale        = Runde("Finale", Rundenart.Finale, 5, eingang: 2);
    private static readonly TurnierRunde _supercup      = Runde("Supercup", Rundenart.KoRunde, 6, eingang: 2);

    private static readonly IReadOnlyList<TurnierRunde> _runden =
        [_achtelfinale, _viertelfinale, _halbfinale, _platzDrei, _finale, _supercup];

    private static TurnierRunde? Quellrunde(string kuerzel, bool verlierer, TurnierRunde ziel, IReadOnlyList<TurnierRunde>? runden = null) =>
        PlatzhalterNamen.KoQuellrunde(new PlatzhalterBezug.KoSpiel(kuerzel, 1, verlierer), ziel, runden ?? _runden);

    [Fact]
    public void KoQuellrunde_FindetDieRundePerNamen_OhneGrossKleinschreibung() =>
        Quellrunde("halbfinale", verlierer: false, _finale).Should().BeSameAs(_halbfinale);

    [Fact]
    public void KoQuellrunde_NurVorhergehendeRunden() =>
        Quellrunde("Halbfinale", verlierer: false, _viertelfinale).Should().BeNull();

    [Fact]
    public void KoQuellrunde_SiegerKannAusEinemFinaleKommen_VerliererNicht()
    {
        Quellrunde("Finale", verlierer: false, _supercup).Should().BeSameAs(_finale);
        Quellrunde("Finale", verlierer: true, _supercup).Should().BeNull();
    }

    [Fact]
    public void KoQuellrunde_GleichnamigeRunden_NimmtDieNaechstgelegeneVorhergehende()
    {
        var erste  = Runde("Playoff", Rundenart.KoRunde, 1);
        var zweite = Runde("Playoff", Rundenart.KoRunde, 3);
        var ziel   = Runde("Finale", Rundenart.Finale, 4);

        Quellrunde("Playoff", verlierer: false, ziel, [erste, Runde("Liga", Rundenart.Liga, 2), zweite, ziel])
            .Should().BeSameAs(zweite);
    }

    [Theory]
    [InlineData("AF", 1)]
    [InlineData("VF", 2)]
    [InlineData("HF", 3)]
    public void KoQuellrunde_OhneNamenstreffer_FindetUeberDasKuerzelDieRundeMitPassenderTeamanzahl(string kuerzel, int reihenfolge) =>
        Quellrunde(kuerzel, verlierer: false, _finale)!.Reihenfolge.Should().Be(reihenfolge);

    [Fact]
    public void KoQuellrunde_NamenstrefferGehtDemKuerzelVor()
    {
        var heisstHf = Runde("HF", Rundenart.KoRunde, 1, eingang: 16);
        var vierer   = Runde("Halbfinale", Rundenart.KoRunde, 2, eingang: 4);
        var ziel     = Runde("Finale", Rundenart.Finale, 3);

        Quellrunde("HF", verlierer: false, ziel, [heisstHf, vierer, ziel]).Should().BeSameAs(heisstHf);
    }

    [Fact]
    public void KoQuellrunde_UnbekanntesKuerzel_IstNull() =>
        Quellrunde("Zwischenrunde", verlierer: false, _finale).Should().BeNull();
}
