using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;

namespace Fussballturnierverwaltung.Domain.Entitaeten.Turnierbaum;

public class TurnierBaumKnoten : BasisEntitaet
{
    public int TurnierBaumId { get; set; }
    public TurnierBaum TurnierBaum { get; set; } = null!;
    public int? ElternKnotenId { get; set; }
    public TurnierBaumKnoten? ElternKnoten { get; set; }
    public int? LinkerKindKnotenId { get; set; }
    public TurnierBaumKnoten? LinkerKindKnoten { get; set; }
    public int? RechterKindKnotenId { get; set; }
    public TurnierBaumKnoten? RechterKindKnoten { get; set; }
    public int? SpielId { get; set; }
    public Spiel? Spiel { get; set; }
    public int Runde { get; set; }
}
