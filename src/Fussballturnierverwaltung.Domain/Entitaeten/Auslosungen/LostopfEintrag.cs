using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;

namespace Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;

public class LostopfEintrag : BasisEntitaet
{
    public int LostopfId { get; set; }
    public Lostopf Lostopf { get; set; } = null!;
    public int MannschaftId { get; set; }
    public Mannschaft Mannschaft { get; set; } = null!;
    public int? SetzPosition { get; set; }
}
