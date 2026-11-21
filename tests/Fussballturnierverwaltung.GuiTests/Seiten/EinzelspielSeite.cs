using System.Drawing;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using GuiTest.Framework;

namespace Fussballturnierverwaltung.GuiTests.Seiten;

/// <summary>Was eine Hin-/Rückspiel-Paarung nach dem Rückspiel zeigt.</summary>
public sealed record HinRueckAnzeige(
    SpielKopfAnzeige Hinspiel, SpielKopfAnzeige Rueckspiel, SpielKopfAnzeige Gesamt, string Siegerzeile);

/// <summary>Seitenobjekt „Einzelspiel-Simulation“ (#536): zwei Mannschaften wählen und ein Einzelspiel oder Hin- und
/// Rückspiel simulieren. Simuliert wird nur angezeigt, nichts gespeichert – der Seed bleibt unberührt.</summary>
public sealed class EinzelspielSeite(Window fenster)
{
    private const string HINSPIEL = "Einzelspiel.Hinspiel";
    private const string RUECKSPIEL = "Einzelspiel.Rueckspiel";
    private const string GESAMT = "Einzelspiel.Gesamt";
    private const string SIEGER = "Einzelspiel.Sieger";
    private const string RUECKSPIEL_AUSSTEHEND = "Einzelspiel.RueckspielAusstehend";
    private const string WIEDERHOLUNGSSPIEL = "Einzelspiel.Wiederholungsspiel";
    private const string WIEDERHOLUNG_AUSSTEHEND = "Einzelspiel.WiederholungsspielAusstehend";
    private const string SPIEL_WIEDERHOLUNGSSPIEL = "Spielbericht.Wiederholungsspiel";
    private const string SPIEL_WIEDERHOLUNG_AUSSTEHEND = "Spielbericht.WiederholungsspielAusstehend";
    private const string WIEDERHOLUNG_KNOPF = "Befehlsleiste.WiederholungsspielSimulieren";

    // Die gewählten Mannschaften: Erst wenn ein Spielkopf sie zeigt, gehört er zur letzten Aktion und nicht zu einer
    // früheren Paarung.
    private string _heim = string.Empty;
    private string _auswaerts = string.Empty;

    /// <summary>Filtert jede Auswahlliste erst über ihr Suchfeld – die Liste umfasst alle Mannschaften des Seeds. Die
    /// Suche kommt vor der Auswahl, weil ein neuer Filter die Liste ersetzt und die Auswahl dabei verloren ginge.</summary>
    public void MannschaftenWaehlen(string heim, string auswaerts)
    {
        Waehlen("Einzelspiel.HeimSuche", "Einzelspiel.Heim", heim);
        Waehlen("Einzelspiel.AuswaertsSuche", "Einzelspiel.Auswaerts", auswaerts);
        (_heim, _auswaerts) = (heim, auswaerts);
    }

    public void RegelwerkWaehlen(string regelwerk) =>
        fenster.FindById("Einzelspiel.Regelwerk").AsComboBox().SelectItem(regelwerk);

    public void MitRueckspielSetzen(bool mitRueckspiel)
    {
        var toggle = fenster.FindById("Einzelspiel.MitRueckspiel").Patterns.Toggle.Pattern;
        bool Gesetzt() => toggle.ToggleState.Value == ToggleState.On;
        if (Gesetzt() != mitRueckspiel) toggle.Toggle();
        Wait.Until(() => Gesetzt() == mitRueckspiel, Wait.DefaultTimeout,
            $"'Mit Rückspiel' = {mitRueckspiel}");
    }

    /// <summary>Schaltet die Auswärtstorregel (#539); die Checkbox ist nur mit „Mit Rückspiel“ sichtbar.</summary>
    public void AuswaertstorregelSetzen(bool aktiv)
    {
        var toggle = fenster.FindById("Einzelspiel.Auswaertstorregel").Patterns.Toggle.Pattern;
        bool Gesetzt() => toggle.ToggleState.Value == ToggleState.On;
        if (Gesetzt() != aktiv) toggle.Toggle();
        Wait.Until(() => Gesetzt() == aktiv, Wait.DefaultTimeout, $"'Auswärtstorregel' = {aktiv}");
    }

