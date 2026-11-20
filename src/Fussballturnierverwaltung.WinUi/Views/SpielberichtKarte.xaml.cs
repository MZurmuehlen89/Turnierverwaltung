using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;

namespace Fussballturnierverwaltung.WinUi.Views;

/// <summary>Eine Spielbericht-Karte (Spielkopf, Zeitstrahl, Zeitleiste), #247. Der DataContext ist eine
/// <see cref="ViewModels.SpielBeinAnsicht"/> (Kopf, Ereignisse, HatEreignisse). Mit eigenem Automation-Peer: mehrere
/// Karten stehen auf einer Seite und werden von den GUI-Tests je als Einheit gelesen (Muster <see cref="SpielKopfControl"/>);
/// die AutomationId steht an der Verwendung.</summary>
public sealed partial class SpielberichtKarte : UserControl
{
    public SpielberichtKarte()
    {
        InitializeComponent();
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new FrameworkElementAutomationPeer(this);
}
