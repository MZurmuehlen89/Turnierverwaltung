using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;

public class EntscheidungsVerfahren : BasisEntitaet
{
    public int RegelwerkId { get; set; }
    public Regelwerk Regelwerk { get; set; } = null!;
    public int Reihenfolge { get; set; }
    public VerfahrenArt VerfahrenArt { get; set; }
}