    public bool AuswaertstorregelSichtbar => fenster.TryFindById("Einzelspiel.Auswaertstorregel") is not null;

    /// <summary>Simuliert ein Einzelspiel und liefert den Spielkopf, sobald er die gewählten Mannschaften zeigt.</summary>
    public SpielKopfAnzeige EinzelspielSimulieren()
    {
        fenster.ActivateById("Befehlsleiste.EinzelspielSimulieren");
        return Wait.UntilFound(
            () => KopfLesen(fenster) is { } kopf && ZeigtErgebnis(kopf, _heim, _auswaerts) ? kopf : null,
            Wait.DefaultTimeout,
            $"Spielkopf {_heim} – {_auswaerts} mit Ergebnis");
    }

    /// <summary>Simuliert das Hinspiel; vorher steht noch keine Hinspiel-Karte da.</summary>
    public SpielKopfAnzeige HinspielSimulieren()
    {
        fenster.ActivateById("Befehlsleiste.HinspielSimulieren");
        return KopfAbwarten(HINSPIEL, _heim, _auswaerts);
    }

    /// <summary>Gibt das Hinspiel-Ergebnis von Hand ein und wartet, bis die Hinspiel-Karte es zeigt.</summary>
    public SpielKopfAnzeige HinspielEingeben(int heimTore, int auswaertsTore)
    {
        fenster.FindById("Einzelspiel.HinspielHeimTore").Patterns.RangeValue.Pattern.SetValue(heimTore);
        fenster.FindById("Einzelspiel.HinspielAuswaertsTore").Patterns.RangeValue.Pattern.SetValue(auswaertsTore);
        fenster.ActivateById("Einzelspiel.HinspielUebernehmen");
        return KopfAbwarten(HINSPIEL, _heim, _auswaerts, kopf => kopf.Score == $"{heimTore}:{auswaertsTore}");
    }

    /// <summary>Der Gesamtkopf, solange nur das Hinspiel feststeht.</summary>
    public SpielKopfAnzeige GesamtNachHinspiel() => KopfAbwarten(GESAMT, _heim, _auswaerts);

    /// <summary>Simuliert das Rückspiel und liest alles ab, sobald die Siegerzeile dasteht. Ein neues Hinspiel verwirft
    /// ein vorhandenes Rückspiel samt Siegerzeile, darum gehört die Zeile immer zum gerade simulierten Rückspiel.</summary>
    public HinRueckAnzeige RueckspielSimulieren()
    {
        fenster.ActivateById("Befehlsleiste.RueckspielSimulieren");
        string siegerzeile = Wait.UntilFound(
            () => fenster.TryFindById(SIEGER)?.NameOrEmpty() is { Length: > 0 } text ? text : null,
            Wait.DefaultTimeout,
            "Siegerzeile nach dem Rückspiel");
        return new HinRueckAnzeige(
            KopfAbwarten(HINSPIEL, _heim, _auswaerts),
            KopfAbwarten(RUECKSPIEL, _auswaerts, _heim),
            KopfAbwarten(GESAMT, _heim, _auswaerts),
            siegerzeile);
    }

    // ── Wiederholungsspiel als eigener Schritt (#691) ──────────────────────────────

    /// <summary>Ob das Wiederholungsspiel ansteht: Platzhalter in der Spalte des Wiederholungsspiels (mit Rückspiel in der
    /// Paarung, sonst neben dem Spiel).</summary>
    public bool WiederholungAusstehend =>
        fenster.TryFindById(WIEDERHOLUNG_AUSSTEHEND) is not null || fenster.TryFindById(SPIEL_WIEDERHOLUNG_AUSSTEHEND) is not null;

    /// <summary>Ob die Karte des Wiederholungsspiels steht.</summary>
    public bool WiederholungsspielGespielt =>
        fenster.TryFindById(WIEDERHOLUNGSSPIEL) is not null || fenster.TryFindById(SPIEL_WIEDERHOLUNGSSPIEL) is not null;

