using FluentAssertions;
using Fussballturnierverwaltung.Application.Ranglisten;
using Fussballturnierverwaltung.Application.Simulation;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Tests.Simulation;

/// <summary>Charakterisierung des Spielkopfs (#509 Schnitt B, K2): Score, Entscheidungstext, Zwischenstände, Kürzel,
/// Gesamtkopf – exakte Werte, am unverändert verschobenen Code festgehalten. Beim Aufräumen bleiben die Erwartungen
/// stehen, nur <see cref="Kopf"/> und <see cref="Gesamt"/> ändern sich.</summary>
public class SpielKopfTests
{
    private static readonly UntertitelQuellen _quellen = new(m => m.Land?.Verband?.Kurzname, Ligazugehoerigkeit.Keine);

    private static readonly Mannschaft _alpha = new() { Name = "Alpha", Kurzname = "ALP", Wappen = [7] };
    private static readonly Mannschaft _beta = new() { Name = "Beta Berlin" };

    private static SpielKopf Kopf(
        SimulationsErgebnis e, string? kontext = null, Mannschaft? heim = null, string heimName = "Alpha",
        Mannschaft? auswaerts = null, string auswaertsName = "Beta Berlin")
        => SpielKopf.Aus(kontext, new SpielSeite(heim ?? _alpha, heimName), new SpielSeite(auswaerts ?? _beta, auswaertsName), e, _quellen);

    private static SpielKopf Gesamt(Mannschaft heim, Mannschaft auswaerts, string score, string zusatz)
        => SpielKopf.Gesamt(SpielSeite.Von(heim), SpielSeite.Von(auswaerts), score, zusatz, _quellen);

    private static SimulationsErgebnis Ergebnis(
        int heim, int auswaerts, EntscheidungArt art = EntscheidungArt.RegulaereSpielzeit,
        (int Heim, int Auswaerts)? verlaengerung = null, (int Heim, int Auswaerts)? elfmeter = null,
        (int Heim, int Auswaerts)? halbzeit = null, bool? losGewinnerHeim = null, Wiederholungsergebnis? wiederholung = null)
        => new(heim, auswaerts, verlaengerung?.Heim, verlaengerung?.Auswaerts, elfmeter?.Heim, elfmeter?.Auswaerts,
            art, [], halbzeit?.Heim, halbzeit?.Auswaerts, losGewinnerHeim, wiederholung);

    // ── Score, Entscheidung, Zwischenstände ──────────────────────────────

    [Fact]
    public void NachRegulaererSpielzeit_ScoreMitHalbzeitstand_OhneEntscheidungstext()
    {
        var kopf = Kopf(Ergebnis(2, 1, halbzeit: (1, 0)));

        kopf.Score.Should().Be("2:1");
        kopf.Entscheidung.Should().BeNull();
        kopf.Zwischenstaende.Should().Be("HZ 1:0");
    }

    [Fact]
    public void OhneHalbzeitstand_KeineZwischenstaende()
    {
        var kopf = Kopf(Ergebnis(2, 1));

        kopf.Score.Should().Be("2:1");
        kopf.Zwischenstaende.Should().BeNull();
    }

    [Fact]
    public void NachVerlaengerung_ScoreIstGesamtstand_ZwischenstaendeNennen90MinutenUndVerlaengerung()
    {
        var kopf = Kopf(Ergebnis(1, 1, EntscheidungArt.Verlaengerung, verlaengerung: (1, 0), halbzeit: (0, 1)));

        kopf.Score.Should().Be("2:1");
        kopf.Entscheidung.Should().Be("nach Verlängerung");
        kopf.Zwischenstaende.Should().Be("HZ 0:1 · 90' 1:1 · n.V. 2:1");
    }

    [Fact]
    public void ImElfmeterschiessen_ElfmeterstandGross_ErgebnisNachVerlaengerungInDenZwischenstaenden()
    {
        var kopf = Kopf(Ergebnis(1, 1, EntscheidungArt.Elfmeterschiessen,
            verlaengerung: (0, 0), elfmeter: (5, 4), halbzeit: (1, 0)));

        kopf.Score.Should().Be("5:4");
        kopf.Entscheidung.Should().Be("im Elfmeterschießen");
        kopf.Zwischenstaende.Should().Be("HZ 1:0 · 90' 1:1 · n.V. 1:1");
    }

    [Fact]
    public void ImElfmeterschiessenOhneVerlaengerungsfelder_NennenNur90Minuten()
    {
        var kopf = Kopf(Ergebnis(0, 0, EntscheidungArt.Elfmeterschiessen, elfmeter: (3, 2)));

        kopf.Score.Should().Be("3:2");
        kopf.Zwischenstaende.Should().Be("90' 0:0");
    }

    [Theory]
    [InlineData(EntscheidungArt.GoldenGoal, "nach Golden Goal")]
    [InlineData(EntscheidungArt.SilverGoal, "nach Silver Goal")]
    [InlineData(EntscheidungArt.Auswaertstorregel, "nach Auswärtstorregel")]
    [InlineData(EntscheidungArt.Wiederholungsspiel, "nach Wiederholungsspiel")]
    [InlineData(EntscheidungArt.Losentscheid, "per Losentscheid")]
    [InlineData(EntscheidungArt.Muenzwurf, "per Münzwurf")]
    public void Entscheidungstext_JeArt(EntscheidungArt art, string erwartet)
    {
        Kopf(Ergebnis(1, 1, art)).Entscheidung.Should().Be(erwartet);
    }

    [Theory]
    [InlineData(true, "per Losentscheid für Alpha")]
    [InlineData(false, "per Losentscheid für Beta Berlin")]
    public void Los_NenntDenGewinnerAusgeschrieben(bool heimGewinnt, string erwartet)
    {
        Kopf(Ergebnis(1, 1, EntscheidungArt.Losentscheid, losGewinnerHeim: heimGewinnt))
            .Entscheidung.Should().Be(erwartet);
    }

