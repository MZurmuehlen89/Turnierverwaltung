using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;

public class Land : BasisEntitaet
{
    public string Name { get; set; } = string.Empty;
    public string Kurzname { get; set; } = string.Empty;
    public string FifaCode { get; set; } = string.Empty;
    public Kontinent Kontinent { get; set; }
    public int VerbandId { get; set; }
    public Verband Verband { get; set; } = null!;

    public ICollection<Mannschaft> Mannschaften { get; set; } = new List<Mannschaft>();
}