    /// <summary>Ob der Knopf „Wiederholungsspiel simulieren“ in der Leiste steht (nur bei Regelwerken mit Wiederholungsspiel).</summary>
    public bool WiederholungsspielKnopfVorhanden => fenster.TryFindById(WIEDERHOLUNG_KNOPF) is not null;

    /// <summary>Ob der Knopf aktiv ist – erst wenn das Wiederholungsspiel ansteht.</summary>
    public bool WiederholungsspielKnopfAktiv => fenster.TryFindById(WIEDERHOLUNG_KNOPF)?.IsEnabled == true;

    /// <summary>Simuliert das Rückspiel, ohne eine Siegerzeile zu verlangen: Es endet mit Sieger oder – bei Gleichstand und
    /// Wiederholungsspiel in der Kette – mit dem Platzhalter. Liefert, ob das Wiederholungsspiel ansteht.</summary>
    public bool RueckspielSimulierenBisWiederholung()
    {
        fenster.ActivateById("Befehlsleiste.RueckspielSimulieren");
        Wait.Until(() => WiederholungAusstehend || SiegerzeileSichtbar, Wait.DefaultTimeout,
            "Siegerzeile oder Platzhalter des Wiederholungsspiels nach dem Rückspiel");
        return WiederholungAusstehend;
    }

    /// <summary>Simuliert ein Einzelspiel ohne Rückspiel und liefert, ob das Wiederholungsspiel ansteht.</summary>
    public bool EinzelspielSimulierenBisWiederholung()
    {
        fenster.ActivateById("Befehlsleiste.EinzelspielSimulieren");
        Wait.Until(
            () => WiederholungAusstehend
                  || (fenster.TryFindById("Spielbericht.Spiel") is { } karte && KopfLesen(karte) is { } kopf && ZeigtErgebnis(kopf, _heim, _auswaerts)),
            Wait.DefaultTimeout, "Spielkopf oder Platzhalter des Wiederholungsspiels nach dem Einzelspiel");
        return WiederholungAusstehend;
    }

    /// <summary>Klickt „Wiederholungsspiel simulieren“ und wartet auf dessen Karte.</summary>
    public SpielKopfAnzeige WiederholungsspielSimulieren()
    {
        fenster.ActivateById(WIEDERHOLUNG_KNOPF);
        return Wait.UntilFound(
            () => (fenster.TryFindById(WIEDERHOLUNGSSPIEL) ?? fenster.TryFindById(SPIEL_WIEDERHOLUNGSSPIEL)) is { } karte
                  && KopfLesen(karte) is { } kopf && Spielstand.Tore(kopf.Score) is not null ? kopf : null,
            Wait.DefaultTimeout,
            "Karte des Wiederholungsspiels");
    }

    /// <summary>Der Spielkopf des (ersten) Spiels ohne Rückspiel.</summary>
    public SpielKopfAnzeige SpielKopf() =>
        KopfAbwarten("Spielbericht.Spiel", _heim, _auswaerts);

    public Rectangle WiederholungAusstehendLage =>
        Lage(fenster.TryFindById(WIEDERHOLUNG_AUSSTEHEND) is not null ? WIEDERHOLUNG_AUSSTEHEND : SPIEL_WIEDERHOLUNG_AUSSTEHEND);

    /// <summary>Wie viele Spielkarten (Hinspiel, Rückspiel, Wiederholungsspiel, Platzhalter) nebeneinander stehen (#691 K13).</summary>
    public int Spielspalten =>
        new[] { HINSPIEL, RUECKSPIEL, RUECKSPIEL_AUSSTEHEND, WIEDERHOLUNGSSPIEL, WIEDERHOLUNG_AUSSTEHEND,
                "Spielbericht.Spiel", SPIEL_WIEDERHOLUNGSSPIEL, SPIEL_WIEDERHOLUNG_AUSSTEHEND }
            .Count(id => fenster.TryFindById(id) is not null);

