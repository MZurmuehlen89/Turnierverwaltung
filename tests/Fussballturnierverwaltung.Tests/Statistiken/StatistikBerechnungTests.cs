using FluentAssertions;
using Fussballturnierverwaltung.Application.Statistiken;
using Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Tests.Statistiken;

public class StatistikBerechnungTests
{
    // ── Hilfsmethoden ─────────────────────────────────────────────────────

    private const int LIGA = 1;
    private const int FINALE = 2;

    private static readonly Verband _uefa = new() { Id = 1, Name = "UEFA" };
    private static readonly Verband _conmebol = new() { Id = 2, Name = "CONMEBOL" };
    private static readonly Land _deutschland = new() { Id = 1, Name = "Deutschland", Verband = _uefa };
    private static readonly Land _spanien = new() { Id = 2, Name = "Spanien", Verband = _uefa };
    private static readonly Land _brasilien = new() { Id = 3, Name = "Brasilien", Verband = _conmebol };

    private static readonly Mannschaft _a = new() { Id = 1, Name = "Team A", Land = _deutschland };
    private static readonly Mannschaft _b = new() { Id = 2, Name = "Team B", Land = _deutschland };
    private static readonly Mannschaft _c = new() { Id = 3, Name = "Team C", Land = _spanien };

    private static readonly TurnierRunde _liga = new() { Id = LIGA, Name = "Liga", Reihenfolge = 1 };
    private static readonly TurnierRunde _finale = new() { Id = FINALE, Name = "Finale", Reihenfolge = 2, NeutralerSpielort = true };

    private static readonly Dictionary<int, PunkteRegel> _dreiPunkte = new()
    {
        [LIGA]   = new PunkteRegel { PunkteFuerSieg = 3, PunkteFuerUnentschieden = 1, PunkteFuerNiederlage = 0 },
        [FINALE] = new PunkteRegel { PunkteFuerSieg = 3, PunkteFuerUnentschieden = 1, PunkteFuerNiederlage = 0 }
    };

    private static Spiel Spiel(
        Mannschaft heim, Mannschaft auswaerts, int heimTore, int auswaertsTore,
        (int Heim, int Auswaerts)? halbzeit = null, TurnierRunde? runde = null)
    {
        runde ??= _liga;
        return new Spiel
        {
            TurnierRundeId = runde.Id, TurnierRunde = runde,
            HeimMannschaftId = heim.Id, HeimMannschaft = heim,
            AuswaertsMannschaftId = auswaerts.Id, AuswaertsMannschaft = auswaerts,
            Status = SpielStatus.Abgeschlossen,
            Ergebnis = new SpielErgebnis
            {
                HeimToreRegulaer = heimTore, AuswaertsToreRegulaer = auswaertsTore,
                HalbzeitHeimTore = halbzeit?.Heim, HalbzeitAuswaertsTore = halbzeit?.Auswaerts
            }
        };
    }

    private static IReadOnlyList<MannschaftsSpiel> Sicht(params Spiel[] spiele)
        => StatistikBerechnung.AusMannschaftsSicht(spiele, _dreiPunkte);

    private static Bilanz BilanzVon(IReadOnlyList<BilanzEintrag> rangliste, Mannschaft mannschaft)
        => rangliste.Single(e => e.Mannschaft.Id == mannschaft.Id).Bilanz;

    // ── Mannschaftssicht ──────────────────────────────────────────────────

    [Fact]
    public void AusMannschaftsSicht_OffenesSpiel_WirdUebersprungen()
    {
        var offen = Spiel(_a, _b, 0, 0);
        offen.Status = SpielStatus.Geplant;
        offen.Ergebnis = null;

        Sicht(offen).Should().BeEmpty();
    }

    [Fact]
    public void AusMannschaftsSicht_Elfmeterschiessen_ZaehltAlsUnentschieden()
    {
        var spiel = Spiel(_a, _b, 1, 1);
        spiel.Ergebnis!.HeimElfmetertore = 5;
        spiel.Ergebnis.AuswaertsElfmetertore = 4;
        spiel.Ergebnis.EntscheidungArt = EntscheidungArt.Elfmeterschiessen;
        spiel.Ergebnis.SiegerMannschaftId = _a.Id;

        Sicht(spiel).Should().OnlyContain(s => s.Ausgang == SpielAusgang.Unentschieden);
    }

    [Fact]
    public void AusMannschaftsSicht_SiegNachVerlaengerung_ZaehltAlsSieg()
    {
        var spiel = Spiel(_a, _b, 1, 1);
        spiel.Ergebnis!.HeimToreVerlaengerung = 1;
        spiel.Ergebnis.AuswaertsToreVerlaengerung = 0;

        var sicht = Sicht(spiel);

        sicht.Single(s => s.Mannschaft.Id == _a.Id).Ausgang.Should().Be(SpielAusgang.Sieg);
        sicht.Single(s => s.Mannschaft.Id == _a.Id).Tore.Should().Be(2);
    }

