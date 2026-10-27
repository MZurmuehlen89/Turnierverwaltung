using FlaUI.Core.AutomationElements;
using GuiTest.Framework;

namespace Fussballturnierverwaltung.GuiTests.Seiten;

/// <summary>Was alle Stammdaten-Editoren gemeinsam haben (#530): links eine Liste mit Suchfeld, rechts das Formular,
/// darüber die Befehlsleiste mit Neu, Speichern, Abbrechen und Löschen. Die AutomationIds folgen dem Schema
/// <c>&lt;Präfix&gt;.Suche</c>, <c>.Liste</c>, <c>.ListeName</c>, <c>.Name</c> und <c>.Fehlermeldung</c>. Die Listen
/// zeigen auch alle Einträge des Seeds, darum filtert jeder Zugriff erst per Suchfeld auf den eindeutigen Testnamen.</summary>
public sealed class StammdatenEditor(Window fenster, Hauptfenster hauptfenster, string praefix, string loeschdialog)
{
    public bool SpeichernFreigegeben => fenster.FindById("Befehlsleiste.Speichern").IsEnabled;

    /// <summary>Die Meldung unter dem Formular; leer, solange keine angezeigt wird.</summary>
    public string Fehlermeldung => fenster.TryFindById($"{praefix}.Fehlermeldung")?.NameOrEmpty() ?? string.Empty;

    public void Neu() => fenster.ActivateById("Befehlsleiste.Neu");

    public void Abbrechen() => fenster.ActivateById("Befehlsleiste.Abbrechen");

    /// <summary>Speichert und wartet, bis die Seite neu geladen hat – erkennbar daran, dass Speichern wieder gesperrt
    /// ist (keine ungespeicherten Änderungen mehr).</summary>
    public void Speichern()
    {
        fenster.ActivateById("Befehlsleiste.Speichern");
        Wait.Until(() => !SpeichernFreigegeben, Wait.DefaultTimeout, "gespeichert (Speichern wieder gesperrt)");
        hauptfenster.WarteBisRuhig();
    }

    /// <summary>Speichert in der Erwartung, dass die Prüfung ablehnt, und liefert die Meldung, sobald sie
    /// <paramref name="enthaelt"/> enthält – sonst die tatsächliche (leer, wenn keine kam).</summary>
    public string SpeichernAbgelehnt(string enthaelt)
    {
        fenster.ActivateById("Befehlsleiste.Speichern");
        Wait.Within(() => Fehlermeldung.Contains(enthaelt, StringComparison.Ordinal), Wait.DefaultTimeout);
        hauptfenster.WarteBisRuhig();
        return Fehlermeldung;
    }

    public void Suchen(string begriff) => WertSetzen("Suche", begriff);

    /// <summary>Sucht nach <paramref name="name"/> und liest den Eintrag, sobald er in der Liste steht.</summary>
    public T Eintrag<T>(string name, Func<AutomationElement, T> lesen) where T : class
    {
        Suchen(name);
        return Wait.UntilFound(() => Zeile(name, lesen), Wait.DefaultTimeout, $"'{name}' in der Liste");
    }

    /// <summary>Ob die Suche nach <paramref name="name"/> keinen Eintrag mehr findet.</summary>
    public bool NichtMehrGelistetAbwarten(string name)
    {
        Suchen(name);
        return Wait.Within(() => Zeile(name, e => e) is null, Wait.DefaultTimeout);
    }

    /// <summary>Wählt den Eintrag in der Liste und wartet, bis das Formular ihn zeigt.</summary>
    public void Auswaehlen(string name)
    {
        Eintrag(name, e => e).Activate();
        Wait.Until(() => fenster.TryFindById($"{praefix}.Name") is not null && Wert("Name") == name,
            Wait.DefaultTimeout, $"Formular zeigt '{name}'");
    }

    /// <summary>Löscht den gewählten Eintrag über die Befehlsleiste und beantwortet die Rückfrage.</summary>
    public void Loeschen(bool bestaetigen)
    {
        fenster.ActivateById("Befehlsleiste.Loeschen");
        var dialog = Dialog.WaitFor(fenster, loeschdialog);
        if (bestaetigen) dialog.Confirm();
        else dialog.Close();

        Wait.Until(() => Dialog.FindOpen(fenster).All(d => d.Title != loeschdialog), Wait.DefaultTimeout,
            $"Dialog '{loeschdialog}' geschlossen");
        hauptfenster.WarteBisRuhig();
    }

    /// <summary>Text des Formularfelds <c>&lt;Präfix&gt;.<paramref name="feld"/></c>.</summary>
    public string Wert(string feld) =>
        fenster.FindById($"{praefix}.{feld}").Patterns.Value.Pattern.Value.ValueOrDefault ?? string.Empty;

    public void WertSetzen(string feld, string wert) =>
        fenster.FindById($"{praefix}.{feld}").Patterns.Value.Pattern.SetValue(wert);

    /// <summary>Wert des Schiebereglers <c>&lt;Präfix&gt;.<paramref name="feld"/></c>.</summary>
    public int Regler(string feld) => (int)fenster.FindById($"{praefix}.{feld}").Patterns.RangeValue.Pattern.Value.Value;

    public void ReglerSetzen(string feld, int wert) =>
        fenster.FindById($"{praefix}.{feld}").Patterns.RangeValue.Pattern.SetValue(wert);

    /// <summary>Wählt in der Auswahlliste <c>&lt;Präfix&gt;.<paramref name="feld"/></c> den Eintrag, der
    /// <paramref name="text"/> anzeigt.</summary>
    public void Auswahl(string feld, string text) => fenster.FindById($"{praefix}.{feld}").AsComboBox().SelectItem(text);

    private T? Zeile<T>(string name, Func<AutomationElement, T> lesen) where T : class =>
        fenster.FindById($"{praefix}.Liste")
            .ReadAllItems(e => e.TextOf($"{praefix}.ListeName") == name ? lesen(e) : null)
            .FirstOrDefault();
}
