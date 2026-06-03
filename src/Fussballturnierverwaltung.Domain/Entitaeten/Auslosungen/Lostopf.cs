namespace Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;

public class Lostopf : BasisEntitaet
{
    public int AuslosungId { get; set; }
    public Auslosung Auslosung { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public int Reihenfolge { get; set; }

    public ICollection<LostopfEintrag> Eintraege { get; set; } = new List<LostopfEintrag>();
}
