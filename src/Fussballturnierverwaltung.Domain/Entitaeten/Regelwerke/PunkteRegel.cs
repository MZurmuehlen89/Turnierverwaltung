namespace Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;

public class PunkteRegel : BasisEntitaet
{
    public int RegelwerkId { get; set; }
    public Regelwerk Regelwerk { get; set; } = null!;

    public int PunkteFuerSieg { get; set; }
    public int PunkteFuerUnentschieden { get; set; }
    public int PunkteFuerNiederlage { get; set; }
}
