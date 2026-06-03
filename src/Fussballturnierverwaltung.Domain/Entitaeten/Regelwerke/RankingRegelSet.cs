namespace Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;

public class RankingRegelSet : BasisEntitaet
{
    public string Name { get; set; } = string.Empty;
    public string Beschreibung { get; set; } = string.Empty;

    public ICollection<RankingRegelSetEintrag> Eintraege { get; set; } = new List<RankingRegelSetEintrag>();
}