    [Fact]
    public void LosGewinner_NutztDieUebergebenenNamen_NichtDieMannschaftsnamen()
    {
        var kopf = Kopf(Ergebnis(0, 0, EntscheidungArt.Muenzwurf, losGewinnerHeim: false), auswaertsName: "Sieger Gruppe B");

        kopf.Entscheidung.Should().Be("per Münzwurf für Sieger Gruppe B");
        kopf.AuswaertsName.Should().Be("Sieger Gruppe B");
    }

    [Fact]
    public void Wiederholungsspiel_StehtInDenZwischenstaenden_MitNachVerlaengerungHinweis()
    {
        Kopf(Ergebnis(1, 1, EntscheidungArt.Wiederholungsspiel, halbzeit: (0, 0),
                wiederholung: new Wiederholungsergebnis(2, 1, null, null)))
            .Zwischenstaende.Should().Be("HZ 0:0 · Wdh. 2:1");

        Kopf(Ergebnis(1, 1, EntscheidungArt.Wiederholungsspiel, halbzeit: (0, 0),
                wiederholung: new Wiederholungsergebnis(1, 1, 1, 0)))
            .Zwischenstaende.Should().Be("HZ 0:0 · Wdh. 2:1 n.V.");
    }

    // ── Seiten, Kontext, Kürzel ──────────────────────────────────────────

    [Fact]
    public void KontextUndSeiten_WerdenUebernommen()
    {
        var kopf = Kopf(Ergebnis(0, 0), kontext: "Hinspiel");

        kopf.Kontext.Should().Be("Hinspiel");
        kopf.HeimName.Should().Be("Alpha");
        kopf.AuswaertsName.Should().Be("Beta Berlin");
        kopf.HeimWappen.Should().Equal(7);
        kopf.AuswaertsWappen.Should().BeNull();
        kopf.HeimUntertitel.Should().BeNull();
        kopf.AuswaertsUntertitel.Should().BeNull();
    }

    [Fact]
    public void Kuerzel_Kurzname_sonstDreiBuchstabenDesNamens_auchOhneMannschaft()
    {
        var kopf = Kopf(Ergebnis(0, 0), heim: _alpha, auswaerts: _beta);
        kopf.HeimKuerzel.Should().Be("ALP");
        kopf.AuswaertsKuerzel.Should().Be("BET");

        var platzhalter = SpielKopf.Aus(
            null, new SpielSeite(null, "Sieger Gruppe A"), new SpielSeite(new Mannschaft { Name = "FC", Kurzname = "  " }, "FC"), Ergebnis(0, 0), _quellen);
        platzhalter.HeimKuerzel.Should().Be("SIE");
        platzhalter.AuswaertsKuerzel.Should().Be("FC");
        platzhalter.HeimWappen.Should().BeNull();
        platzhalter.HeimUntertitel.Should().BeNull();
    }

    // ── Gesamtkopf ───────────────────────────────────────────────────────

    [Fact]
    public void Gesamtkopf_SummenergebnisUndZusatz_OhneZwischenstaende()
    {
        var kopf = Gesamt(_alpha, _beta, "3:2", "nach 180 Minuten");

        kopf.Kontext.Should().Be("Gesamt");
        kopf.Score.Should().Be("3:2");
        kopf.Entscheidung.Should().Be("nach 180 Minuten");
        kopf.Zwischenstaende.Should().BeNull();
        kopf.HeimName.Should().Be("Alpha");
        kopf.AuswaertsName.Should().Be("Beta Berlin");
        kopf.HeimKuerzel.Should().Be("ALP");
        kopf.AuswaertsKuerzel.Should().Be("BET");
        kopf.HeimWappen.Should().Equal(7);
    }

    // ── Wiederholungsspiel (#247 K12) ────────────────────────────────────

    [Fact]
    public void Wiederholungsspiel_OhneTausch_HeimteamLinksUndHeimrechtWieImErstenSpiel()
    {
        var kopf = SpielKopf.Wiederholungsspiel(
            SpielSeite.Von(_alpha), SpielSeite.Von(_beta), new Wiederholungsergebnis(2, 1, null, null), getauscht: false, _quellen);

        kopf.Kontext.Should().Be("Wiederholungsspiel");
        kopf.HeimName.Should().Be("Alpha");
        kopf.Score.Should().Be("2:1");
        kopf.Entscheidung.Should().Be("Heimrecht wie im ersten Spiel");
    }

    [Fact]
    public void Wiederholungsspiel_MitTausch_GastteamDesErstenSpielsLinksUndScoreGespiegelt()
    {
        var kopf = SpielKopf.Wiederholungsspiel(
            SpielSeite.Von(_alpha), SpielSeite.Von(_beta), new Wiederholungsergebnis(2, 1, null, null), getauscht: true, _quellen);

        kopf.HeimName.Should().Be("Beta Berlin");
        kopf.AuswaertsName.Should().Be("Alpha");
        kopf.Score.Should().Be("1:2");
        kopf.Entscheidung.Should().Be("Heimrecht getauscht");
    }

    [Fact]
    public void Wiederholungsspiel_MitVerlaengerung_ScoreGesamtUndZwischenstand()
    {
        var kopf = SpielKopf.Wiederholungsspiel(
            SpielSeite.Von(_alpha), SpielSeite.Von(_beta), new Wiederholungsergebnis(1, 1, 1, 0), getauscht: true, _quellen);

        kopf.Score.Should().Be("1:2");
        kopf.Zwischenstaende.Should().Be("90' 1:1 · n.V. 1:2");
    }
}
