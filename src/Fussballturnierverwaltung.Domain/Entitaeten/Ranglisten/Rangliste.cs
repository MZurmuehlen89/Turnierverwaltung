namespace Fussballturnierverwaltung.Domain.Entitaeten.Ranglisten;

public class Rangliste : BasisEntitaet
{
    public string Name { get; set; } = string.Empty;
    public DateOnly Datum { get; set; }
    public string Quelle { get; set; } = string.Empty;

    public ICollection<RanglistenEintrag> Eintraege { get; set; } = new List<RanglistenEintrag>();
}
