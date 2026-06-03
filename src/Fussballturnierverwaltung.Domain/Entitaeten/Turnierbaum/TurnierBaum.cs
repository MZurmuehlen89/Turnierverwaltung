using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;

namespace Fussballturnierverwaltung.Domain.Entitaeten.Turnierbaum;

public class TurnierBaum : BasisEntitaet
{
    public int TurnierInstanzId { get; set; }
    public TurnierInstanz TurnierInstanz { get; set; } = null!;

    // Nullable: wird gesetzt, nachdem der Wurzelknoten angelegt wurde
    public int? WurzelKnotenId { get; set; }
    public TurnierBaumKnoten? WurzelKnoten { get; set; }

    public ICollection<TurnierBaumKnoten> Knoten { get; set; } = new List<TurnierBaumKnoten>();
}
