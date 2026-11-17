namespace Fussballturnierverwaltung.Domain.Enums;

/// <summary>Was in den Spielen einer Runde auf dem Spiel steht (#668) – verschiebt in der Simulation, wie riskant beide
/// Teams ins Spiel gehen: im Finale vorsichtig, im Spiel um Platz 3 offen. Die Bedeutung je Team aus der Tabellenlage
/// in Liga und Gruppe gehört zu #176.</summary>
public enum Spielbedeutung
{
    /// <summary>Es geht um wenig, es wird offen gespielt (Spiel um Platz 3).</summary>
    Gering = -1,
    Normal = 0,
    /// <summary>Es geht um alles, beide Teams spielen vorsichtig (Finale).</summary>
    Hoch   = 1
}
