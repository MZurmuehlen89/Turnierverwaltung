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
}
