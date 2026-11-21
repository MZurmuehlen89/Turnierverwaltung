using FluentAssertions;
using Fussballturnierverwaltung.GuiTests.Seiten;
using GuiTest.Framework;

namespace Fussballturnierverwaltung.GuiTests;

/// <summary>Das Wiederholungsspiel der freien Einzelspiel-Simulation als eigener Schritt (#691): Nach dem Spiel bzw. Rückspiel
/// mit Gleichstand steht an seiner Stelle ein Platzhalter und der Knopf „Wiederholungsspiel simulieren“ ist aktiv; erst der
/// Klick spielt es. Gearbeitet wird mit dem Regelwerk „Wiederholungsspiel + Los“ (das Wiederholungsspiel folgt direkt auf das
/// Remis). Wie in <see cref="EinzelspielSimulationsTests"/> hat jede Simulation mit festem Seed dieselbe Zufallsfolge, darum
/// werden Paarungen und Hinspiel-Stände der Reihe nach probiert, bis ein Remis fällt (wiederholbar).</summary>
public sealed class EinzelspielWiederholungsspielTests(TurnierverwaltungApp app) : IClassFixture<TurnierverwaltungApp>, IDisposable
{
    private const string HEIM = "Deutschland";
    private const string REGELWERK = "Historisch: Wiederholungsspiel + Los";
    private const string OHNE_WIEDERHOLUNG = "FIFA Standard Modern (3-Punkte)";
    private const string ANSTEHT = "nach Rückspiel – Wiederholungsspiel steht an";

    // Gegner in Stufen von 3–4 Rating-Punkten (siehe EinzelspielSimulationsTests): gleich starke spielen mit festem Seed gleich.
    private static readonly string[] GEGNER =
        ["Frankreich", "England", "Niederlande", "Dänemark", "Serbien", "Wales", "Island", "Spanien", "Italien"];
    private static readonly int[] HINSPIEL_DIFFERENZEN = [0, 1, -1, 2, -2];

    private readonly EinzelspielSeite _seite = app.Hauptfenster.EinzelspielSimulationOeffnen();

    /// <summary>K4 + K6 + K13: Nach dem Remis im Rückspiel steht „Wiederholungsspiel steht an“ ohne Sieger, in einer dritten
    /// Spalte der Platzhalter und der Knopf ist aktiv; der Klick ersetzt den Platzhalter durch die Karte des
    /// Wiederholungsspiels, der Gesamtkopf zeigt „nach Wiederholungsspiel“ und den Sieger, der Knopf ist wieder aus.</summary>
    [Fact]
    public void HinUndRueckspiel_NachRemis_WiederholungsspielStehtAnUndWirdPerKnopfGespielt()
    {
        HinUndRueckspielMitWiederholungHerbeifuehren();

        var gesamt = _seite.GesamtLesen();
        gesamt.Entscheidung.Should().Be(ANSTEHT);
        _seite.SiegerzeileSichtbar.Should().BeFalse("solange das Wiederholungsspiel aussteht, gibt es keinen Sieger");
        _seite.WiederholungsspielKnopfAktiv.Should().BeTrue();
        _seite.Spielspalten.Should().Be(3, "Hinspiel, Rückspiel und der Platzhalter des Wiederholungsspiels");
        _seite.WiederholungAusstehendLage.Left.Should().BeGreaterThanOrEqualTo(_seite.RueckspielLage.Right - 2);
        var rueckspiel = _seite.RueckspielKopf();
        rueckspiel.Entscheidung.Should().NotContain("Wiederholungsspiel", "die Karte zeigt nur den Stand ihres Spiels (K10)");
        rueckspiel.Zwischenstaende.Should().NotContain("Wdh.");

        var karte = _seite.WiederholungsspielSimulieren();

        Wait.Until(() => !_seite.WiederholungAusstehend, Wait.DefaultTimeout, "Platzhalter durch die Karte ersetzt");
        Spielstand.Tore(karte.Score).Should().NotBeNull();
        _seite.GesamtLesen().Entscheidung.Should().Be("nach Wiederholungsspiel");
        _seite.SiegerzeileSichtbar.Should().BeTrue("das Wiederholungsspiel (bzw. das Los danach) entscheidet");
        _seite.WiederholungsspielKnopfAktiv.Should().BeFalse("das Wiederholungsspiel ist gespielt");
        _seite.Spielspalten.Should().Be(3);
        _seite.Fehlermeldung.Should().BeEmpty();
    }

