using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;

namespace Fussballturnierverwaltung.Domain.Entitaeten.Ranglisten;

public class RanglistenEintrag : BasisEntitaet
{
    public int RanglisteId { get; set; }
    public Rangliste Rangliste { get; set; } = null!;
    public int MannschaftId { get; set; }
    public Mannschaft Mannschaft { get; set; } = null!;
    public int Rang { get; set; }
    public int? Punkte { get; set; }
}
