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

    public StatistikReiter Statistik() => Reiter("Statistik", () => new StatistikReiter(fenster, hauptfenster));

    private T Reiter<T>(string kopf, Func<T> erzeugen)
    {
        fenster.SelectTab(kopf);
        hauptfenster.WarteBisRuhig();
        return erzeugen();
    }
}