    /// <summary>K5: Vor dem Rückspiel und nach einem Rückspiel mit Sieger gibt es weder Platzhalter noch einen aktiven
    /// Knopf; bei einem Regelwerk ohne Wiederholungsspiel fehlt der Knopf ganz. K13: Ohne Wiederholungsspiel teilen sich
    /// Hin- und Rückspiel die Breite zu zweit.</summary>
    [Fact]
    public void HinUndRueckspiel_OhneRemis_KeinPlatzhalterKeinAktiverKnopfUndZweiSpalten()
    {
        _seite.MannschaftenWaehlen(HEIM, "San Marino");
        _seite.RegelwerkWaehlen(REGELWERK);
        _seite.MitRueckspielSetzen(true);
        _seite.HinspielEingeben(3, 1);

        _seite.WiederholungAusstehend.Should().BeFalse("nach dem Hinspiel steht noch kein Wiederholungsspiel an");
        _seite.WiederholungsspielKnopfVorhanden.Should().BeTrue("das Regelwerk kennt ein Wiederholungsspiel");
        _seite.WiederholungsspielKnopfAktiv.Should().BeFalse();

        _seite.RueckspielSimulierenBisWiederholung().Should().BeFalse("3:1 vorn gegen San Marino endet nicht im Gesamt-Remis");
        _seite.WiederholungAusstehend.Should().BeFalse();
        _seite.WiederholungsspielKnopfAktiv.Should().BeFalse();
        _seite.Spielspalten.Should().Be(2, "ohne Wiederholungsspiel keine leere dritte Spalte");

        _seite.RegelwerkWaehlen(OHNE_WIEDERHOLUNG);
        Wait.Until(() => !_seite.WiederholungsspielKnopfVorhanden, Wait.DefaultTimeout, "Knopf bei einem Regelwerk ohne Wiederholungsspiel");
    }

    /// <summary>K7: Ein neues Hinspiel verwirft ein ausstehendes Wiederholungsspiel (samt Rückspiel).</summary>
    [Fact]
    public void NeuesHinspiel_VerwirftDasAusstehendeWiederholungsspiel()
    {
        HinUndRueckspielMitWiederholungHerbeifuehren();

        _seite.HinspielEingeben(1, 0);

        Wait.Until(() => !_seite.WiederholungAusstehend, Wait.DefaultTimeout, "Platzhalter des Wiederholungsspiels verworfen");
        _seite.WiederholungsspielKnopfAktiv.Should().BeFalse();
        Wait.Until(() => _seite.RueckspielAusstehend, Wait.DefaultTimeout, "Rückspiel ebenfalls verworfen");
    }

    /// <summary>K8 (E2): Ein Wechsel des Regelwerks verwirft das ausstehende Wiederholungsspiel samt Rückspiel, das Hinspiel
    /// bleibt.</summary>
    [Fact]
    public void RegelwerkWechsel_VerwirftWiederholungsspielUndRueckspiel_HinspielBleibt()
    {
        HinUndRueckspielMitWiederholungHerbeifuehren();

        _seite.RegelwerkWaehlen(OHNE_WIEDERHOLUNG);

        Wait.Until(() => !_seite.WiederholungAusstehend, Wait.DefaultTimeout, "Wiederholungsspiel verworfen");
        Wait.Until(() => _seite.RueckspielVerworfen, Wait.DefaultTimeout, "Rückspiel verworfen");
        _seite.RueckspielAusstehend.Should().BeTrue("an der Stelle des Rückspiels steht wieder der Platzhalter");
        _seite.GesamtNachHinspiel().Entscheidung.Should().Be("Ergebnis nach Hinspiel");
    }

