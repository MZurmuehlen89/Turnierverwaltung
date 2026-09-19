using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;

namespace Fussballturnierverwaltung.Domain.Entitaeten.Turniere;

public class LandesQuote : BasisEntitaet
{
    public int TurnierInstanzId { get; set; }
    public TurnierInstanz TurnierInstanz { get; set; } = null!;

    public int LandId { get; set; }
    public Land Land { get; set; } = null!;

    public int Anzahl { get; set; }
}
