using FlaUI.Core.AutomationElements;
using GuiTest.Framework;

namespace Fussballturnierverwaltung.GuiTests.Seiten;

/// <summary>Seitenobjekt „Turniere“: links die Turnierliste, rechts die Reiter Turnierkopf, Runden und Vorbelegung.
/// Die Befehlsleiste gehört immer dem gewählten Reiter, darum wählt jeder Zugriff zuerst seinen Reiter.</summary>
public sealed class TurnierlisteSeite(Window fenster, Hauptfenster hauptfenster)
{
    private const string DIALOGTITEL = "Aus Vorlage erstellen";

    /// <summary>Der Reiter „Turnierkopf“ samt Liste: Neu, Speichern, Abbrechen und Löschen eines Turniers.</summary>
    public StammdatenEditor Kopf() => Reiter("Turnierkopf",
        () => new StammdatenEditor(fenster, hauptfenster, "Turnierkopf", "Turnier löschen", listenPraefix: "Turniere"));

    public RundenReiter Runden() => Reiter("Runden", () => new RundenReiter(fenster, hauptfenster));

    public VorbelegungReiter Vorbelegung() => Reiter("Vorbelegung", () => new VorbelegungReiter(fenster, hauptfenster));

    /// <summary>Die Meldungen der Live-Prüfung im Turnierkopf, sobald sie <paramref name="erwartet"/> entsprechen
    /// (Reihenfolge egal) – sonst die tatsächlichen.</summary>
    public IReadOnlyList<string> KopfFehlerAbwarten(params string[] erwartet)
    {
        Wait.Within(() => KopfFehler.Order().SequenceEqual(erwartet.Order()), Wait.DefaultTimeout);
        return KopfFehler;
    }

    private IReadOnlyList<string> KopfFehler => [.. fenster.FindAllById("Turnierkopf.Fehler").Select(f => f.NameOrEmpty())];

    /// <summary>Legt im Turnierkopf ein Turnier ohne Vorlage an. Danach ist es in der Liste gewählt.</summary>
    public void Anlegen(string name, string saison, int mannschaften)
    {
        var kopf = Kopf();
        kopf.Neu();
        kopf.WertSetzen("Name", name);
        kopf.WertSetzen("Saison", saison);
        kopf.WertSetzen("Mannschaftsanzahl", mannschaften.ToString());
        kopf.Speichern();
        Wait.Until(() => kopf.Wert("Name") == name && fenster.FindByName("Runden", FlaUI.Core.Definitions.ControlType.TabItem).IsEnabled,
            Wait.DefaultTimeout, $"'{name}' gespeichert und gewählt");
    }

    /// <summary>Legt über den Dialog „Aus Vorlage erstellen“ ein echtes Turnier an. Die Vorlage wird über Name und
    /// Variante bestimmt, weil mehrere Vorlagen gleich heißen („Mini-Turnier“ mit 4 und 8 Teams). Der Name kommt
    /// nach der Saison, weil die Saison den vorgeschlagenen Namen überschreibt.</summary>
    public void AusVorlageErstellen(string vorlage, string variante, string saison, string name)
    {
        Kopf();
        fenster.ActivateById("Befehlsleiste.AusVorlageErstellen");
        var dialog = Dialog.WaitFor(fenster, DIALOGTITEL);
        dialog.Element.FindById("AusVorlage.Vorlage").AsComboBox().SelectItem(vorlage, variante);
        dialog.Element.FindById("AusVorlage.Saison").Patterns.Value.Pattern.SetValue(saison);
        dialog.Element.FindById("AusVorlage.Name").Patterns.Value.Pattern.SetValue(name);
        dialog.Confirm();

        Wait.Until(() => Dialog.FindOpen(fenster).All(d => d.Title != DIALOGTITEL), Wait.DefaultTimeout,
            $"Dialog '{DIALOGTITEL}' geschlossen");
        hauptfenster.WarteBisRuhig();
    }

    private T Reiter<T>(string kopf, Func<T> erzeugen)
    {
        fenster.SelectTab(kopf);
        hauptfenster.WarteBisRuhig();
        return erzeugen();
    }
}
