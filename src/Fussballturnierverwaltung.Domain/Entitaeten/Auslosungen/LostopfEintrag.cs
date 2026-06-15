using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;

namespace Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;

public class LostopfEintrag : BasisEntitaet
{
    public int LostopfId { get; set; }
    public Lostopf Lostopf { get; set; } = null!;

    // Entweder MannschaftId (echte Mannschaft) oder PlatzhalterName (Folgerunden-Slot)
    public int? MannschaftId { get; set; }
    public Mannschaft? Mannschaft { get; set; }
    public string? PlatzhalterName { get; set; }

    public int? SetzPosition { get; set; }
}
