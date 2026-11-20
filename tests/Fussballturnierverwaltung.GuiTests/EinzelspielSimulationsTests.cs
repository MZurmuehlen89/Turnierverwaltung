using FluentAssertions;
using Fussballturnierverwaltung.GuiTests.Seiten;
using GuiTest.Framework;

namespace Fussballturnierverwaltung.GuiTests;

/// <summary>Freie Einzelspiel-Simulation (#536): ein Einzelspiel sowie Hin- und Rückspiel zweier Seed-Mannschaften.
/// Die Simulation speichert nichts, der Seed bleibt also unverändert. Geprüft werden nur Invarianten – Gesamtstand
/// ist die Summe beider Spiele, der Sieger folgt aus Gesamtstand bzw. Elfmeterschießen –, keine festen Ergebnisse.
/// Die Auswärtstorregel (#539) ist eine Checkbox, die nur mit „Mit Rückspiel“ erscheint; sie entscheidet bei
/// ausgeglichenem Gesamtstand vor dem Regelwerk und zählt nur die Tore der regulären Spielzeit.</summary>
public sealed class EinzelspielSimulationsTests(TurnierverwaltungApp app) : IClassFixture<TurnierverwaltungApp>, IDisposable
{
    private const string HEIM = "Deutschland";
    private const string AUSWAERTS = "Frankreich";
    // Deutlich schwächer als HEIM: Mit festem Seed zieht jede Simulation dieselbe Zufallsfolge, gleich starke Teams
    // spielen dann stets gleich (Deutschland gegen Frankreich, Spanien, Italien, England, … im Rückspiel je 1:1)
    private static readonly string[] SCHWACHE_GEGNER = ["San Marino", "Gibraltar", "Andorra", "Liechtenstein", "Färöer"];
    private const string MIT_VERLAENGERUNG = "FIFA Standard Modern (3-Punkte)";
    private const string DIREKTES_ELFMETERSCHIESSEN = "CONMEBOL Copa América (direktes Elfmeterschießen)";

    // GleichstandHerbeifuehren probiert je Gegner die Hinspiel-Differenzen der Reihe nach. Ohne Auswärtstorregel
    // kennt das Rückspiel vom Hinspiel nur die Tordifferenz, und mit festem Seed beginnt jedes Rückspiel mit derselben
    // Zufallsfolge – je Paarung gibt es also nur so viele verschiedene Rückspiele wie Differenzen (CI-Lauf nach #673:
    // 16 Hinspiele gegen Frankreich, kein Gleichstand). Erst ein Gegner anderer Stärke bringt einen anderen
    // Spielverlauf – gleich starke spielen gleich (siehe SCHWACHE_GEGNER), darum in Stufen von 3–4 Rating-Punkten
    // (Seed: Frankreich 88, England 84, Niederlande 82, Dänemark 78, Serbien 74, Wales 71, Island 67; Deutschland 83).
    private static readonly string[] GESTAFFELTE_GEGNER =
        [AUSWAERTS, "England", "Niederlande", "Dänemark", "Serbien", "Wales", "Island"];
    private static readonly int[] HINSPIEL_DIFFERENZEN = [0, 1, -1, 2, -2];

    private readonly EinzelspielSeite _seite = app.Hauptfenster.EinzelspielSimulationOeffnen();

    [Fact]
    public void Einzelspiel_ZeigtDasErgebnisDerGewaehltenMannschaften()
    {
        _seite.MannschaftenWaehlen(HEIM, AUSWAERTS);

        var kopf = _seite.EinzelspielSimulieren();

        kopf.Should().Match<SpielKopfAnzeige>(k => k.Heim == HEIM && k.Auswaerts == AUSWAERTS);
        Spielstand.Tore(kopf.Score).Should().NotBeNull("ein Ergebnis wird angezeigt");
        kopf.Entscheidung.Should().BeEmpty("ohne Regelwerk gibt es keine Verlängerung");
        _seite.Fehlermeldung.Should().BeEmpty();
    }

    /// <summary>Der Untertitel unter dem Teamnamen folgt dem Kontext der Begegnung (#341, #384): Vereine desselben Landes
    /// zeigen ihre Liga aus der Ligapyramide, Vereine desselben Kontinentalverbands das Land, Vereine verschiedener
    /// Verbände Verband und Land, Nationalmannschaften den Verband.</summary>
    [Theory]
    [InlineData("Bayern München", "Schalke 04", "Bundesliga", "2. Bundesliga")]
    [InlineData("Bayern München", "Real Madrid", "Deutschland", "Spanien")]
    [InlineData("Bayern München", "Flamengo", "UEFA · Deutschland", "CONMEBOL · Brasilien")]
    [InlineData(HEIM, AUSWAERTS, "UEFA", "UEFA")]
    public void Einzelspiel_UntertitelFolgtDemKontextDerBegegnung(
        string heim, string auswaerts, string heimUntertitel, string auswaertsUntertitel)
    {
        _seite.MannschaftenWaehlen(heim, auswaerts);

        var kopf = _seite.EinzelspielSimulieren();

        (kopf.HeimUntertitel, kopf.AuswaertsUntertitel).Should().Be((heimUntertitel, auswaertsUntertitel));
    }

