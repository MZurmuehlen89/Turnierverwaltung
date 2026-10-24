using FlaUI.Core.AutomationElements;
using GuiTest.Framework;

namespace Fussballturnierverwaltung.GuiTests.Seiten;

/// <summary>Seitenobjekt „Turniere“: Turniere aus einer Vorlage anlegen.</summary>
public sealed class TurnierlisteSeite(Window fenster, Hauptfenster hauptfenster)
{
    private const string DIALOGTITEL = "Aus Vorlage erstellen";

    /// <summary>Legt über den Dialog „Aus Vorlage erstellen“ ein echtes Turnier an. Die Vorlage wird über Name und
    /// Variante bestimmt, weil mehrere Vorlagen gleich heißen („Mini-Turnier“ mit 4 und 8 Teams). Der Name kommt
    /// nach der Saison, weil die Saison den vorgeschlagenen Namen überschreibt.</summary>
    public void AusVorlageErstellen(string vorlage, string variante, string saison, string name)
    {
        fenster.FindById("Befehlsleiste.AusVorlageErstellen").Activate();
        var dialog = Dialog.WaitFor(fenster, DIALOGTITEL);
        dialog.Element.FindById("AusVorlage.Vorlage").AsComboBox().SelectItem(vorlage, variante);
        dialog.Element.FindById("AusVorlage.Saison").Patterns.Value.Pattern.SetValue(saison);
        dialog.Element.FindById("AusVorlage.Name").Patterns.Value.Pattern.SetValue(name);
        dialog.Confirm();

        Wait.Until(() => Dialog.FindOpen(fenster).All(d => d.Title != DIALOGTITEL), Wait.DefaultTimeout,
            $"Dialog '{DIALOGTITEL}' geschlossen");
        hauptfenster.WarteBisRuhig();
    }
}
