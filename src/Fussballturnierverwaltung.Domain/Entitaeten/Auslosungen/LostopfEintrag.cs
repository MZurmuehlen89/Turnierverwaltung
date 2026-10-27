using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;

namespace Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;

public class LostopfEintrag : BasisEntitaet
{
    public int LostopfId { get; set; }
    public Lostopf Lostopf { get; set; } = null!;

    // Genau eine der drei Belegungsarten ist gesetzt: MannschaftId (echte Mannschaft),
    // PlatzhalterName (Folgerunden-Slot, Freitext) oder UebergangsregelId+Platzierung
    // (Folgerunden-Slot, strukturiert über Issue #130 - Andockpunkt für #131/#132).
    public int? MannschaftId { get; set; }
    public Mannschaft? Mannschaft { get; set; }
    public string? PlatzhalterName { get; set; }

    public int? UebergangsregelId { get; set; }
    public Uebergangsregel? Uebergangsregel { get; set; }
    public int? Platzierung { get; set; }
}
