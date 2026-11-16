using FluentAssertions;
using Fussballturnierverwaltung.Application.Simulation;

namespace Fussballturnierverwaltung.Tests.Simulation;

/// <summary>Entscheidungsmodell der Risikostufe (#673): Stufenplan nach Rückstand, Kräfteverhältnis und Unterzahl,
/// Umstellwahrscheinlichkeit nach Erreichen der Schwelle.</summary>
public class RisikoentscheidungTests
{
    private const double GLEICH_STARK = 0.5;

    private static Spiellage Lage(int vorsprung, int verbleibend = 90, double staerke = GLEICH_STARK, int unterzahl = 0)
        => new(vorsprung, verbleibend, staerke, unterzahl);

    private static IEnumerable<(Risikostufe, int)> Plan(Spiellage lage)
        => Risikoentscheidung.Stufenplan(lage).Select(s => (s.Stufe, s.AbVerbleibend));

    /// <summary>Erste Minute, in der die Stufe fällig ist (Restzeit 90 − Minute).</summary>
    private static int? AbMinute(Spiellage lage, Risikostufe stufe)
    {
        for (int minute = 1; minute <= 90; minute++)
            if (Risikoentscheidung.Faellig(lage with { Verbleibend = 90 - minute }).Stufe == stufe)
                return minute;
        return null;
    }

    // ── Stufenplan nach Rückstand (K1, K3, K4) ────────────────────────────

    [Fact]
    public void Plan_EinTorRueckstand_Offensiv60AllesNachVorn80TorwartVorn88()
    {
        Plan(Lage(-1)).Should().Equal(
            (Risikostufe.Offensiv, 30), (Risikostufe.AllesNachVorn, 10), (Risikostufe.Brechstange, 2));
    }

    [Fact]
    public void Plan_ZweiToreRueckstand_FrueherUndNieAussichtslos_OhneTorwartVorn()
    {
        Plan(Lage(-2)).Should().Equal((Risikostufe.Offensiv, 45), (Risikostufe.AllesNachVorn, 25));
    }

    [Theory]
    [InlineData(3, 60, 40, 9)]   // aussichtslos ab der 81.
    [InlineData(4, 75, 55, 19)]  // ab der 71.
    [InlineData(5, 90, 70, 29)]  // offensiv vom Anpfiff, aussichtslos ab der 61.
    [InlineData(6, 105, 85, 39)]
    public void Plan_AbDreiToren_ZurueckZurGewohntenOrdnungWennAussichtslos(
        int rueckstand, int offensiv, int allesNachVorn, int aussichtslos)
    {
        Plan(Lage(-rueckstand)).Should().Equal(
            (Risikostufe.Offensiv, offensiv), (Risikostufe.AllesNachVorn, allesNachVorn), (Risikostufe.Neutral, aussichtslos));
    }

    [Theory]
    [InlineData(-1, 30, Risikostufe.Offensiv)]
    [InlineData(-1, 31, Risikostufe.Neutral)]
    [InlineData(-1, 2, Risikostufe.Brechstange)]
    [InlineData(-1, -4, Risikostufe.Brechstange)]   // Nachspielzeit
    [InlineData(-2, -4, Risikostufe.AllesNachVorn)] // zwei Tore: bis zum Schluss
    [InlineData(-3, 10, Risikostufe.AllesNachVorn)]
    [InlineData(-3, 9, Risikostufe.Neutral)]
    [InlineData(0, 0, Risikostufe.Neutral)]
    [InlineData(1, 21, Risikostufe.Neutral)]
    [InlineData(1, 20, Risikostufe.Mauern)]
    [InlineData(3, -2, Risikostufe.Mauern)]
    public void Faellig_NachVorsprungUndRestzeit(int vorsprung, int verbleibend, Risikostufe erwartet)
    {
        Risikoentscheidung.Faellig(Lage(vorsprung, verbleibend)).Stufe.Should().Be(erwartet);
    }

    [Fact]
    public void Brechstange_NurBeiGenauEinemTorRueckstand()
    {
        var lagen =
            from vorsprung in new[] { 2, 1, 0, -2, -3, -5 }
            from verbleibend in Enumerable.Range(-10, 101)
            from staerke in new[] { 0.2, 0.5, 0.8 }
            from unterzahl in new[] { -1, 0, 1 }
            select Lage(vorsprung, verbleibend, staerke, unterzahl);

        lagen.Select(Risikoentscheidung.Faellig).Should().NotContain(f => f.Stufe == Risikostufe.Brechstange);
    }

    // ── Monotonie (K2) ────────────────────────────────────────────────────

    [Fact]
    public void JeGroesserDerRueckstand_DestoFrueherOffensivUndAllesNachVorn()
    {
        for (int rueckstand = 1; rueckstand < 6; rueckstand++)
        {
            var plan     = Risikoentscheidung.Stufenplan(Lage(-rueckstand));
            var groesser = Risikoentscheidung.Stufenplan(Lage(-rueckstand - 1));
            groesser[0].AbVerbleibend.Should().BeGreaterThan(plan[0].AbVerbleibend);
            groesser[1].AbVerbleibend.Should().BeGreaterThan(plan[1].AbVerbleibend);
        }
    }

