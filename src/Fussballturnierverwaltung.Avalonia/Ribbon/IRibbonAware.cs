using System.Windows.Input;

namespace Fussballturnierverwaltung.AvaloniaSpike.Ribbon;

/// <summary>Spike-Nachbau (#278) des WPF-Originals in src\Fussballturnierverwaltung.Wpf\Ribbon\IRibbonAware.cs —
/// bewusst unabhängige Kopie, da beide Startprojekte keine Abhängigkeit zueinander haben dürfen.</summary>
public interface IRibbonAware
{
    string RibbonTabTitel { get; }
    ICommand? NeuBefehl { get; }
    ICommand? SpeichernBefehl { get; }
    ICommand? AbbrechenBefehl { get; }
    ICommand? LoeschenBefehl { get; }

    bool HatStandardAktionen => true;
    bool HatLoeschenAktion => true;
}
