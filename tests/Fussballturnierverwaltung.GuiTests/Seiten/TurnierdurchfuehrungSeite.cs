using FlaUI.Core.AutomationElements;
using GuiTest.Framework;

namespace Fussballturnierverwaltung.GuiTests.Seiten;

/// <summary>Seitenobjekt „Turnierdurchführung“: das gewählte Turnier und die Reiter Auslosung, Spielplan und
/// Statistik.</summary>
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

    public StatistikReiter Statistik() => Reiter("Statistik", () => new StatistikReiter(fenster, hauptfenster));

    private T Reiter<T>(string kopf, Func<T> erzeugen)
    {
        fenster.SelectTab(kopf);
        hauptfenster.WarteBisRuhig();
        return erzeugen();
    }
}