    [Fact]
    public void Stufenfolge_OhneToreNurAufwaerts_BisAufAussichtslos()
    {
        for (int rueckstand = 1; rueckstand <= 6; rueckstand++)
        {
            var bisher = Risikostufe.Neutral;
            bool aussichtslos = false;
            for (int verbleibend = 90; verbleibend >= -6; verbleibend--)
            {
                var stufe = Risikoentscheidung.Faellig(Lage(-rueckstand, verbleibend)).Stufe;
                if (stufe < bisher)
                {
                    stufe.Should().Be(Risikostufe.Neutral, "zurück geht es nur, wenn der Rückstand aussichtslos ist");
                    aussichtslos = true;
                }
                else if (aussichtslos)
                    stufe.Should().Be(Risikostufe.Neutral, "aufgegeben ist aufgegeben");
                bisher = stufe;
            }
        }
    }

    // ── Kräfteverhältnis (K5) ─────────────────────────────────────────────

    [Fact]
    public void Favorit_DruecktImRueckstandFrueher_AussenseiterSpaeter()
    {
        AbMinute(Lage(-1, staerke: 0.7), Risikostufe.Offensiv).Should().Be(44);
        AbMinute(Lage(-1), Risikostufe.Offensiv).Should().Be(60);
        AbMinute(Lage(-1, staerke: 0.3), Risikostufe.Offensiv).Should().Be(76);
        AbMinute(Lage(-1, staerke: 0.7), Risikostufe.AllesNachVorn).Should().Be(64);
    }

    [Fact]
    public void Aussenseiter_MauertInFuehrungFrueher_FavoritSpaeter()
    {
        AbMinute(Lage(1, staerke: 0.3), Risikostufe.Mauern).Should().Be(54);
        AbMinute(Lage(1), Risikostufe.Mauern).Should().Be(70);
        AbMinute(Lage(1, staerke: 0.7), Risikostufe.Mauern).Should().Be(86);
    }

    [Fact]
    public void Kraefteverhaeltnis_VerschiebtWederTorwartVornNochAussichtslos()
    {
        Risikoentscheidung.Stufenplan(Lage(-1, staerke: 0.2))[^1].Should().Be(new FaelligeStufe(Risikostufe.Brechstange, 2));
        Risikoentscheidung.Stufenplan(Lage(-3, staerke: 0.8))[^1].Should().Be(new FaelligeStufe(Risikostufe.Neutral, 9));
    }

    [Fact]
    public void Aussenseiter_SpaetesAllesNachVorn_FaelltNichtHinterDieBrechstangeZurueck()
    {
        // Stärkeanteil 0,2: alles nach vorn wäre erst bei Restzeit −14 fällig, die Brechstange schon bei 2.
        var lage = Lage(-1, staerke: 0.2);

        Risikoentscheidung.Faellig(lage with { Verbleibend = -6 }).Stufe.Should().Be(Risikostufe.Brechstange);
    }

    // ── Unterzahl (K6) ────────────────────────────────────────────────────

    [Theory]
    [InlineData(1, 55)]   // je Mann weniger 15 Minuten früher mauern
    [InlineData(2, 40)]
    [InlineData(-1, 85)]  // Überzahl: später
    public void Unterzahl_MauertInFuehrungFrueher(int unterzahl, int abMinute)
    {
        AbMinute(Lage(1, unterzahl: unterzahl), Risikostufe.Mauern).Should().Be(abMinute);
    }

    [Theory]
    [InlineData(1, 75)]
    [InlineData(2, 60)]
    public void Unterzahl_MauertBeiGleichstand(int unterzahl, int abMinute)
    {
        AbMinute(Lage(0, unterzahl: unterzahl), Risikostufe.Mauern).Should().Be(abMinute);
    }

    [Fact]
    public void Gleichzahl_UndUeberzahl_BeiGleichstandGewohnteOrdnung()
    {
        Plan(Lage(0)).Should().BeEmpty();
        Plan(Lage(0, unterzahl: -1)).Should().BeEmpty();
    }

    [Theory]
    [InlineData(1, 70)]   // je Mann weniger 10 Minuten später offensiv
    [InlineData(-1, 50)]  // Überzahl: früher
    public void Unterzahl_ImRueckstandSpaeterOffensiv(int unterzahl, int abMinute)
    {
        AbMinute(Lage(-1, unterzahl: unterzahl), Risikostufe.Offensiv).Should().Be(abMinute);
    }

    // ── Umstellwahrscheinlichkeit (K7) ────────────────────────────────────

    [Theory]
    [InlineData(31, 0.0)]
    [InlineData(30, 0.125)]
    [InlineData(29, 0.25)]
    [InlineData(24, 0.875)]
    [InlineData(23, 1.0)]
    [InlineData(-5, 1.0)]
    public void Umstellwahrscheinlichkeit_SteigtJeMinuteUmEinAchtelAbDerSchwelle(int verbleibend, double erwartet)
    {
        Risikoentscheidung.Umstellwahrscheinlichkeit(new FaelligeStufe(Risikostufe.Offensiv, 30), verbleibend)
            .Should().BeApproximately(erwartet, 1e-9);
    }

    [Fact]
    public void Umstellwahrscheinlichkeit_OhneSchwelleNie_NurEinTorFuehrtZurGewohntenOrdnungZurueck()
    {
        var ohneSchwelle = new FaelligeStufe(Risikostufe.Neutral, Risikoentscheidung.OHNE_SCHWELLE);

        Risikoentscheidung.Faellig(Lage(-1, verbleibend: 60)).Should().Be(ohneSchwelle);
        Risikoentscheidung.Umstellwahrscheinlichkeit(ohneSchwelle, 60).Should().Be(0);
        Risikoentscheidung.Umstellwahrscheinlichkeit(ohneSchwelle, -5).Should().Be(0);
    }
}
