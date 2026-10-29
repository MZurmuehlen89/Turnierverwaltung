using Fussballturnierverwaltung.Domain.Entitaeten.Ranglisten;

namespace Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;

public class Verband : BasisEntitaet
{
    public string Name { get; set; } = string.Empty;
    public string Kurzname { get; set; } = string.Empty;

    public byte[]? Logo { get; set; }

    // Rangliste des Verbands (#572), etwa die FIFA-Weltrangliste (für alle Verbände dieselbe, gefiltert nach Verband).
    public int? RanglisteId { get; set; }
    public Rangliste? Rangliste { get; set; }

    public ICollection<Land> Laender { get; set; } = new List<Land>();
}