    [Fact]
    public void AusMannschaftsSicht_KoRueckspiel_WertetDasSpielNichtDieGesamtwertung()
    {
        // Rückspiel 1:0 für B, in der Gesamtwertung kommt A weiter — der gespeicherte Sieger ist A.
        var rueckspiel = Spiel(_b, _a, 1, 0);
        rueckspiel.IstRueckspiel = true;
        rueckspiel.Ergebnis!.SiegerMannschaftId = _a.Id;

        var sicht = Sicht(rueckspiel);

        sicht.Single(s => s.Mannschaft.Id == _b.Id).Ausgang.Should().Be(SpielAusgang.Sieg);
        sicht.Single(s => s.Mannschaft.Id == _a.Id).Ausgang.Should().Be(SpielAusgang.Niederlage);
    }

    [Fact]
    public void AusMannschaftsSicht_PunkteNachRegelDerRunde()
    {
        var regeln = new Dictionary<int, PunkteRegel>
        {
            [LIGA]   = new() { PunkteFuerSieg = 2, PunkteFuerUnentschieden = 1, PunkteFuerNiederlage = 0 },
            [FINALE] = new() { PunkteFuerSieg = 3, PunkteFuerUnentschieden = 1, PunkteFuerNiederlage = 0 }
        };
        var spiele = new[] { Spiel(_a, _b, 1, 0), Spiel(_a, _b, 2, 0, runde: _finale) };

        var rangliste = StatistikBerechnung.BilanzRangliste(StatistikBerechnung.AusMannschaftsSicht(spiele, regeln));

        BilanzVon(rangliste, _a).Punkte.Should().Be(5);
    }

    // ── Bilanz ────────────────────────────────────────────────────────────

    [Fact]
    public void BilanzRangliste_ZaehltSpieleToreUndPunkte()
    {
        var rangliste = StatistikBerechnung.BilanzRangliste(Sicht(
            Spiel(_a, _b, 2, 1),
            Spiel(_b, _a, 0, 0),
            Spiel(_c, _a, 3, 1)));

        BilanzVon(rangliste, _a).Should().Be(new Bilanz(3, 1, 1, 1, 3, 4, 4));
        BilanzVon(rangliste, _c).Should().Be(new Bilanz(1, 1, 0, 0, 3, 1, 3));
    }

    [Fact]
    public void BilanzRangliste_SortiertNachPunktenDannTordifferenz()
    {
        var rangliste = StatistikBerechnung.BilanzRangliste(Sicht(
            Spiel(_a, _b, 1, 0),
            Spiel(_c, _b, 4, 0)));

        rangliste.Select(e => e.Mannschaft.Name).Should().Equal("Team C", "Team A", "Team B");
    }

    [Fact]
    public void BilanzRangliste_HeimUndAuswaerts_TrennenNachSpielort()
    {
        var sicht = Sicht(Spiel(_a, _b, 2, 0), Spiel(_b, _a, 1, 1));

        BilanzVon(StatistikBerechnung.BilanzRangliste(sicht, SpielortFilter.Heim), _a)
            .Should().Be(new Bilanz(1, 1, 0, 0, 2, 0, 3));
        BilanzVon(StatistikBerechnung.BilanzRangliste(sicht, SpielortFilter.Auswaerts), _a)
            .Should().Be(new Bilanz(1, 0, 1, 0, 1, 1, 1));
    }

    [Fact]
    public void BilanzRangliste_NeutralerSpielort_FehltInHeimUndAuswaerts()
    {
        var sicht = Sicht(Spiel(_a, _b, 2, 0, runde: _finale));

        StatistikBerechnung.BilanzRangliste(sicht).Should().HaveCount(2);
        StatistikBerechnung.BilanzRangliste(sicht, SpielortFilter.Heim).Should().BeEmpty();
        StatistikBerechnung.BilanzRangliste(sicht, SpielortFilter.Auswaerts).Should().BeEmpty();
        StatistikBerechnung.HatHeimUndAuswaertsSpiele(sicht).Should().BeFalse();
    }

    // ── Form ──────────────────────────────────────────────────────────────

    [Fact]
    public void Formtabelle_NimmtNurDieLetztenSpieleInSpielreihenfolge()
    {
        var formtabelle = StatistikBerechnung.Formtabelle(
            Sicht(Spiel(_a, _b, 0, 1), Spiel(_a, _b, 1, 1), Spiel(_a, _b, 2, 0)), letzte: 2);

        var form = formtabelle.Single(e => e.Mannschaft.Id == _a.Id);
        form.LetzteSpiele.Should().Equal(SpielAusgang.Unentschieden, SpielAusgang.Sieg);
        form.Punkte.Should().Be(4);
    }

    // ── Halbzeit ──────────────────────────────────────────────────────────

