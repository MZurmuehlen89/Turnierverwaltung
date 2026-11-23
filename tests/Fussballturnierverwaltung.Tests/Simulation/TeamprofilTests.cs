using FluentAssertions;
using Fussballturnierverwaltung.Application.Simulation;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Tests.Simulation;

/// <summary>Grundausrichtung und Flexibilität eines Teams (#667): Startpunkt auf der Risikoachse, Ziel einer fälligen
/// Stufe, Umstellfenster und Verschiebung der Schwellen.</summary>
public class TeamprofilTests
{
    private static Teamprofil Profil(Grundausrichtung grundausrichtung, Flexibilitaet flexibilitaet = Flexibilitaet.Normal)
        => new(grundausrichtung, flexibilitaet);

    // ── Startpunkt und gewohnte Ordnung (K3) ──────────────────────────────

    [Theory]
    [InlineData(Grundausrichtung.SehrDefensiv, -1.0)]
    [InlineData(Grundausrichtung.Defensiv, -0.5)]
    [InlineData(Grundausrichtung.Ausgeglichen, 0.0)]
    [InlineData(Grundausrichtung.Offensiv, 0.5)]
    [InlineData(Grundausrichtung.SehrOffensiv, 1.0)]
    public void Startpunkt_JeGrundausrichtung(Grundausrichtung grundausrichtung, double startpunkt)
    {
        Profil(grundausrichtung).Startpunkt.Should().Be(startpunkt);
    }

    [Theory]
    [InlineData(Flexibilitaet.Starr)]
    [InlineData(Flexibilitaet.Normal)]
    [InlineData(Flexibilitaet.Flexibel)]
    public void GewohnteOrdnung_IstDerStartpunkt_UnabhaengigVonDerFlexibilitaet(Flexibilitaet flexibilitaet)
    {
        Profil(Grundausrichtung.Offensiv, flexibilitaet).Ziel(Risikostufe.Neutral).Should().Be(0.5);
        Profil(Grundausrichtung.SehrDefensiv, flexibilitaet).Ziel(Risikostufe.Neutral).Should().Be(-1);
    }

    [Fact]
    public void Standard_IstAusgeglichenUndNormal()
    {
        default(Teamprofil).Should().Be(Profil(Grundausrichtung.Ausgeglichen, Flexibilitaet.Normal));
    }

    // ── Ziel einer fälligen Stufe (K5) ────────────────────────────────────

    [Theory]
    [InlineData(Flexibilitaet.Flexibel, -2.0)]   // genau die verlangte Stufe
    [InlineData(Flexibilitaet.Normal, -1.75)]    // halber Versatz
    [InlineData(Flexibilitaet.Starr, -1.5)]      // voller Versatz
    public void Ziel_OffensivesTeamMauert_JeNachFlexibilitaet(Flexibilitaet flexibilitaet, double ziel)
    {
        Profil(Grundausrichtung.Offensiv, flexibilitaet).Ziel(Risikostufe.Mauern).Should().Be(ziel);
    }

    [Theory]
    [InlineData(Risikostufe.Defensiv, Flexibilitaet.Starr, -2.0)]
    [InlineData(Risikostufe.Offensiv, Flexibilitaet.Normal, 0.5)]
    [InlineData(Risikostufe.AllesNachVorn, Flexibilitaet.Flexibel, 2.0)]
    public void Ziel_DefensivesTeam_BleibtHinterDerVerlangtenStufe(Risikostufe stufe, Flexibilitaet flexibilitaet, double ziel)
    {
        Profil(Grundausrichtung.SehrDefensiv, flexibilitaet).Ziel(stufe).Should().Be(ziel);
    }

    [Theory]
    [InlineData(Risikostufe.Mauern)]
    [InlineData(Risikostufe.Defensiv)]
    [InlineData(Risikostufe.Offensiv)]
    [InlineData(Risikostufe.AllesNachVorn)]
    [InlineData(Risikostufe.Brechstange)]
    public void Ziel_AusgeglichenesTeam_GenauDieStufe_BeiJederFlexibilitaet(Risikostufe stufe)
    {
        foreach (var flexibilitaet in Enum.GetValues<Flexibilitaet>())
            Profil(Grundausrichtung.Ausgeglichen, flexibilitaet).Ziel(stufe).Should().Be(Spielsituation.Position(stufe));
    }

    [Fact]
    public void Ziel_BleibtAufDerAchse()
    {
        Profil(Grundausrichtung.SehrOffensiv, Flexibilitaet.Starr).Ziel(Risikostufe.Brechstange).Should().Be(3);
        Profil(Grundausrichtung.SehrDefensiv, Flexibilitaet.Starr).Ziel(Risikostufe.Mauern).Should().Be(-2);
    }

    // ── Tempo und Schwellen (K4, K6) ──────────────────────────────────────

    [Theory]
    [InlineData(Flexibilitaet.Starr, 12)]
    [InlineData(Flexibilitaet.Normal, 8)]
    [InlineData(Flexibilitaet.Flexibel, 4)]
    public void Umstellfenster_JeFlexibilitaet(Flexibilitaet flexibilitaet, int fenster)
    {
        Profil(Grundausrichtung.Ausgeglichen, flexibilitaet).Umstellfenster.Should().Be(fenster);
    }

