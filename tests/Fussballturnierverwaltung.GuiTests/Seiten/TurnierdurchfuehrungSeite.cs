using FlaUI.Core.AutomationElements;
using GuiTest.Framework;

namespace Fussballturnierverwaltung.GuiTests.Seiten;

/// <summary>Seitenobjekt „Turnierdurchführung“: das gewählte Turnier und die Reiter Auslosung, Spielplan,
/// Simulation und Statistik.</summary>
public sealed class TurnierdurchfuehrungSeite(Window fenster, Hauptfenster hauptfenster)
{
    /// <summary>Name des gewählten Turniers (Beschriftung des Auswahlknopfs).</summary>
    public string GewaehltesTurnier => fenster.FindById("Turnierdurchfuehrung.Turnier").NameOrEmpty();

    public AuslosungReiter Auslosung() => Reiter("Auslosung", () => new AuslosungReiter(fenster, hauptfenster));

    public SpielplanReiter Spielplan() => Reiter("Spielplan & Baum", () => new SpielplanReiter(fenster, hauptfenster));

    /// <summary>Simuliert <paramref name="runde"/> mit ihren <paramref name="anzahl"/> Spielen und liefert sie.</summary>
    public IReadOnlyList<SpielZeile> RundeSimulieren(string runde, int anzahl)
    {
        var spielplan = Spielplan();
        spielplan.RundeWaehlen(runde);
        spielplan.RundeSimulieren(anzahl);
        return spielplan.Spiele();
    }

    public IReadOnlyList<SpielZeile> SpieleDerRunde(string runde)
    {
        var spielplan = Spielplan();
        spielplan.RundeWaehlen(runde);
        return spielplan.Spiele();
    }

    /// <summary>„Ausführlich simulieren“ für das im Spielplan gewählte Spiel: springt in den Reiter „Simulation“ und
    /// simuliert die Partie dort.</summary>
    public SimulationsReiter AusfuehrlichSimulieren() => Sprung("Befehlsleiste.AusfuehrlichSimulieren");

    /// <summary>„Spielverlauf ansehen“ für das im Spielplan gewählte Spiel: springt in den Reiter „Simulation“ und
    /// zeigt das gespeicherte Protokoll, ohne neu zu simulieren.</summary>
    public SimulationsReiter SpielverlaufAnsehen() => Sprung("Befehlsleiste.SpielverlaufAnsehen");

    private SimulationsReiter Sprung(string befehl)
    {
        fenster.ActivateById(befehl);
        return new SimulationsReiter(fenster, hauptfenster);
    }

    public StatistikReiter Statistik() => Reiter("Statistik", () => new StatistikReiter(fenster, hauptfenster));

    private T Reiter<T>(string kopf, Func<T> erzeugen)
    {
        fenster.SelectTab(kopf);
        hauptfenster.WarteBisRuhig();
        return erzeugen();
    }
}
