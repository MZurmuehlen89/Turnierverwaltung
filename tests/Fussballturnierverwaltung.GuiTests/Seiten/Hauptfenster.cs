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
        fenster.ActivateById(bereich.AutomationId);
        WarteBisGeladen(bereich.Titel);
    }

    public MannschaftenSeite MannschaftenOeffnen()
    {
        Oeffnen(Bereich.Mannschaften);
        return new MannschaftenSeite(fenster, this);
    }

    public LaenderUndVerbaendeSeite LaenderUndVerbaendeOeffnen()
    {
        Oeffnen(Bereich.LaenderUndVerbaende);
        return new LaenderUndVerbaendeSeite(fenster, this);
    }

    public RegelwerkeSeite RegelwerkeOeffnen()
    {
        Oeffnen(Bereich.Regelwerke);
        return new RegelwerkeSeite(fenster, this);
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

    /// <summary>Legt aus <paramref name="vorlage"/> ein Turnier unter eindeutigem Namen an und öffnet es in der
    /// Turnierdurchführung.</summary>
    public TurnierdurchfuehrungSeite TurnierAusVorlageAnlegen(string vorlage, string variante)
    {
        string name = $"GuiTest {Guid.NewGuid().ToString("N")[..8]}";
        TurniereOeffnen().AusVorlageErstellen(vorlage, variante, "2027", name);

        var turnier = TurnierdurchfuehrungOeffnen();
        if (turnier.GewaehltesTurnier != name)
            throw new InvalidOperationException(
                $"Turnierdurchführung zeigt „{turnier.GewaehltesTurnier}“ statt des neuen Turniers „{name}“.");
        return turnier;
    }

    public EinzelspielSeite EinzelspielSimulationOeffnen()
    {
        Oeffnen(Bereich.EinzelspielSimulation);
        return new EinzelspielSeite(fenster);
    }

    public ImportExportSeite ImportExportOeffnen()
    {
        Oeffnen(Bereich.ImportExport);
        return new ImportExportSeite(fenster);
    }

    public DatenbankwartungSeite DatenbankwartungOeffnen()
    {
        Oeffnen(Bereich.Datenbankwartung);
        return new DatenbankwartungSeite(fenster);
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
