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

    /// <summary>Güte der Chance hinter einem Tor oder einer vergebenen Großchance (#431); null bei Karten, Taktik,
    /// Elfmeterschießen und allen Ereignissen von vor #431.</summary>
    public Chancenart? Chancenart { get; set; }

    /// <summary>#547: das Ereignis gehört zum Wiederholungsspiel, nicht zum ersten Spiel – seine Minuten zählen neu ab 0.</summary>
    public bool ImWiederholungsspiel { get; set; }
    public int? MannschaftId { get; set; }
    public Mannschaft? Mannschaft { get; set; }
    public string Beschreibung { get; set; } = string.Empty;
}
