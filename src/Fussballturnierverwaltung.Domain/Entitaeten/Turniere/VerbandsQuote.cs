using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;

namespace Fussballturnierverwaltung.Domain.Entitaeten.Turniere;

public class VerbandsQuote : BasisEntitaet
{
    public int TurnierInstanzId { get; set; }
    public TurnierInstanz TurnierInstanz { get; set; } = null!;

    public int VerbandId { get; set; }
    public Verband Verband { get; set; } = null!;

    public int Anzahl { get; set; }
}