    [Theory]
    [InlineData(Grundausrichtung.SehrDefensiv, -10)]
    [InlineData(Grundausrichtung.Defensiv, -5)]
    [InlineData(Grundausrichtung.Ausgeglichen, 0)]
    [InlineData(Grundausrichtung.Offensiv, 5)]
    [InlineData(Grundausrichtung.SehrOffensiv, 10)]
    public void Offensivdrang_FuenfMinutenJeStufe(Grundausrichtung grundausrichtung, int minuten)
    {
        Profil(grundausrichtung).Offensivdrang.Should().Be(minuten);
    }

    // ── Tabellenbedarf (#176, K7) ─────────────────────────────────────────

    [Theory]
    [InlineData(Tabellenbedarf.Offen, 0.0)]
    [InlineData(Tabellenbedarf.Abstiegskampf, -0.25)]
    [InlineData(Tabellenbedarf.OhneZiel, 0.5)]
    [InlineData(Tabellenbedarf.SiegNoetig, 0.0)]
    [InlineData(Tabellenbedarf.RemisReicht, 0.0)]
    public void Tabellenbedarf_VerschiebtDenStartpunkt(Tabellenbedarf bedarf, double startpunkt)
    {
        new Teamprofil(Grundausrichtung.Ausgeglichen, Flexibilitaet.Normal, Bedarf: bedarf).Startpunkt.Should().Be(startpunkt);
    }

    [Fact]
    public void Tabellenbedarf_KommtZurSpielbedeutungHinzu()
    {
        new Teamprofil(Grundausrichtung.Ausgeglichen, Flexibilitaet.Normal, Spielbedeutung.Gering, Bedarf: Tabellenbedarf.OhneZiel)
            .Startpunkt.Should().Be(1.0);
    }

    // ── Spielbedeutung der Runde (#668, K2) ───────────────────────────────

    [Theory]
    [InlineData(Spielbedeutung.Hoch, -0.25)]
    [InlineData(Spielbedeutung.Normal, 0.0)]
    [InlineData(Spielbedeutung.Gering, 0.5)]
    public void Spielbedeutung_VerschiebtDenStartpunkt(Spielbedeutung spielbedeutung, double startpunkt)
    {
        new Teamprofil(Grundausrichtung.Ausgeglichen, Flexibilitaet.Normal, spielbedeutung).Startpunkt.Should().Be(startpunkt);
    }

    [Theory]
    [InlineData(Grundausrichtung.SehrDefensiv, Spielbedeutung.Hoch, -1.25)]
    [InlineData(Grundausrichtung.SehrOffensiv, Spielbedeutung.Gering, 1.5)]
    [InlineData(Grundausrichtung.Defensiv, Spielbedeutung.Gering, 0.0)]
    public void Spielbedeutung_KommtZurGrundausrichtungHinzu(
        Grundausrichtung grundausrichtung, Spielbedeutung spielbedeutung, double startpunkt)
    {
        new Teamprofil(grundausrichtung, Flexibilitaet.Normal, spielbedeutung).Startpunkt.Should().Be(startpunkt);
    }

    [Theory]
    [InlineData(Flexibilitaet.Starr, -1.25)]    // voller Versatz
    [InlineData(Flexibilitaet.Normal, -1.125)]  // halber Versatz
    [InlineData(Flexibilitaet.Flexibel, -1.0)]  // genau die verlangte Stufe
    public void Spielbedeutung_Hoch_VerschiebtDieZiele_WieDieGrundausrichtung(Flexibilitaet flexibilitaet, double ziel)
    {
        var profil = new Teamprofil(Grundausrichtung.Ausgeglichen, flexibilitaet, Spielbedeutung.Hoch);

        profil.Ziel(Risikostufe.Defensiv).Should().Be(ziel);
        profil.Ziel(Risikostufe.Neutral).Should().Be(-0.25);
        profil.Ziel(Risikostufe.Mauern).Should().Be(Spielsituation.ACHSE_ANFANG, "die Achse endet beim Mauern");
    }

    [Theory]
    [InlineData(Flexibilitaet.Starr, 1.5)]
    [InlineData(Flexibilitaet.Normal, 1.25)]
    [InlineData(Flexibilitaet.Flexibel, 1.0)]
    public void Spielbedeutung_Gering_OffensiveStufeJeNachFlexibilitaet(Flexibilitaet flexibilitaet, double ziel)
    {
        new Teamprofil(Grundausrichtung.Ausgeglichen, flexibilitaet, Spielbedeutung.Gering)
            .Ziel(Risikostufe.Offensiv).Should().Be(ziel);
    }

    [Fact]
    public void Spielbedeutung_AendertWederSchwellenNochTempo()
    {
        var profil = new Teamprofil(Grundausrichtung.Ausgeglichen, Flexibilitaet.Normal, Spielbedeutung.Hoch);

        profil.Offensivdrang.Should().Be(0);
        profil.Umstellfenster.Should().Be(Teamprofil.NORMALES_UMSTELLFENSTER);
    }
}
