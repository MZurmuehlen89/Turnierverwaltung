using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using GuiTest.Framework;

namespace Fussballturnierverwaltung.GuiTests.Seiten;

/// <summary>Die Meldungsleiste eines Formulars (#594): eine Kante <c>&lt;Präfix&gt;.Meldungen</c> mit Zähler, die die
/// Meldungen <c>.Fehlermeldung</c>, <c>.Fehler</c> und <c>.Hinweis</c> aufklappt. Eingeklappt stehen die Meldungen
/// nicht im UIA-Baum – jede Abfrage klappt die Leiste darum erst auf, sofern sie da ist (ohne Meldung fehlt sie).
/// Die Abfragen eignen sich für Warteschleifen: Aufklappen ist wiederholbar.</summary>
public sealed class MeldungsLeiste(Window fenster, string praefix)
{
    /// <summary>Die Speicher-/Statusmeldung; leer, solange keine angezeigt wird.</summary>
    public string Fehlermeldung => Aufgeklappt(() => fenster.TryFindById($"{praefix}.Fehlermeldung")?.NameOrEmpty() ?? string.Empty);

    /// <summary>Die Fehler der Live-Prüfung.</summary>
    public IReadOnlyList<string> Fehler =>
        Aufgeklappt<IReadOnlyList<string>>(() => [.. fenster.FindAllById($"{praefix}.Fehler").Select(f => f.NameOrEmpty())]);

    /// <summary>Der Hinweis, der das Speichern nicht verhindert; leer, solange keiner angezeigt wird.</summary>
    public string Hinweis => Aufgeklappt(() => fenster.TryFindById($"{praefix}.Hinweis")?.NameOrEmpty() ?? string.Empty);

    /// <summary>Klickt den Fehler <paramref name="fehler"/> an (#595): mit Feldbezug ein Knopf, der ins Feld springt;
    /// die Leiste klappt danach zu.</summary>
    public void Anspringen(string fehler) => Aufgeklappt(() =>
    {
        Wait.UntilFound(() => fenster.FindAllById($"{praefix}.Fehler").FirstOrDefault(f => f.NameOrEmpty() == fehler),
            Wait.DefaultTimeout, $"Fehler '{fehler}' in der Meldungsleiste").AsButton().Invoke();
        return true;
    });

    private T Aufgeklappt<T>(Func<T> lesen)
    {
        var umschalter = fenster.TryFindById($"{praefix}.Meldungen")?.Patterns.Toggle.PatternOrDefault;
        if (umschalter?.ToggleState.Value == ToggleState.Off) umschalter.Toggle();
        return lesen();
    }
}
