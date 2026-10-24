using FlaUI.Core.AutomationElements;
using GuiTest.Framework;

namespace Fussballturnierverwaltung.GuiTests.Seiten;

/// <summary>Seitenobjekt des Hauptfensters: Navigation, Seitentitel und Ladezustand. Kennt die AutomationIds aus
/// <c>MainWindow.xaml</c> (#521), damit die Tests fachlich lesbar bleiben.</summary>
public sealed class Hauptfenster(Window fenster)
{
    private const string BEFEHLSLEISTE = "Hauptfenster.Befehlsleiste";
    private const string BEREIT = "Bereit";

    private static readonly TimeSpan _ladezeit = TimeSpan.FromSeconds(30);

    // Zwischen zwei Ladeschritten kann der Zustand kurz auf „Bereit“ springen
    private static readonly TimeSpan _ruhezeit = TimeSpan.FromMilliseconds(300);

    /// <summary>Titel der aktuellen Seite – als Name der Befehlsleiste, weil der sichtbare Titel auf Seiten ohne
    /// Ribbon-Bereich ausgeblendet ist und dann im UIA-Baum fehlt.</summary>
    public string Seitentitel => Befehlsleiste.NameOrEmpty();

    public string Ladezustand => Befehlsleiste.ItemStatus();

    public IReadOnlyList<Dialog> OffeneDialoge => Dialog.FindOpen(fenster);

    private AutomationElement Befehlsleiste => fenster.FindById(BEFEHLSLEISTE);

    public void Oeffnen(Bereich bereich)
    {
        fenster.FindById(bereich.AutomationId).Activate();
        WarteBisGeladen(bereich.Titel);
    }

    public TurnierlisteSeite TurniereOeffnen()
    {
        Oeffnen(Bereich.Turniere);
        return new TurnierlisteSeite(fenster, this);
    }

    public TurnierdurchfuehrungSeite TurnierdurchfuehrungOeffnen()
    {
        Oeffnen(Bereich.Turnierdurchfuehrung);
        return new TurnierdurchfuehrungSeite(fenster, this);
    }

    /// <summary>Geladen heißt: Der erwartete Seitentitel steht da und kein Vorgang läuft mehr – und zwar für eine
    /// kurze Ruhezeit am Stück.</summary>
    public void WarteBisGeladen(string seitentitel) =>
        Wait.UntilStable(
            () => Seitentitel == seitentitel && Ladezustand == BEREIT,
            _ruhezeit,
            _ladezeit,
            $"Seite '{seitentitel}' geladen (Seitentitel und Ladezustand '{BEREIT}')");

    /// <summary>Wartet, bis nach einer Aktion innerhalb einer Seite kein Vorgang mehr läuft.</summary>
    public void WarteBisRuhig() =>
        Wait.UntilStable(() => Ladezustand == BEREIT, _ruhezeit, _ladezeit, $"Ladezustand '{BEREIT}'");
}
