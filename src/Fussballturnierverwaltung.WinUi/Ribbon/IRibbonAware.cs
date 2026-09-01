using System.Windows.Input;

namespace Fussballturnierverwaltung.WinUi.Ribbon;

/// <summary>Spike-Nachbau (#278) des WPF-Originals in src\Fussballturnierverwaltung.Wpf\Ribbon\IRibbonAware.cs —
/// bewusst unabhängige Kopie, da beide Startprojekte keine Abhängigkeit zueinander haben dürfen. Hier gegen
/// eine CommandBar (statt Fluent.Ribbon) gebunden — WinUI 3 hat kein first-party Ribbon-Control mehr.</summary>
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
