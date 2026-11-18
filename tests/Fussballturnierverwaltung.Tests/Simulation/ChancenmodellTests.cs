using FluentAssertions;
using Fussballturnierverwaltung.Application.Simulation;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Tests.Simulation;

/// <summary>Chancenarten, Verwertungsquoten und Aufteilung der Torerwartung nach Ratings (#431, K5).</summary>
public class ChancenmodellTests
{
    private static Mannschaft Team(int offensiv, int mittelfeld, int defensiv)
        => new() { OffensivRating = offensiv, MittelfeldRating = mittelfeld, DefensivRating = defensiv };

    // ── Art ──────────────────────────────────────────────────────────────

    [Fact]
    public void Art_AnDenBandgrenzen()
    {
        Chancenmodell.Art(0.0).Should().Be(Chancenart.Elfmeter);
        Chancenmodell.Art(Chancenmodell.ANTEIL_ELFMETER - 1e-9).Should().Be(Chancenart.Elfmeter);
        Chancenmodell.Art(Chancenmodell.ANTEIL_ELFMETER).Should().Be(Chancenart.Grosschance);
        Chancenmodell.Art(Chancenmodell.ANTEIL_ELFMETER + Chancenmodell.ANTEIL_GROSSCHANCE - 1e-9)
            .Should().Be(Chancenart.Grosschance);
        Chancenmodell.Art(Chancenmodell.ANTEIL_ELFMETER + Chancenmodell.ANTEIL_GROSSCHANCE).Should().Be(Chancenart.Normal);
        Chancenmodell.Art(0.999).Should().Be(Chancenart.Normal);
    }

    // ── Quote ────────────────────────────────────────────────────────────

    [Fact]
    public void Quote_Elfmeter_IstUnabhaengigVonDerVerwertung()
    {
        Chancenmodell.Quote(Chancenart.Elfmeter, 0.5).Should().Be(Chancenmodell.QUOTE_ELFMETER);
        Chancenmodell.Quote(Chancenart.Elfmeter, 1.3).Should().Be(Chancenmodell.QUOTE_ELFMETER);
    }

    [Fact]
    public void Quote_SonstGrundquoteMalVerwertung_HoechstensDeckel()
    {
        Chancenmodell.Quote(Chancenart.Grosschance, 1.0).Should().BeApproximately(0.37, 1e-12);
        Chancenmodell.Quote(Chancenart.Normal, 1.2).Should().BeApproximately(0.07 * 1.2, 1e-12);
        Chancenmodell.Quote(Chancenart.Grosschance, 5.0).Should().Be(0.95);
    }

    [Fact]
    public void MittlereQuote_BeiVerwertungEins_IstDerGewichteteMittelwert()
    {
        // (0,16 · 0,785 + 1,85 · 0,37 + 10,9 · 0,07) / 12,91
        Chancenmodell.MittlereQuote(1.0).Should().BeApproximately(0.12185, 0.00001);
    }

    // ── Aufteilung nach Ratings (K5) ─────────────────────────────────────

    [Fact]
    public void Profil_GleicheRatings_VerwertungEins()
    {
        var profil = Chancenprofil.Aus(1.4, Team(70, 70, 70), Team(70, 70, 70));

        profil.Verwertung.Should().Be(1.0);
    }

    [Fact]
    public void Profil_MehrMittelfeld_MehrChancenSchlechtereVerwertung_ProduktBleibt()
    {
        const double lambda = 1.5;
        var verteidiger = Team(70, 70, 70);
        var a = Chancenprofil.Aus(lambda, Team(70, 84, 70), verteidiger);   // hohes Mittelfeld
        var b = Chancenprofil.Aus(lambda, Team(84, 70, 70), verteidiger);   // hoher Offensivwert

        a.ChancenJe90.Should().BeGreaterThan(b.ChancenJe90);
        a.Verwertung.Should().BeLessThan(b.Verwertung);
        (a.ChancenJe90 * Chancenmodell.MittlereQuote(a.Verwertung)).Should().BeApproximately(lambda, 1e-9);
        (b.ChancenJe90 * Chancenmodell.MittlereQuote(b.Verwertung)).Should().BeApproximately(lambda, 1e-9);
    }

    [Fact]
    public void Profil_SchwacheGegnerischeDefensive_BessereVerwertung()
    {
        var angreifer = Team(70, 70, 70);

        var gegenStarke = Chancenprofil.Aus(1.4, angreifer, Team(70, 70, 85));
        var gegenSchwache = Chancenprofil.Aus(1.4, angreifer, Team(70, 70, 55));

        gegenSchwache.Verwertung.Should().BeGreaterThan(gegenStarke.Verwertung);
    }

    [Fact]
    public void Profil_Verwertung_IstBegrenzt()
    {
        Chancenprofil.Aus(1.4, Team(100, 10, 70), Team(70, 100, 10)).Verwertung.Should().Be(1.3);
        Chancenprofil.Aus(1.4, Team(10, 100, 70), Team(70, 10, 100)).Verwertung.Should().Be(0.75);
    }

    [Fact]
    public void Profil_RatingNull_FaktorEins()
    {
        var profil = Chancenprofil.Aus(1.4, Team(0, 0, 0), Team(0, 0, 0));

        profil.Verwertung.Should().Be(1.0);
        profil.ChancenJe90.Should().BeApproximately(1.4 / Chancenmodell.MittlereQuote(1.0), 1e-9);
    }
}
