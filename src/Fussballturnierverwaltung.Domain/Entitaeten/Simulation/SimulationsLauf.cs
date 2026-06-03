using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;

namespace Fussballturnierverwaltung.Domain.Entitaeten.Simulation;

public class SimulationsLauf : BasisEntitaet
{
    public int TurnierInstanzId { get; set; }
    public TurnierInstanz TurnierInstanz { get; set; } = null!;
    public DateTime StartZeitpunkt { get; set; }
    public DateTime? EndZeitpunkt { get; set; }
    public int ZufallsSeed { get; set; }
    public string? Beschreibung { get; set; }
}
