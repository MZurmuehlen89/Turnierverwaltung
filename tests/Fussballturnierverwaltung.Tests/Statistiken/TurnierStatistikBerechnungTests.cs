using FluentAssertions;
using Fussballturnierverwaltung.Application.Statistiken;
using Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Tests.Statistiken;

public class TurnierStatistikBerechnungTests
{
    // ── Hilfsmethoden ─────────────────────────────────────────────────────

    // Gesamtrating: A 80, B 70, C 60.
    private static readonly Mannschaft _a = new() { Id = 1, Name = "Team A", OffensivRating = 80, MittelfeldRating = 80, DefensivRating = 80 };
    private static readonly Mannschaft _b = new() { Id = 2, Name = "Team B", OffensivRating = 70, MittelfeldRating = 70, DefensivRating = 70 };
    private static readonly Mannschaft _c = new() { Id = 3, Name = "Team C", OffensivRating = 60, MittelfeldRating = 60, DefensivRating = 60 };

    private static readonly TurnierRunde _gruppen = new() { Id = 1, Name = "Gruppenphase" };
    private static readonly TurnierRunde _finale = new() { Id = 2, Name = "Finale", NeutralerSpielort = true };

    private static readonly PunkteRegel _dreiPunkte = new() { PunkteFuerSieg = 3, PunkteFuerUnentschieden = 1, PunkteFuerNiederlage = 0 };
    private static readonly Dictionary<int, PunkteRegel> _punkte = new() { [1] = _dreiPunkte, [2] = _dreiPunkte };

    private static Spiel Spiel(Mannschaft heim, Mannschaft auswaerts, int heimTore, int auswaertsTore,
        TurnierRunde? runde = null, EntscheidungArt entscheidung = EntscheidungArt.RegulaereSpielzeit)
    {
        runde ??= _gruppen;
        return new Spiel
        {
            TurnierRundeId = runde.Id, TurnierRunde = runde,
            HeimMannschaftId = heim.Id, HeimMannschaft = heim,
            AuswaertsMannschaftId = auswaerts.Id, AuswaertsMannschaft = auswaerts,
            Status = SpielStatus.Abgeschlossen,
            Ergebnis = new SpielErgebnis
            {
                HeimToreRegulaer = heimTore, AuswaertsToreRegulaer = auswaertsTore, EntscheidungArt = entscheidung
            }
        };
    }

    private static IReadOnlyList<MannschaftsSpiel> Sicht(params Spiel[] spiele)
        => StatistikBerechnung.AusMannschaftsSicht(spiele, _punkte);

    // ── Tore je Runde ─────────────────────────────────────────────────────

    [Fact]
    public void ToreJeRunde_ZaehltJedesSpielEinmal()
    {
        var spiele = Sicht(Spiel(_a, _b, 2, 1), Spiel(_b, _c, 0, 0), Spiel(_a, _c, 3, 1, _finale));

        var runden = TurnierStatistikBerechnung.ToreJeRunde(spiele);

        runden.Should().Equal(new RundenTore("Gruppenphase", 2, 3), new RundenTore("Finale", 1, 4));
        runden[0].ToreProSpiel.Should().Be(1.5);
    }

    // ── Ausgänge ──────────────────────────────────────────────────────────

    [Fact]
    public void Ausgaenge_ZaehltNeutraleSpieleNurGetrennt()
    {
        var spiele = Sicht(Spiel(_a, _b, 2, 1), Spiel(_b, _c, 1, 1), Spiel(_c, _a, 0, 1), Spiel(_a, _c, 3, 1, _finale));

        TurnierStatistikBerechnung.Ausgaenge(spiele).Should().Be(new Ausgangsverteilung(1, 1, 1, 1));
    }

    // ── Entscheidungen ────────────────────────────────────────────────────

    [Fact]
    public void Entscheidungen_ZeigtDieDreiHauptartenImmer_UndWeitereNurWennVorgekommen()
    {
        var spiele = Sicht(
            Spiel(_a, _b, 2, 1),
            Spiel(_b, _c, 1, 1, entscheidung: EntscheidungArt.Elfmeterschiessen),
            Spiel(_c, _a, 1, 1, entscheidung: EntscheidungArt.Auswaertstorregel));

        TurnierStatistikBerechnung.Entscheidungen(spiele).Should().Equal(
            new EntscheidungsAnzahl(EntscheidungArt.RegulaereSpielzeit, 1),
            new EntscheidungsAnzahl(EntscheidungArt.Verlaengerung, 0),
            new EntscheidungsAnzahl(EntscheidungArt.Elfmeterschiessen, 1),
            new EntscheidungsAnzahl(EntscheidungArt.Auswaertstorregel, 1));
    }

