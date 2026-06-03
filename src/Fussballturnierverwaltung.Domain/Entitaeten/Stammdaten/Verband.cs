namespace Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;

public class Verband : BasisEntitaet
{
    public string Name { get; set; } = string.Empty;
    public string Kurzname { get; set; } = string.Empty;

    public ICollection<Land> Laender { get; set; } = new List<Land>();
}
