namespace Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;

public class Regelwerk : BasisEntitaet
{
    public string Name { get; set; } = string.Empty;
    public string Beschreibung { get; set; } = string.Empty;
    public DateOnly? GueltigVon { get; set; }
    public DateOnly? GueltigBis { get; set; }
    public int? RankingRegelSetId { get; set; }
    public RankingRegelSet? RankingRegelSet { get; set; }

    public ICollection<PunkteRegel> PunkteRegeln { get; set; } = new List<PunkteRegel>();
    public ICollection<EntscheidungsVerfahren> Entscheidungsverfahren { get; set; } = new List<EntscheidungsVerfahren>();
}
