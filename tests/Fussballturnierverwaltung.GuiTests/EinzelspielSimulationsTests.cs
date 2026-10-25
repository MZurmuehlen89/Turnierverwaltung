using FluentAssertions;
using Fussballturnierverwaltung.GuiTests.Seiten;

namespace Fussballturnierverwaltung.GuiTests;

/// <summary>Freie Einzelspiel-Simulation (#536): ein Einzelspiel sowie Hin- und Rückspiel zweier Seed-Mannschaften.
/// Die Simulation speichert nichts, der Seed bleibt also unverändert. Geprüft werden nur Invarianten – Gesamtstand
/// ist die Summe beider Spiele, der Sieger folgt aus Gesamtstand bzw. Elfmeterschießen –, keine festen Ergebnisse.
/// Eine Auswärtstorregel kennt die Seite nicht (weder Einstellung noch Entscheidungsverfahren des Regelwerks).</summary>
public sealed class EinzelspielSimulationsTests(TurnierverwaltungApp app) : IClassFixture<TurnierverwaltungApp>, IDisposable
{
    private const string HEIM = "Deutschland";
    private const string AUSWAERTS = "Frankreich";
    // Deutlich schwächer als HEIM: Mit festem Seed zieht jede Simulation dieselbe Zufallsfolge, gleich starke Teams
    // spielen dann stets gleich (Deutschland gegen Frankreich, Spanien, Italien, England, … im Rückspiel je 1:1)
    private static readonly string[] SCHWACHE_GEGNER = ["San Marino", "Gibraltar", "Andorra", "Liechtenstein", "Färöer"];
    private const string MIT_VERLAENGERUNG = "FIFA Standard Modern (3-Punkte)";
    private const string DIREKTES_ELFMETERSCHIESSEN = "CONMEBOL Copa América (direktes Elfmeterschießen)";

    // Ohne festen Seed trifft jeder Versuch mit gut einem Viertel einen Gleichstand; mit Seed klappt es beim ersten
    private const int GLEICHSTAND_VERSUCHE = 15;

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

    /// <summary>Den Gleichstand nach 180 Minuten erzwingt die Hinspiel-Eingabe von Hand: Sie spiegelt den regulären
    /// Stand des letzten Rückspiels. Mit festem Seed wiederholt sich der beim nächsten Rückspiel genau.</summary>
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

    public void Dispose() => app.BeiFehlschlagBelegeSichern();

    private HinRueckAnzeige GleichstandHerbeifuehren()
    {
        for (int versuch = 0; versuch < GLEICHSTAND_VERSUCHE; versuch++)
        {
            var paarung = _seite.RueckspielSimulieren();
            if (HinUndRueckspiel.GleichstandNach180(paarung.Hinspiel, paarung.Rueckspiel))
                return paarung;

            var (rueckHeim, rueckAuswaerts) = paarung.Rueckspiel.ToreNach90!.Value;
            _seite.HinspielEingeben(rueckHeim, rueckAuswaerts);
        }
        throw new InvalidOperationException($"Kein Gleichstand nach 180 Minuten in {GLEICHSTAND_VERSUCHE} Versuchen.");
    }

    private static void PruefeGesamtwertung(HinRueckAnzeige paarung)
    {
        var (score, siegerzeile) = HinUndRueckspiel.Erwartet(paarung.Hinspiel, paarung.Rueckspiel);
        paarung.Gesamt.Should().Match<SpielKopfAnzeige>(k => k.Heim == paarung.Hinspiel.Heim && k.Auswaerts == paarung.Hinspiel.Auswaerts);
        paarung.Gesamt.Score.Should().Be(score, "der Gesamtstand ist die Summe aus Hin- und Rückspiel ({0})", paarung);
        paarung.Siegerzeile.Should().Be(siegerzeile, "der Sieger folgt aus Gesamtstand bzw. Elfmeterschießen ({0})", paarung);
    }
}