    /// <summary>Damit eine falsch zusammengezählte Summe auffällt, darf kein Spiel nach 90 Minuten unentschieden enden:
    /// Das Hinspiel kommt darum von Hand (3:1), und als Gegner dient ein deutlich schwächeres Team – der nächste, falls
    /// das Rückspiel doch unentschieden endet.</summary>
    [Fact]
    public void HinUndRueckspiel_GesamtstandIstDieSummeUndDerSiegerStimmt()
    {
        _seite.RegelwerkWaehlen(MIT_VERLAENGERUNG);
        _seite.MitRueckspielSetzen(true);

        var gelesen = new List<HinRueckAnzeige>();
        foreach (string gegner in SCHWACHE_GEGNER)
        {
            _seite.MannschaftenWaehlen(HEIM, gegner);
            var hinspiel = _seite.HinspielEingeben(3, 1);
            var gesamtNachHinspiel = _seite.GesamtNachHinspiel();
            var paarung = _seite.RueckspielSimulieren();
            gelesen.Add(paarung);
            if (paarung.Rueckspiel.ToreNach90 is not (int rueckHeim, int rueckAuswaerts) || rueckHeim == rueckAuswaerts)
                continue;

            hinspiel.Should().Match<SpielKopfAnzeige>(k => k.Heim == HEIM && k.Auswaerts == gegner);
            gesamtNachHinspiel.Score.Should().Be("3:1");
            gesamtNachHinspiel.Entscheidung.Should().Be("Ergebnis nach Hinspiel");
            paarung.Rueckspiel.Should().Match<SpielKopfAnzeige>(k => k.Heim == gegner && k.Auswaerts == HEIM,
                "im Rückspiel tauschen die Mannschaften das Heimrecht");
            PruefeGesamtwertung(paarung);
            _seite.Fehlermeldung.Should().BeEmpty();
            return;
        }
        throw new InvalidOperationException($"Jedes Rückspiel endete nach 90 Minuten unentschieden: {string.Join("; ", gelesen.Select(g => g.Rueckspiel))}");
    }

    /// <summary>Den Gleichstand nach 180 Minuten erzwingt die Hinspiel-Eingabe von Hand: verschiedene Gegner und
    /// Hinspiele, bis das Rückspiel eines ausgleicht. Mit festem Seed ist das Rückspiel zu jedem Hinspiel immer
    /// dasselbe, der Test also wiederholbar. Bis #673 spiegelte der Test einfach das letzte Rückspiel, weil es sich
    /// unabhängig vom Hinspiel wiederholte; seit die Taktik nach dem Gesamtstand würfelt, hängt der Verlauf am Hinspiel.</summary>
    [Theory]
    [InlineData(MIT_VERLAENGERUNG)]
    [InlineData(DIREKTES_ELFMETERSCHIESSEN)]
    [InlineData(null)]
    public void HinUndRueckspiel_GleichstandNach180Minuten_EntscheidetDasRegelwerk(string? regelwerk)
    {
        _seite.MannschaftenWaehlen(HEIM, AUSWAERTS);
        if (regelwerk is not null) _seite.RegelwerkWaehlen(regelwerk);
        _seite.MitRueckspielSetzen(true);
        _seite.HinspielSimulieren();

        var paarung = GleichstandHerbeifuehren();

        PruefeGesamtwertung(paarung);
        if (regelwerk is null)
        {
            paarung.Siegerzeile.Should().Be(HinUndRueckspiel.UNENTSCHIEDEN);
            paarung.Gesamt.Entscheidung.Should().Be("nach 180 Minuten");
        }
        else
        {
            paarung.Siegerzeile.Should().NotBe(HinUndRueckspiel.UNENTSCHIEDEN, "das Regelwerk endet im Elfmeterschießen");
            paarung.Gesamt.Entscheidung.Should().Match(e => e == "nach Verlängerung" || e.StartsWith("nach Elfmeterschießen"));
            if (regelwerk == DIREKTES_ELFMETERSCHIESSEN)
                paarung.Rueckspiel.ImElfmeterschiessen.Should().BeTrue("ohne Verlängerung geht es direkt ins Elfmeterschießen");
        }
    }

