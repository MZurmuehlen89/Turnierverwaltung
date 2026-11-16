using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;

namespace Fussballturnierverwaltung.Domain.Entitaeten.Spiele;

public class SpielEreignis : BasisEntitaet
{
    public int SpielId { get; set; }
    public Spiel Spiel { get; set; } = null!;
    public int Minute { get; set; }

    /// <summary>Minute der Nachspielzeit (#361): ein Ereignis in „90+4'“ hat <see cref="Minute"/> = 90 und
    /// Nachspielminute = 4. Auswertungen über <see cref="Minute"/> bleiben dadurch gültig; null = keine
    /// Nachspielzeit (auch alle Ereignisse von vor #361).</summary>
    public int? Nachspielminute { get; set; }
    public EreignisArt EreignisArt { get; set; }
    public int? MannschaftId { get; set; }
    public Mannschaft? Mannschaft { get; set; }
    public string Beschreibung { get; set; } = string.Empty;
}
