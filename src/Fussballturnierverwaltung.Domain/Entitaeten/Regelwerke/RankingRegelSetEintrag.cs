namespace Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;

public class RankingRegelSetEintrag : BasisEntitaet
{
    public int RankingRegelSetId { get; set; }
    public RankingRegelSet RankingRegelSet { get; set; } = null!;
    public int RankingRegelId { get; set; }
    public RankingRegel RankingRegel { get; set; } = null!;
    public int Reihenfolge { get; set; }
}