    /// <summary>#539: Mit aktiver Regel entscheiden bei Gleichstand nach 180 Minuten die Auswärtstore – das Regelwerk
    /// (hier mit Verlängerung) kommt nicht mehr dran. Gesucht wird eine Paarung, bei der das eintritt; der Hinweis
    /// „nach Auswärtstorregel“ im Rückspielkopf zeigt es an.</summary>
    [Fact]
    public void HinUndRueckspiel_MitAuswaertstorregel_EntscheidenDieAuswaertstoreVorDemRegelwerk()
    {
        _seite.MannschaftenWaehlen(HEIM, AUSWAERTS);
        _seite.RegelwerkWaehlen(MIT_VERLAENGERUNG);
        _seite.MitRueckspielSetzen(true);
        _seite.AuswaertstorregelSetzen(true);
        _seite.HinspielSimulieren();

        var paarung = GleichstandHerbeifuehren(p => p.Rueckspiel.NachAuswaertstorregel);

        paarung.Gesamt.Entscheidung.Should().Be("nach Auswärtstorregel");
        paarung.Siegerzeile.Should().NotBe(HinUndRueckspiel.UNENTSCHIEDEN);
        paarung.Rueckspiel.Zwischenstaende.Should().NotContain("n.V.", "die Regel greift vor der Verlängerung");
        PruefeGesamtwertung(paarung);
    }

    /// <summary>#539: Die Checkbox gibt es nur mit „Mit Rückspiel“; ein Wechsel verwirft das Rückspiel (es hing von der
    /// Regel ab), das Hinspiel bleibt und der Gesamtkopf zeigt wieder den Stand nach dem Hinspiel.</summary>
    [Fact]
    public void Auswaertstorregel_NurMitRueckspiel_UndEinWechselVerwirftDasRueckspiel()
    {
        _seite.MannschaftenWaehlen(HEIM, SCHWACHE_GEGNER[0]);
        _seite.MitRueckspielSetzen(false);
        _seite.AuswaertstorregelSichtbar.Should().BeFalse("ohne Rückspiel gibt es keine Auswärtstore");
        _seite.MitRueckspielSetzen(true);
        _seite.AuswaertstorregelSichtbar.Should().BeTrue();

        _seite.AuswaertstorregelSetzen(true); // ohne Hinspiel und Rückspiel: folgenlos
        _seite.HinspielEingeben(3, 1);
        _seite.RueckspielSimulieren();
        _seite.RueckspielVerworfen.Should().BeFalse();

        _seite.AuswaertstorregelSetzen(false);

        Wait.Until(() => _seite.RueckspielVerworfen, Wait.DefaultTimeout, "Rückspiel nach dem Wechsel verworfen");
        _seite.GesamtNachHinspiel().Entscheidung.Should().Be("Ergebnis nach Hinspiel");
    }

    public void Dispose() => app.BeiFehlschlagBelegeSichern();

    private HinRueckAnzeige GleichstandHerbeifuehren(Func<HinRueckAnzeige, bool>? gesucht = null)
    {
        gesucht ??= p => HinUndRueckspiel.GleichstandNach180(p.Hinspiel, p.Rueckspiel);
        var gelesen = new List<string>();
        foreach (string gegner in GESTAFFELTE_GEGNER)
        {
            _seite.MannschaftenWaehlen(HEIM, gegner);
            foreach (int differenz in HINSPIEL_DIFFERENZEN)
            {
                int heim = Math.Max(differenz, 0), auswaerts = Math.Max(-differenz, 0);
                _seite.HinspielEingeben(heim, auswaerts);
                var paarung = _seite.RueckspielSimulieren();
                if (gesucht(paarung))
                    return paarung;
                gelesen.Add($"{gegner} {heim}:{auswaerts} → {paarung.Rueckspiel.Score}");
            }
        }
        throw new InvalidOperationException($"Kein Gleichstand nach 180 Minuten: {string.Join("; ", gelesen)}");
    }

    private static void PruefeGesamtwertung(HinRueckAnzeige paarung)
    {
        var (score, siegerzeile) = HinUndRueckspiel.Erwartet(paarung.Hinspiel, paarung.Rueckspiel);
        paarung.Gesamt.Should().Match<SpielKopfAnzeige>(k => k.Heim == paarung.Hinspiel.Heim && k.Auswaerts == paarung.Hinspiel.Auswaerts);
        paarung.Gesamt.Score.Should().Be(score, "der Gesamtstand ist die Summe aus Hin- und Rückspiel ({0})", paarung);
        paarung.Siegerzeile.Should().Be(siegerzeile, "der Sieger folgt aus Gesamtstand bzw. Elfmeterschießen ({0})", paarung);
    }
}
