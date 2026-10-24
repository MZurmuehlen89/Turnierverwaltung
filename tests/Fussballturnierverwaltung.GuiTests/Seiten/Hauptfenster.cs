using FlaUI.Core.AutomationElements;
using GuiTest.Framework;

namespace Fussballturnierverwaltung.GuiTests.Seiten;

/// <summary>Seitenobjekt des Hauptfensters: Navigation, Seitentitel und Ladezustand. Kennt die AutomationIds aus
/// <c>MainWindow.xaml</c> (#521), damit die Tests fachlich lesbar bleiben.</summary>
public sealed class Hauptfenster(Window fenster)
{
    private const string SEITENTITEL = "Hauptfenster.Seitentitel";
    private const string BEFEHLSLEISTE = "Hauptfenster.Befehlsleiste";
    private const string BEREIT = "Bereit";

    private static readonly TimeSpan _ladezeit = TimeSpan.FromSeconds(30);

    // Zwischen zwei Ladeschritten kann der Zustand kurz auf „Bereit“ springen
    private static readonly TimeSpan _ruhezeit = TimeSpan.FromMilliseconds(300);

    public string Seitentitel => fenster.TryFindById(SEITENTITEL)?.Name ?? string.Empty;

    public string Ladezustand => fenster.FindById(BEFEHLSLEISTE).ItemStatus();

    public IReadOnlyList<Dialog> OffeneDialoge => Dialog.FindOpen(fenster);

    public void Oeffnen(Bereich bereich)
    {
        fenster.FindById(bereich.AutomationId).Activate();
        WarteBisGeladen(bereich.Titel);
    }

    /// <summary>Geladen heißt: Der erwartete Seitentitel steht da und kein Vorgang läuft mehr – und zwar für eine
    /// kurze Ruhezeit am Stück.</summary>
    public void WarteBisGeladen(string seitentitel) =>
        Wait.UntilStable(
            () => Seitentitel == seitentitel && Ladezustand == BEREIT,
            _ruhezeit,
            _ladezeit,
            $"Seite '{seitentitel}' geladen (Seitentitel und Ladezustand '{BEREIT}')");
}
