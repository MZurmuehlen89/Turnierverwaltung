using FluentAssertions;
using Fussballturnierverwaltung.Application.Statistiken;
using Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Tests.Statistiken;

public class MannschaftsStatistikBerechnungTests
{
    // ── Hilfsmethoden ─────────────────────────────────────────────────────

    private static readonly Mannschaft _a = new() { Id = 1, Name = "Team A" };
    private static readonly Mannschaft _b = new() { Id = 2, Name = "Team B" };
    private static readonly TurnierRunde _runde = new() { Id = 1, Name = "Runde" };

    private static readonly Dictionary<int, PunkteRegel> _dreiPunkte = new()
    {
        [1] = new PunkteRegel { PunkteFuerSieg = 3, PunkteFuerUnentschieden = 1, PunkteFuerNiederlage = 0 }
    };

    /// <summary>Spiel A gegen B; <paramref name="tore"/> = (Minute, für A?) der Torereignisse.</summary>
    private static Spiel Spiel(int toreA, int toreB, (int Minute, bool FuerA)[] tore,
        int? verlaengerungA = null, int? verlaengerungB = null, bool mitSonstigemEreignis = false)
    {
        var ereignisse = tore
            .Select(t => new SpielEreignis { Minute = t.Minute, EreignisArt = EreignisArt.Tor, MannschaftId = t.FuerA ? _a.Id : _b.Id })
            .ToList();
        if (mitSonstigemEreignis)
            ereignisse.Add(new SpielEreignis { Minute = 30, EreignisArt = EreignisArt.Grosschance, MannschaftId = _a.Id });

        return new Spiel
        {
            TurnierRundeId = _runde.Id, TurnierRunde = _runde,
            HeimMannschaftId = _a.Id, HeimMannschaft = _a,
            AuswaertsMannschaftId = _b.Id, AuswaertsMannschaft = _b,
            Status = SpielStatus.Abgeschlossen,
            Ergebnis = new SpielErgebnis
            {
                HeimToreRegulaer = toreA - (verlaengerungA ?? 0), AuswaertsToreRegulaer = toreB - (verlaengerungB ?? 0),
                HeimToreVerlaengerung = verlaengerungA, AuswaertsToreVerlaengerung = verlaengerungB
            },
            Ereignisse = ereignisse
        };
    }

    private static IReadOnlyList<MannschaftsSpiel> SpieleVonA(params Spiel[] spiele)
        => [.. StatistikBerechnung.AusMannschaftsSicht(spiele, _dreiPunkte).Where(s => s.Mannschaft.Id == _a.Id)];

    // ── Platz ─────────────────────────────────────────────────────────────

    [Fact]
    public void Platz_HoeherIstBesser_ZaehltBessereWerte()
        => MannschaftsStatistikBerechnung.Platz(5, [9, 7, 5, 3], hoeherIstBesser: true).Should().Be(3);

    [Fact]
    public void Platz_NiedrigerIstBesser_ZaehltBessereWerte()
        => MannschaftsStatistikBerechnung.Platz(5, [9, 7, 5, 3], hoeherIstBesser: false).Should().Be(2);

    [Fact]
    public void Platz_GleicheWerte_TeilenSichDenPlatz()
    {
        double[] werte = [8, 8, 6];
        MannschaftsStatistikBerechnung.Platz(8, werte, hoeherIstBesser: true).Should().Be(1);
        MannschaftsStatistikBerechnung.Platz(6, werte, hoeherIstBesser: true).Should().Be(3);
    }

    // ── Torabschnitte ─────────────────────────────────────────────────────

    [Fact]
    public void Torabschnitte_OrdnetToreUndGegentoreDenAbschnittenZu()
    {
        var spiele = SpieleVonA(Spiel(2, 1, [(15, true), (16, false), (90, true)]));

        var auswertung = MannschaftsStatistikBerechnung.Torabschnitte(spiele);

        auswertung.Abschnitte.Should().HaveCount(6);
        auswertung.Abschnitte[0].Should().Be(new Torabschnitt("1.–15. Minute", 1, 0));
        auswertung.Abschnitte[1].Should().Be(new Torabschnitt("16.–30. Minute", 0, 1));
        auswertung.Abschnitte[5].Should().Be(new Torabschnitt("76.–90. Minute", 1, 0));
        auswertung.SpieleMitVerlauf.Should().Be(1);
    }

    [Fact]
    public void Torabschnitte_SpielOhneEreignisse_ZaehltNicht()
    {
        var spiele = SpieleVonA(Spiel(2, 0, []), Spiel(1, 0, [(40, true)]));

        var auswertung = MannschaftsStatistikBerechnung.Torabschnitte(spiele);

        auswertung.SpieleMitVerlauf.Should().Be(1);
        auswertung.Spiele.Should().Be(2);
        auswertung.Abschnitte.Sum(a => a.Tore).Should().Be(1);
    }

    [Fact]
    public void Torabschnitte_NullNullMitSpielverlauf_ZaehltMit()
    {
        var spiele = SpieleVonA(Spiel(0, 0, [], mitSonstigemEreignis: true));

        MannschaftsStatistikBerechnung.Torabschnitte(spiele).SpieleMitVerlauf.Should().Be(1);
    }

    [Fact]
    public void Torabschnitte_UnvollstaendigeTorereignisse_ZaehltNicht()
    {
        var spiele = SpieleVonA(Spiel(3, 0, [(10, true)]));

        MannschaftsStatistikBerechnung.Torabschnitte(spiele).SpieleMitVerlauf.Should().Be(0);
    }

    [Fact]
    public void Torabschnitte_Verlaengerung_ErscheintNurWennGespielt()
    {
        var ohne = MannschaftsStatistikBerechnung.Torabschnitte(SpieleVonA(Spiel(1, 0, [(5, true)])));
        var mit = MannschaftsStatistikBerechnung.Torabschnitte(
            SpieleVonA(Spiel(2, 1, [(5, true), (60, false), (105, true)], verlaengerungA: 1, verlaengerungB: 0)));

        ohne.Abschnitte.Should().NotContain(a => a.Bezeichnung == "Verlängerung");
        mit.Abschnitte[^1].Should().Be(new Torabschnitt("Verlängerung", 1, 0));
    }
}
