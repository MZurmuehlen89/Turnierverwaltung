using FlaUI.Core.AutomationElements;
using GuiTest.Framework;

namespace Fussballturnierverwaltung.GuiTests.Seiten;

/// <summary>Reiter „Spielplan &amp; Baum“: Runde wählen, simulieren, Spiele und Fortschritt lesen.</summary>
public sealed class SpielplanReiter(Window fenster, Hauptfenster hauptfenster)
{
    private const string ALLE_SPIELTAGE = "Alle Spieltage";

    private static readonly TimeSpan _simulationsZeit = TimeSpan.FromSeconds(60);

    /// <summary>(gespielt, gesamt) über alle Runden, aus „Runde x von y · a / b Spiele gespielt“. Während die Seite nach
    /// einer Aktion neu lädt, ist die Anzeige kurz leer – gelesen wird, bis sie wieder dasteht.</summary>
    public (int Gespielt, int Gesamt) Fortschritt =>
        Wait.UntilHasValue(() => AktuellerFortschritt, Wait.DefaultTimeout, "Fortschritt lesbar");

    private (int Gespielt, int Gesamt)? AktuellerFortschritt =>
        Spielstand.Fortschritt(fenster.TextOf("Spielplan.Fortschritt"));

    public void RundeWaehlen(string runde)
    {
        fenster.FindById("Spielplan.Runden").FindByName(runde, FlaUI.Core.Definitions.ControlType.ListItem).Activate();
        hauptfenster.WarteBisRuhig();
    }

    /// <summary>Simuliert die gewählte Runde und wartet, bis ihre <paramref name="spieleInDerRunde"/> Spiele im
    /// Fortschritt angekommen sind.</summary>
    public void RundeSimulieren(int spieleInDerRunde)
    {
        int erwartet = Fortschritt.Gespielt + spieleInDerRunde;
        fenster.InvokeMenuItem("Befehlsleiste.Simulieren", "Befehlsleiste.SimulierenRunde");
        Wait.Until(() => AktuellerFortschritt?.Gespielt == erwartet, _simulationsZeit, $"{erwartet} Spiele gespielt");
        hauptfenster.WarteBisRuhig();
    }

    /// <summary>Die Spiele der gewählten Runde, auch die von einer virtualisierenden Liste noch nicht erzeugten. Runden
    /// mit mehreren Spieltagen zeigen sonst nur einen Spieltag, darum zuerst „Alle Spieltage“.</summary>
    public IReadOnlyList<SpielZeile> Spiele()
    {
        if (fenster.TryFindById("Spielplan.Spieltag") is { } spieltag)
        {
            spieltag.AsComboBox().SelectItem(ALLE_SPIELTAGE);
            hauptfenster.WarteBisRuhig();
        }
        return fenster.FindById("Spielplan.Spiele").ReadAllItems(zeile =>
            zeile.TryFindById("Spiel.Heim") is null
                ? null
                : new SpielZeile(
                    zeile.TextOf("Spiel.Heim"),
                    zeile.TextOf("Spiel.Ergebnis"),
                    zeile.TextOf("Spiel.ErgebnisZusatz"),
                    zeile.TextOf("Spiel.Auswaerts"),
                    zeile.TextOf("Spiel.Status")));
    }
}
