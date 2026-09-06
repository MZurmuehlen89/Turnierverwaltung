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

    /// <summary>Zusätzliche Optionen im Flyout-Menü des "Neu"-Buttons, z.B. "Aus Vorlage erstellen"
    /// (Issue #297-Nachzug) — leer lässt den Button wie gewohnt direkt <see cref="NeuBefehl"/> auslösen,
    /// nicht-leer hängt ein Flyout mit "Neu" + diesen Zusatzpunkten an (siehe MainWindow.xaml.cs,
    /// NeuFlyoutAktualisieren).</summary>
    IReadOnlyList<RibbonAktion> NeuUnterpunkte => [];

    bool HatStandardAktionen => true;
    bool HatLoeschenAktion => true;
}