    [Fact]
    public void Halbzeittabelle_WertetPausenstandUndZaehltGedrehteSpiele()
    {
        var tabelle = StatistikBerechnung.Halbzeittabelle(Sicht(
            Spiel(_a, _b, 2, 1, halbzeit: (0, 1)),
            Spiel(_a, _c, 1, 1, halbzeit: (1, 0))));

        var a = tabelle.Single(e => e.Mannschaft.Id == _a.Id);
        a.Bilanz.Should().Be(new Bilanz(2, 1, 0, 1, 1, 1, 3));
        a.Gedreht.Should().Be(1);
        a.Verspielt.Should().Be(0);
        tabelle.Single(e => e.Mannschaft.Id == _b.Id).Verspielt.Should().Be(1);
    }

    [Fact]
    public void Halbzeittabelle_SpielOhnePausenstand_ZaehltNicht()
    {
        StatistikBerechnung.Halbzeittabelle(Sicht(Spiel(_a, _b, 2, 1))).Should().BeEmpty();
    }

    // ── Serien ────────────────────────────────────────────────────────────

    [Fact]
    public void Serien_LaengsteUndAktuelleSerie()
    {
        var serien = StatistikBerechnung.Serien(Sicht(
            Spiel(_a, _b, 1, 0),
            Spiel(_a, _b, 2, 0),
            Spiel(_a, _b, 1, 1),
            Spiel(_a, _b, 0, 1),
            Spiel(_a, _b, 3, 0)));

        var a = serien.Single(e => e.Mannschaft.Id == _a.Id);
        a.Siege.Should().Be(new Serie(2, 1));
        a.Ungeschlagen.Should().Be(new Serie(3, 1));
        a.OhneGegentor.Should().Be(new Serie(2, 1));
    }

    // ── Tore und Zu-Null ──────────────────────────────────────────────────

    [Fact]
    public void ToreUndZuNull_ZaehltZuNullSpieleUndSchnitt()
    {
        var tore = StatistikBerechnung.ToreUndZuNull(Sicht(Spiel(_a, _b, 3, 0), Spiel(_b, _a, 2, 0)));

        var a = tore.Single(e => e.Mannschaft.Id == _a.Id);
        a.ZuNullSpiele.Should().Be(1);
        a.SpieleOhneEigenesTor.Should().Be(1);
        a.ToreProSpiel.Should().Be(1.5);
        a.GegentoreProSpiel.Should().Be(1.0);
    }

    // ── Fairplay ──────────────────────────────────────────────────────────

    [Fact]
    public void Fairplay_ZaehltKartenJeMannschaft_FairsteZuerst()
    {
        var spiel = Spiel(_a, _b, 1, 0);
        spiel.Ereignisse =
        [
            new SpielEreignis { MannschaftId = _a.Id, EreignisArt = EreignisArt.GelbeKarte },
            new SpielEreignis { MannschaftId = _a.Id, EreignisArt = EreignisArt.RoteKarte },
            new SpielEreignis { MannschaftId = _b.Id, EreignisArt = EreignisArt.GelbeKarte },
            new SpielEreignis { MannschaftId = _a.Id, EreignisArt = EreignisArt.Tor }
        ];

        var fairplay = StatistikBerechnung.Fairplay(Sicht(spiel));

        fairplay.Select(e => e.Mannschaft.Name).Should().Equal("Team B", "Team A");
        fairplay[1].Should().Be(new FairplayEintrag(_a, 1, 1, 1));
        fairplay[1].Wertung.Should().Be(4);
    }

    // ── Verbände ──────────────────────────────────────────────────────────

    [Fact]
    public void Verbaende_Vereine_WerdenJeLandZusammengefasst()
    {
        var verbaende = StatistikBerechnung.Verbaende(Sicht(Spiel(_a, _c, 2, 0), Spiel(_b, _c, 1, 0)));

        var deutschland = verbaende.Single(e => e.Name == "Deutschland");
        deutschland.Mannschaften.Should().Be(2);
        deutschland.Bilanz.Should().Be(new Bilanz(2, 2, 0, 0, 3, 0, 6));
        verbaende.Single(e => e.Name == "Spanien").Mannschaften.Should().Be(1);
    }

    [Fact]
    public void Verbaende_Nationalmannschaften_WerdenJeVerbandZusammengefasst()
    {
        var deutschland = new Mannschaft { Id = 10, Name = "Deutschland", Land = _deutschland, Mannschaftsart = Mannschaftsart.Nationalmannschaft };
        var spanien = new Mannschaft { Id = 11, Name = "Spanien", Land = _spanien, Mannschaftsart = Mannschaftsart.Nationalmannschaft };
        var brasilien = new Mannschaft { Id = 12, Name = "Brasilien", Land = _brasilien, Mannschaftsart = Mannschaftsart.Nationalmannschaft };

        var verbaende = StatistikBerechnung.Verbaende(Sicht(Spiel(deutschland, brasilien, 1, 0), Spiel(spanien, brasilien, 0, 0)));

        verbaende.Select(e => e.Name).Should().Equal("UEFA", "CONMEBOL");
        verbaende[0].Mannschaften.Should().Be(2);
        verbaende[0].Bilanz.Punkte.Should().Be(4);
    }
}
