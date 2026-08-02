using System.Windows.Input;

namespace Fussballturnierverwaltung.Wpf.Ribbon;

public interface IRibbonAware
{
    string RibbonTabTitel { get; }
    ICommand? NeuBefehl { get; }
    ICommand? SpeichernBefehl { get; }
    ICommand? AbbrechenBefehl { get; }
    ICommand? LoeschenBefehl { get; }

    /// <summary>
    /// Zusätzliche, editor-spezifische Ribbon-Gruppen im selben Tab, nach den
    /// Standardgruppen "Datensatz"/"Bearbeiten". Standardimplementierung liefert
    /// keine zusätzlichen Gruppen, damit bestehende Implementierungen unverändert bleiben.
    /// </summary>
    IReadOnlyList<RibbonGruppe> RibbonZusatzGruppen => [];

    /// <summary>
    /// Steuert die Sichtbarkeit der Standardgruppen "Datensatz"/"Bearbeiten" (Neu/Speichern/
    /// Abbrechen/Löschen). Standardimplementierung liefert true, damit bestehende
    /// Implementierungen unverändert bleiben — nur Composite-ViewModels mit eingebetteten
    /// Kind-Tabs ohne eigenes IRibbonAware (z.B. TurnierEditorViewModel, wenn der aktive Tab
    /// kein IRibbonAware implementiert) überschreiben dies auf false, damit keine command-losen
    /// Buttons angezeigt werden (Issue #173 — WPF zeigt Buttons ohne Command dauerhaft aktiv
    /// statt gegraut an).
    /// </summary>
    bool HatStandardAktionen => true;
}
