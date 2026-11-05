using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using GuiTest.Framework;

namespace Fussballturnierverwaltung.GuiTests.Seiten;

/// <summary>Reiter „Runden“ der Turnierliste: Runden anlegen, bearbeiten und entfernen, Übergangsregeln zwischen den
/// Runden und die Vorgabe „Paarung fest vorgegeben“. Alle Änderungen bleiben im Formular, bis „Speichern“ sie als
/// Ganzes schreibt.</summary>
public sealed class RundenReiter(Window fenster, Hauptfenster hauptfenster)
{
    public bool SpeichernFreigegeben => fenster.FindById("Befehlsleiste.Speichern").IsEnabled;

    public IReadOnlyList<string> Rundennamen =>
        fenster.FindById("Runden.Liste").ReadAllItems(e => e.TextOf("Runden.ListeName"));

    public string Name => fenster.FindById("Runden.Name").Patterns.Value.Pattern.Value.ValueOrDefault ?? string.Empty;

    private IReadOnlyList<string> Fehler => Meldungen.Fehler;

    /// <summary>Die Meldungsleiste des Reiters.</summary>
    public MeldungsLeiste Meldungen => new(fenster, "Runden");

    /// <summary>Ein Feld der gewählten Runde, z. B. „Eingang“ (Kennung ohne <c>Runden.</c>) – für die Feldmarkierung.</summary>
    public AutomationElement Feld(string feld) => fenster.FindById($"Runden.{feld}");

    /// <summary>„Platz von“ bzw. „bis“ (<paramref name="feld"/> = <c>PlatzVon</c>/<c>PlatzBis</c>) in Zeile
    /// <paramref name="zeile"/> der offenen Übergangsregel.</summary>
    public AutomationElement RegelFeld(string feld, int zeile) => fenster.FindAllById($"Runden.Regel{feld}")[zeile];

    /// <summary>Die Fehleranzahl am Symbol der Runde in der Liste (#601), sobald sie <paramref name="erwartet"/> ist; leer =
    /// kein Symbol.</summary>
    public string FehlerAnzahlAbwarten(string runde, string erwartet)
    {
        Wait.Within(() => FehlerAnzahl(runde) == erwartet, Wait.DefaultTimeout);
        return FehlerAnzahl(runde);
    }

    private string FehlerAnzahl(string runde) =>
        fenster.FindById("Runden.Liste")
            .ReadAllItems(e => e.TextOf("Runden.ListeName") == runde ? e.TextOf("Runden.ListeFehler") : null)
            .Single();

    /// <summary>Wartet, bis das Formular die Runde <paramref name="runde"/> zeigt, und liefert den gezeigten Namen.</summary>
    public string GezeigteRundeAbwarten(string runde)
    {
        Wait.Within(() => Name == runde, Wait.DefaultTimeout);
        return Name;
    }

    /// <summary>Hängt eine neue Runde an und füllt sie aus. Die Rundenart kommt zuerst, weil sie bestimmt, welche
    /// Felder das Formular zeigt (Gruppenanzahl nur bei Gruppenphase).</summary>
    public void Anlegen(string name, string rundenart, int eingang, int ausgang, int? gruppen = null)
    {
        int vorher = Rundennamen.Count;
        fenster.ActivateById("Befehlsleiste.Neu");
        Wait.Until(() => Rundennamen.Count == vorher + 1, Wait.DefaultTimeout, "neue Runde in der Liste");
        Ausfuellen(name, rundenart, eingang, ausgang, gruppen);
    }

    public void Ausfuellen(string name, string rundenart, int eingang, int ausgang, int? gruppen = null)
    {
        fenster.FindById("Runden.Rundenart").AsComboBox().SelectItem(rundenart);
        WertSetzen("Eingang", eingang.ToString());
        WertSetzen("Ausgang", ausgang.ToString());
        if (gruppen is { } anzahl) WertSetzen("Gruppen", anzahl.ToString());
        Umbenennen(name);
    }

    public void Umbenennen(string name)
    {
        WertSetzen("Name", name);
        Wait.Until(() => Rundennamen.Contains(name), Wait.DefaultTimeout, $"Runde '{name}' in der Liste");
    }

    public void AusgangSetzen(int ausgang) => WertSetzen("Ausgang", ausgang.ToString());

    /// <summary>„Anzahl Spieltage“ einer Runde „Schweizer System“ – ausdrücklich gesetzt, damit ein Test nicht vom
    /// Vorschlag des Formulars abhängt.</summary>
    public void SpieltageSetzen(int spieltage) => WertSetzen("AnzahlSpieltage", spieltage.ToString());

    /// <summary>Wählt die Runde in der Liste und wartet, bis das Formular sie zeigt.</summary>
    public void Auswaehlen(string name)
    {
        fenster.FindById("Runden.Liste")
            .ReadAllItems(e => e.TextOf("Runden.ListeName") == name ? e : null)
            .Single()
            .Activate();
        Wait.Until(() => fenster.TryFindById("Runden.Name") is not null && Name == name, Wait.DefaultTimeout,
            $"Formular zeigt Runde '{name}'");
    }

    /// <summary>Entfernt die gewählte Runde (ohne Rückfrage – erst „Speichern“ schreibt es).</summary>
    public void Entfernen(string name)
    {
        Auswaehlen(name);
        fenster.ActivateById("Befehlsleiste.Loeschen");
        Wait.Until(() => !Rundennamen.Contains(name), Wait.DefaultTimeout, $"Runde '{name}' entfernt");
    }

