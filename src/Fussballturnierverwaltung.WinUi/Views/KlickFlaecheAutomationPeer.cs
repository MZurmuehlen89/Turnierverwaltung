using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;

namespace Fussballturnierverwaltung.WinUi.Views;

/// <summary>Macht eine <see cref="KlickFlaeche"/> für UI Automation zum Knopf (#522): Ohne Peer erscheint das
/// zugrunde liegende <c>Grid</c> nicht im UIA-Baum, GUI-Tests und Screenreader sähen nur die Texte darin. Den
/// Namen setzt die jeweilige Verwendung per <c>AutomationProperties.Name</c>.</summary>
internal sealed partial class KlickFlaecheAutomationPeer(KlickFlaeche besitzer)
    : FrameworkElementAutomationPeer(besitzer), IInvokeProvider
{
    public void Invoke() => besitzer.Ausloesen();

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Button;

    protected override string GetClassNameCore() => nameof(KlickFlaeche);

    protected override object GetPatternCore(PatternInterface patternInterface) =>
        patternInterface == PatternInterface.Invoke ? this : base.GetPatternCore(patternInterface);
}
