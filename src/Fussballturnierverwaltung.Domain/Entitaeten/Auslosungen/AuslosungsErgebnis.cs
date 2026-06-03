using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Entitaeten.Turnierbaum;

namespace Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;

public class AuslosungsErgebnis : BasisEntitaet
{
    public int AuslosungId { get; set; }
    public Auslosung Auslosung { get; set; } = null!;
    public int PaarungsNummer { get; set; }
    public int? HeimMannschaftId { get; set; }
    public Mannschaft? HeimMannschaft { get; set; }
    public int? AuswaertsMannschaftId { get; set; }
    public Mannschaft? AuswaertsMannschaft { get; set; }
    public int? ZugeordneterTurnierBaumKnotenId { get; set; }
    public TurnierBaumKnoten? ZugeordneterTurnierBaumKnoten { get; set; }
    public bool IstFreilos { get; set; }
}
