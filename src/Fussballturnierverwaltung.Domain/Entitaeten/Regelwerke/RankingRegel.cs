using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;

public class RankingRegel : BasisEntitaet
{
    public string Name { get; set; } = string.Empty;
    public string Beschreibung { get; set; } = string.Empty;
    public RankingRegelArt Regelart { get; set; }
    public Sortierrichtung Sortierrichtung { get; set; }
}
