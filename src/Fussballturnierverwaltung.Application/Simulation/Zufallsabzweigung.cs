namespace Fussballturnierverwaltung.Application.Simulation;

/// <summary>
/// Zufallsquelle, die selbst bestimmt, welchen eigenen Strom die Spielkontrolle bekommt (#674). Ohne diese Schnittstelle
/// zweigt der Spielablauf den Strom per Startwert aus dem Zufall des Spiels ab. Die vorgegebenen Zufallsfolgen der Tests
/// liefern hier einen ruhenden Strom: Ihre Würfe je Minute bleiben so unberührt, und die Spielkontrolle steht still.
/// </summary>
public interface IZufallsabzweigung
{
    Random Abzweigen();
}
