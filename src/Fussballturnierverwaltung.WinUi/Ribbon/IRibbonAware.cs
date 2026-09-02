using System.Windows.Input;

namespace Fussballturnierverwaltung.WinUi.Ribbon;

/// <summary>Pendant zum WPF-Original in src\Fussballturnierverwaltung.Wpf\Ribbon\IRibbonAware.cs —
/// bewusst unabhängige Kopie, da beide Startprojekte keine Abhängigkeit zueinander haben dürfen. Hier gegen
/// eine CommandBar (statt Fluent.Ribbon) gebunden — WinUI 3 hat kein first-party Ribbon-Control mehr.</summary>
public interface IRibbonAware
{
    string RibbonTabTitel { get; }
    ICommand? NeuBefehl { get; }
    ICommand? SpeichernBefehl { get; }
    ICommand? AbbrechenBefehl { get; }
    ICommand? LoeschenBefehl { get; }

    /// <summary>Zusätzliche, editor-spezifische Ribbon-Gruppen, nach den Standardaktionen
    /// Neu/Speichern/Abbrechen/Löschen. Standardimplementierung liefert keine zusätzlichen
    /// Gruppen, damit bestehende Implementierungen unverändert bleiben.</summary>
    IReadOnlyList<RibbonGruppe> RibbonZusatzGruppen => [];

    bool HatStandardAktionen => true;
    bool HatLoeschenAktion => true;
}