    // ── Bestwerte ─────────────────────────────────────────────────────────

    [Fact]
    public void HoechsteSiege_SortiertNachDifferenzDannToren_AusSichtDesSiegers()
    {
        var spiele = Sicht(Spiel(_a, _b, 3, 1), Spiel(_b, _c, 0, 3), Spiel(_c, _a, 4, 2), Spiel(_a, _c, 1, 1));

        var siege = TurnierStatistikBerechnung.HoechsteSiege(spiele, 2);

        siege.Select(s => (s.Mannschaft.Id, s.Tore, s.Gegentore)).Should().Equal((3, 3, 0), (3, 4, 2));
    }

    [Fact]
    public void TorreichsteSpiele_NimmtJedesSpielEinmal_OhneTorloseSpiele()
    {
        var spiele = Sicht(Spiel(_a, _b, 3, 3), Spiel(_b, _c, 0, 0), Spiel(_c, _a, 1, 4));

        var torreich = TurnierStatistikBerechnung.TorreichsteSpiele(spiele, 5);

        torreich.Select(s => s.Tore + s.Gegentore).Should().Equal(6, 5);
        torreich.Should().OnlyContain(s => s.IstHeim);
    }

    [Fact]
    public void HaeufigsteErgebnisse_ZaehltErgebnisUnabhaengigVonHeimUndAuswaerts()
    {
        var spiele = Sicht(Spiel(_a, _b, 2, 1), Spiel(_b, _c, 1, 2), Spiel(_c, _a, 0, 0), Spiel(_a, _c, 1, 1), Spiel(_b, _a, 3, 0));

        TurnierStatistikBerechnung.HaeufigsteErgebnisse(spiele, 3).Should().Equal(
            new ErgebnisHaeufigkeit(2, 1, 2),
            new ErgebnisHaeufigkeit(0, 0, 1),
            new ErgebnisHaeufigkeit(1, 1, 1));
    }

    [Fact]
    public void Ueberraschungen_NurSiegeAbMindestabstand_GroessterAbstandZuerst()
    {
        var spiele = Sicht(
            Spiel(_b, _a, 1, 0),   // B schlägt A: 10 Punkte Abstand
            Spiel(_c, _a, 2, 1),   // C schlägt A: 20 Punkte
            Spiel(_a, _c, 0, 0),   // Unentschieden zählt nicht
            Spiel(_c, _b, 1, 1, entscheidung: EntscheidungArt.Elfmeterschiessen), // Elfmeterschießen ist kein Sieg
            Spiel(_a, _b, 0, 1));  // B schlägt A erneut

        var ueberraschungen = TurnierStatistikBerechnung.Ueberraschungen(spiele, 2);

        ueberraschungen.Select(u => (u.Sieger.Mannschaft.Id, u.RatingAbstand)).Should().Equal((3, 20m), (2, 10m));
    }

    [Fact]
    public void Ueberraschungen_KnappUnterMindestabstand_ZaehltNicht()
    {
        var knapp = new Mannschaft { Id = 4, Name = "Knapp", OffensivRating = 76, MittelfeldRating = 76, DefensivRating = 76 };

        TurnierStatistikBerechnung.Ueberraschungen(Sicht(Spiel(knapp, _a, 1, 0)), 5).Should().BeEmpty();
    }

    // ── Torabschnitte ─────────────────────────────────────────────────────

    [Fact]
    public void Torabschnitte_ZaehltToreBeiderMannschaftenEinmal()
    {
        var spiel = Spiel(_a, _b, 1, 1);
        spiel.Ereignisse =
        [
            new SpielEreignis { Minute = 10, EreignisArt = EreignisArt.Tor, MannschaftId = _a.Id },
            new SpielEreignis { Minute = 80, EreignisArt = EreignisArt.Tor, MannschaftId = _b.Id }
        ];

        var auswertung = TurnierStatistikBerechnung.Torabschnitte(Sicht(spiel));

        auswertung.SpieleMitVerlauf.Should().Be(1);
        auswertung.Abschnitte.Sum(a => a.Tore + a.Gegentore).Should().Be(2);
        auswertung.Abschnitte[0].Tore.Should().Be(1);
        auswertung.Abschnitte[5].Gegentore.Should().Be(1);
    }
}