    /// <summary>K9 (E1) + K5 + K13: Auch das Einzelspiel ohne Rückspiel hat den eigenen Schritt – bei Remis stehen Platzhalter
    /// und aktiver Knopf da (und zwei Spalten), nach dem Klick die Karte des Wiederholungsspiels neben dem Spiel.</summary>
    [Fact]
    public void Einzelspiel_NachRemis_WiederholungsspielStehtAnUndWirdPerKnopfGespielt()
    {
        _seite.RegelwerkWaehlen(REGELWERK);
        _seite.MitRueckspielSetzen(false);

        EinzelspielMitRemisHerbeifuehren();

        _seite.WiederholungsspielKnopfAktiv.Should().BeTrue();
        _seite.Spielspalten.Should().Be(2, "das Spiel und der Platzhalter");
        var spiel = _seite.SpielKopf();
        spiel.Entscheidung.Should().NotContain("Wiederholungsspiel");
        var tore = Spielstand.Tore(spiel.Score);
        tore.Should().NotBeNull();
        tore!.Value.Item1.Should().Be(tore.Value.Item2, "ein Remis");

        var karte = _seite.WiederholungsspielSimulieren();

        Wait.Until(() => !_seite.WiederholungAusstehend, Wait.DefaultTimeout, "Platzhalter durch die Karte ersetzt");
        Spielstand.Tore(karte.Score).Should().NotBeNull();
        _seite.WiederholungsspielKnopfAktiv.Should().BeFalse();
        _seite.Spielspalten.Should().Be(2, "das Spiel und die Karte des Wiederholungsspiels");
        _seite.WiederholungsspielLage.Left.Should().BeGreaterThanOrEqualTo(_seite.SpielLage.Right - 2);
        _seite.SpielKopf().Score.Should().Be(spiel.Score, "das erste Spiel bleibt, wie es war");
    }

    public void Dispose() => app.BeiFehlschlagBelegeSichern();

    // Probiert Gegner und Hinspiel-Stände der Reihe nach, bis das Rückspiel zum Gesamt-Remis führt – dann steht das
    // Wiederholungsspiel an (die Kette beginnt direkt mit ihm).
    private void HinUndRueckspielMitWiederholungHerbeifuehren()
    {
        _seite.RegelwerkWaehlen(REGELWERK);
        _seite.MitRueckspielSetzen(true);
        var gelesen = new List<string>();
        foreach (string gegner in GEGNER)
        {
            _seite.MannschaftenWaehlen(HEIM, gegner);
            foreach (int differenz in HINSPIEL_DIFFERENZEN)
            {
                int heim = Math.Max(differenz, 0), auswaerts = Math.Max(-differenz, 0);
                _seite.HinspielEingeben(heim, auswaerts);
                if (_seite.RueckspielSimulierenBisWiederholung()) return;
                gelesen.Add($"{gegner} {heim}:{auswaerts}");
            }
        }
        throw new InvalidOperationException($"Kein Gesamt-Remis nach dem Rückspiel: {string.Join("; ", gelesen)}");
    }

    private void EinzelspielMitRemisHerbeifuehren()
    {
        var gelesen = new List<string>();
        foreach (string gegner in GEGNER)
        {
            foreach (var (heim, auswaerts) in new[] { (HEIM, gegner), (gegner, HEIM) })
            {
                _seite.MannschaftenWaehlen(heim, auswaerts);
                if (_seite.EinzelspielSimulierenBisWiederholung()) return;
                gelesen.Add($"{heim} – {auswaerts}");
            }
        }
        throw new InvalidOperationException($"Kein Remis im Einzelspiel: {string.Join("; ", gelesen)}");
    }
}
