namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>Ziel des Kontextmenüs einer Turnierbaum-Karte (<see cref="Views.SpielKontextmenue"/>, #62): das Spiel im
/// Spielplan zeigen oder seine Simulation öffnen.</summary>
public interface ISpielAnzeige
{
    /// <summary>Wählt das Spiel im Spielplan aus und setzt den Cursor ins Ergebnisformular.</summary>
    Task SpielZeigenAsync(int rundeId, int spielId);

    /// <summary>Wechselt zur Simulation des Spiels — <paramref name="nurAnzeigen"/>: nur den Spielverlauf zeigen.</summary>
    void SimulationOeffnen(int rundeId, int spielId, bool nurAnzeigen);
}
