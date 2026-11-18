using FluentAssertions;
using Fussballturnierverwaltung.Application.Simulation;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Tests.Simulation;

/// <summary>Kondition (#677): Konditionsrückstand, Faktor auf die Chancenrate, Ermüdungsfaktor und Verschiebung der
/// Risikoentscheidung. „Normal“ ändert nichts.</summary>
public class KonditionTests
{
    private static Teamprofil Profil(Kondition kondition, Grundausrichtung grundausrichtung = Grundausrichtung.Ausgeglichen)
        => new(grundausrichtung, Flexibilitaet.Normal, Spielbedeutung.Normal, kondition);

    // ── K2 Rückstand ──────────────────────────────────────────────────────

    [Theory]
    [InlineData(Kondition.Schwach)]
    [InlineData(Kondition.Normal)]
    [InlineData(Kondition.Stark)]
    public void Rueckstand_BisZurHalbzeitNull(Kondition kondition)
    {
        double k = Profil(kondition).Ermuedungsfaktor;
        for (int minute = 0; minute <= 45; minute++)
            Spielsituation.Konditionsrueckstand(Spielsituation.Ermuedung(minute), k).Should().Be(0);
    }

    [Fact]
    public void Rueckstand_Normal_InJederMinuteNull()
    {
        for (int minute = 0; minute <= 120; minute++)
            Spielsituation.Konditionsrueckstand(Spielsituation.Ermuedung(minute), Profil(Kondition.Normal).Ermuedungsfaktor).Should().Be(0);
    }

    [Fact]
    public void Rueckstand_NachNeunzigMinuten_SchwachPositivStarkNegativBetragsgleich()
    {
        double e = Spielsituation.Ermuedung(90);
        double schwach = Spielsituation.Konditionsrueckstand(e, Profil(Kondition.Schwach).Ermuedungsfaktor);
        double stark = Spielsituation.Konditionsrueckstand(e, Profil(Kondition.Stark).Ermuedungsfaktor);
        schwach.Should().BeGreaterThan(0);
        stark.Should().BeLessThan(0);
        schwach.Should().BeApproximately(-stark, 1e-12);
    }

    [Fact]
    public void Rueckstand_WaechstInDerVerlaengerungWeiter()
    {
        double k = Profil(Kondition.Schwach).Ermuedungsfaktor;
        Spielsituation.Konditionsrueckstand(Spielsituation.Ermuedung(120), k)
            .Should().BeGreaterThan(Spielsituation.Konditionsrueckstand(Spielsituation.Ermuedung(90), k));
    }

    [Fact]
    public void Konditionsfaktor_OhneRueckstand_IstExaktEins()
    {
        Spielsituation.Konditionsfaktor(0, 0).Should().Be(1.0);
    }

    [Fact]
    public void Konditionsfaktor_EigenerRueckstandMindert_RueckstandDesGegnersStaerktDenAngriff()
    {
        Spielsituation.Konditionsfaktor(0.1, 0).Should().BeLessThan(1);
        Spielsituation.Konditionsfaktor(0, 0.1).Should().BeGreaterThan(1);
    }

    // ── K7 Risikoentscheidung ─────────────────────────────────────────────

    [Fact]
    public void Offensivdrang_SchwacheKonditionFuenfMinutenWeniger_StarkWieNormal()
    {
        Profil(Kondition.Normal).Offensivdrang.Should().Be(0);
        Profil(Kondition.Schwach).Offensivdrang.Should().Be(-5);
        Profil(Kondition.Stark).Offensivdrang.Should().Be(0);
        Profil(Kondition.Schwach, Grundausrichtung.Offensiv).Offensivdrang.Should().Be(0);
    }

    [Fact]
    public void Stufenplan_Fuehrung_SchwachesTeamStelltFuenfMinutenFruehererUm_StarkWieNormal()
    {
        var normal = Risikoentscheidung.Stufenplan(Lage(vorsprung: 1, Kondition.Normal));
        var schwach = Risikoentscheidung.Stufenplan(Lage(vorsprung: 1, Kondition.Schwach));
        var stark = Risikoentscheidung.Stufenplan(Lage(vorsprung: 1, Kondition.Stark));

        stark.Should().Equal(normal);
        schwach.Select(f => f.Stufe).Should().Equal(normal.Select(f => f.Stufe));
        schwach.Zip(normal).Should().AllSatisfy(p => p.First.AbVerbleibend.Should().Be(p.Second.AbVerbleibend + 5),
            "in Führung wird fünf Minuten früher defensiv bzw. gemauert");
    }

    [Fact]
    public void Stufenplan_Rueckstand_SchwachesTeamDruecktFuenfMinutenSpaeter_StarkWieNormal()
    {
        var normal = Risikoentscheidung.Stufenplan(Lage(vorsprung: -1, Kondition.Normal));
        var schwach = Risikoentscheidung.Stufenplan(Lage(vorsprung: -1, Kondition.Schwach));
        var stark = Risikoentscheidung.Stufenplan(Lage(vorsprung: -1, Kondition.Stark));

        stark.Should().Equal(normal);
        // Offensiv und alles nach vorn verschieben sich; die Brechstange hängt an der Restzeit, nicht am Drang.
        schwach[0].AbVerbleibend.Should().Be(normal[0].AbVerbleibend - 5);
        schwach[1].AbVerbleibend.Should().Be(normal[1].AbVerbleibend - 5);
    }

    private static Spiellage Lage(int vorsprung, Kondition kondition)
        => new(vorsprung, 90, 0.5, 0, Profil(kondition));

    // ── Teamprofil.Von ────────────────────────────────────────────────────

    [Fact]
    public void Von_UebernimmtDieKonditionDerMannschaft()
    {
        var team = SimulationsEngineTestdaten.Team(1);
        team.Kondition = Kondition.Schwach;
        Teamprofil.Von(team).Kondition.Should().Be(Kondition.Schwach);
    }
}