    /// <summary>Legt bei der gewählten Runde eine ausgehende Übergangsregel nach <paramref name="ziel"/> an. Die Zeilen
    /// der Regel füllt die App aus der Quellrunde vor: bei einer Gruppenphase je Gruppe die Plätze bis Ausgang ÷
    /// Gruppen, bei einer K.-o.-Runde je Spiel der Sieger.</summary>
    public void AusgehendeRegelAnlegen(string ziel)
    {
        AusgehendeRegelOeffnen(ziel);
        RegelSchliessen();
    }

    /// <summary>Wie <see cref="AusgehendeRegelAnlegen"/>, lässt das Regelformular aber offen.</summary>
    public void AusgehendeRegelOeffnen(string ziel)
    {
        Aufklappen("Runden.UebergaengeBereich");
        fenster.ActivateById("Runden.NeueAusgehendeRegel");
        fenster.FindById("Runden.RegelZiel").AsComboBox().SelectItem(ziel);
    }

    public void RegelSchliessen()
    {
        fenster.ActivateById("Runden.RegelSchliessen");
        Wait.Until(() => fenster.TryFindById("Runden.RegelZiel") is null, Wait.DefaultTimeout, "Regelformular geschlossen");
    }

    /// <summary>Ob das Regelformular offen ist, sobald es aufgeht.</summary>
    public bool RegelformularOffenAbwarten() =>
        Wait.Within(() => fenster.TryFindById("Runden.RegelZiel") is not null, Wait.DefaultTimeout);

    /// <summary>Setzt „Platz bis“ in Zeile <paramref name="zeile"/> der offenen Regel. Die Regelfelder übernehmen ihren
    /// Wert erst beim Verlassen – darum hinein und danach wieder hinaus (ins Namensfeld der Runde).</summary>
    public void RegelPlatzBisSetzen(int zeile, int platz)
    {
        var feld = RegelFeld("PlatzBis", zeile);
        feld.Focus();
        feld.Patterns.Value.Pattern.SetValue(platz.ToString());
        Feld("Name").Focus();
    }

    /// <summary>Hakt bei der gewählten Runde „Paarung fest vorgegeben“ an; das geht erst, wenn eine Übergangsregel in
    /// die Runde führt.</summary>
    public void PaarungFestVorgeben()
    {
        Aufklappen("Runden.AuslosungBereich");
        Anhaken("Runden.PaarungFestVorgegeben");
    }

    /// <summary>Hakt bei der gewählten K.-o.-Runde „Mit Rückspiel“ an, mit <paramref name="auswaertstorregel"/> auch
    /// „Auswärtstorregel aktiv“ (erst mit Rückspiel freigegeben).</summary>
    public void MitRueckspiel(bool auswaertstorregel)
    {
        Anhaken("Runden.MitRueckspiel");
        if (auswaertstorregel) Anhaken("Runden.AuswaertstorRegel");
    }

    private void Anhaken(string id)
    {
        var haken = fenster.FindById(id).AsCheckBox();
        Wait.Until(() => haken.IsEnabled, Wait.DefaultTimeout, $"'{id}' freigegeben");
        if (haken.IsChecked != true) haken.Patterns.Toggle.Pattern.Toggle();
        Wait.Until(() => haken.IsChecked == true, Wait.DefaultTimeout, $"'{id}' angehakt");
    }

    /// <summary>Die Meldungen der Live-Prüfung, sobald eine davon <paramref name="enthaelt"/> enthält – sonst die
    /// tatsächlichen.</summary>
    public IReadOnlyList<string> FehlerAbwarten(string enthaelt)
    {
        Wait.Within(() => Fehler.Any(f => f.Contains(enthaelt, StringComparison.Ordinal)), Wait.DefaultTimeout);
        return Fehler;
    }

    /// <summary>Wartet, bis keine Meldung mehr dasteht, und liefert die verbliebenen.</summary>
    public IReadOnlyList<string> KeineFehlerAbwarten()
    {
        Wait.Within(() => Fehler.Count == 0, Wait.DefaultTimeout);
        return Fehler;
    }

    public bool SpeichernFreigabeAbwarten(bool erwartet)
    {
        Wait.Within(() => SpeichernFreigegeben == erwartet, Wait.DefaultTimeout);
        return SpeichernFreigegeben;
    }

    /// <summary>Speichert Runden und Übergangsregeln und wartet, bis der Reiter neu geladen hat (Speichern wieder
    /// gesperrt). Eine Meldung der App bricht das Warten mit ihrem Text ab.</summary>
    public void Speichern()
    {
        fenster.ActivateById("Befehlsleiste.Speichern");
        Wait.Until(() => !SpeichernFreigegeben || Fehlermeldung.Length > 0, Wait.DefaultTimeout, "Runden gespeichert");
        if (Fehlermeldung.Length > 0)
            throw new InvalidOperationException($"Runden nicht gespeichert: {Fehlermeldung}");
        hauptfenster.WarteBisRuhig();
    }

    private string Fehlermeldung => new MeldungsLeiste(fenster, "Runden").Fehlermeldung;

    private void WertSetzen(string feld, string wert) =>
        fenster.FindById($"Runden.{feld}").Patterns.Value.Pattern.SetValue(wert);

    private void Aufklappen(string bereich)
    {
        var expander = fenster.FindById(bereich).Patterns.ExpandCollapse.Pattern;
        if (expander.ExpandCollapseState.Value != ExpandCollapseState.Expanded)
            expander.Expand();
        Wait.Until(() => expander.ExpandCollapseState.Value == ExpandCollapseState.Expanded, Wait.DefaultTimeout,
            $"'{bereich}' aufgeklappt");
    }
}
