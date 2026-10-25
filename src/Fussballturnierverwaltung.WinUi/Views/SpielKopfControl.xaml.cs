using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;

namespace Fussballturnierverwaltung.WinUi.Views;

/// <summary>Issue #341: Spielkopf nach kicker.de-Vorbild — DataContext ist ein
/// <see cref="ViewModels.SpielKopf"/>.</summary>
public sealed partial class SpielKopfControl : UserControl
{
    public SpielKopfControl()
    {
        InitializeComponent();
    }

    // #536: Ein UserControl hat von sich aus keinen Automation-Peer, seine Texte hingen dann flach am nächsten
    // Vorfahren. Mit eigenem Peer bleibt jeder Kopf ein eigener Knoten im UIA-Baum, und die GUI-Tests können
    // Hinspiel-, Rückspiel- und Gesamtkopf getrennt lesen (AutomationId an der Verwendungsstelle).
    protected override AutomationPeer OnCreateAutomationPeer() => new FrameworkElementAutomationPeer(this);
}
