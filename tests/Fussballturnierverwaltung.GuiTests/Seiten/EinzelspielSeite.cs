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
                container.TextOf("SpielKopf.Zwischenstaende"));
}