    public Rectangle SpielLage => Lage("Spielbericht.Spiel");
    public Rectangle WiederholungsspielLage => Lage(fenster.TryFindById(WIEDERHOLUNGSSPIEL) is not null ? WIEDERHOLUNGSSPIEL : SPIEL_WIEDERHOLUNGSSPIEL);

    /// <summary>Der Gesamtkopf nach dem Rückspiel, auch wenn noch kein Sieger feststeht.</summary>
    public SpielKopfAnzeige GesamtLesen() => KopfAbwarten(GESAMT, _heim, _auswaerts);

    /// <summary>Ob die Siegerzeile der Paarung dasteht.</summary>
    public bool SiegerzeileSichtbar => fenster.TryFindById(SIEGER) is not null;

    /// <summary>Der Kopf der Rückspiel-Karte.</summary>
    public SpielKopfAnzeige RueckspielKopf() => KopfAbwarten(RUECKSPIEL, _auswaerts, _heim);

    /// <summary>Ob ein vorhandenes Rückspiel samt Siegerzeile verworfen ist (#539) – beide sind dann nicht mehr im
    /// UIA-Baum, weil ihre Elemente ausgeblendet sind.</summary>
    public bool RueckspielVerworfen =>
        fenster.TryFindById(RUECKSPIEL) is null && fenster.TryFindById(SIEGER) is null;

    /// <summary>Ob rechts neben dem Hinspiel der Platzhalter „Rückspiel steht noch aus“ steht (#247).</summary>
    public bool RueckspielAusstehend => fenster.TryFindById(RUECKSPIEL_AUSSTEHEND) is not null;

    // Lage der Karten und des Gesamtkopfs auf dem Bildschirm (#247: Spalten nebeneinander, Gesamtkopf darüber).
    public Rectangle HinspielLage => Lage(HINSPIEL);
    public Rectangle RueckspielLage => Lage(RUECKSPIEL);
    public Rectangle GesamtLage => Lage(GESAMT);
    public Rectangle PlatzhalterLage => Lage(RUECKSPIEL_AUSSTEHEND);

    private Rectangle Lage(string id) =>
        (fenster.TryFindById(id) ?? throw new InvalidOperationException($"'{id}' ist nicht zu sehen.")).BoundingRectangle;

    public string Fehlermeldung => fenster.TryFindById("Einzelspiel.Fehlermeldung")?.NameOrEmpty() ?? string.Empty;

    private void Waehlen(string sucheId, string auswahlId, string mannschaft)
    {
        fenster.FindById(sucheId).Patterns.Value.Pattern.SetValue(mannschaft);
        fenster.FindById(auswahlId).AsComboBox().SelectItem(mannschaft);
    }

    private SpielKopfAnzeige KopfAbwarten(
        string containerId, string heim, string auswaerts, Func<SpielKopfAnzeige, bool>? bedingung = null) =>
        Wait.UntilFound(
            () => fenster.TryFindById(containerId) is { } container && KopfLesen(container) is { } kopf
                  && ZeigtErgebnis(kopf, heim, auswaerts) && (bedingung?.Invoke(kopf) ?? true) ? kopf : null,
            Wait.DefaultTimeout,
            $"Spielkopf '{containerId}' {heim} – {auswaerts}");

    private static bool ZeigtErgebnis(SpielKopfAnzeige kopf, string heim, string auswaerts) =>
        kopf.Heim == heim && kopf.Auswaerts == auswaerts && Spielstand.Tore(kopf.Score) is not null;

    private static SpielKopfAnzeige? KopfLesen(AutomationElement container) =>
        container.TryFindById("SpielKopf.Score") is null
            ? null
            : new SpielKopfAnzeige(
                container.TextOf("SpielKopf.HeimName"),
                container.TextOf("SpielKopf.AuswaertsName"),
                container.TextOf("SpielKopf.Score"),
                container.TextOf("SpielKopf.Entscheidung"),
                container.TextOf("SpielKopf.Zwischenstaende"),
                container.TextOf("SpielKopf.HeimUntertitel"),
                container.TextOf("SpielKopf.AuswaertsUntertitel"));
}
