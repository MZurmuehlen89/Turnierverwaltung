using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;

namespace Fussballturnierverwaltung.Domain.Entitaeten.Spiele;

public class SpielEreignis : BasisEntitaet
{
    public int SpielId { get; set; }
    public Spiel Spiel { get; set; } = null!;
    public int Minute { get; set; }
    public EreignisArt EreignisArt { get; set; }
    public int? MannschaftId { get; set; }
    public Mannschaft? Mannschaft { get; set; }
    public string Beschreibung { get; set; } = string.Empty;
}
