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
    /// Steuert die Sichtbarkeit der "Datensatz"-Gruppe (Neu/Speichern/Abbrechen). Standard-
    /// implementierung liefert true, damit bestehende Implementierungen unverändert bleiben —
    /// nur ViewModels ohne echte Neu/Speichern/Abbrechen-Semantik (z.B. SimulationsViewModel,
    /// Composite-ViewModels ohne aktives IRibbonAware-Kind) überschreiben dies auf false, damit
    /// keine command-losen Buttons angezeigt werden (Issue #173 — WPF zeigt Buttons ohne Command
    /// dauerhaft aktiv statt gegraut an).
    /// </summary>
    bool HatStandardAktionen => true;

    /// <summary>
    /// Steuert unabhängig von <see cref="HatStandardAktionen"/> die Sichtbarkeit der
    /// "Bearbeiten"-Gruppe (Löschen) — z.B. SimulationsViewModel hat keine Datensatz-Aktionen,
    /// aber sehr wohl eine sinnvolle Löschen-Aktion (Ergebnis zurücksetzen, Issue #164).
    /// Standardimplementierung liefert true, damit bestehende Implementierungen unverändert
    /// bleiben.
    /// </summary>
    bool HatLoeschenAktion => true;
}
