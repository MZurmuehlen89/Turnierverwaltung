namespace Fussballturnierverwaltung.Domain.Entitaeten.Ranglisten;

public class Rangliste : BasisEntitaet
{
    public string Name { get; set; } = string.Empty;
    public DateOnly Datum { get; set; }
    public string Quelle { get; set; } = string.Empty;

    // Ranglisten-Quelle, aus der die Rangliste importiert wurde (Issue #349) — darüber findet die Auslosung
    // die neueste Rangliste zur Standard-Quelle einer Runde. null = manuell gepflegt oder Quelle gelöscht.
    public int? RanglistenQuelleId { get; set; }
    public RanglistenQuelle? RanglistenQuelle { get; set; }

    public ICollection<RanglistenEintrag> Eintraege { get; set; } = new List<RanglistenEintrag>();
}
