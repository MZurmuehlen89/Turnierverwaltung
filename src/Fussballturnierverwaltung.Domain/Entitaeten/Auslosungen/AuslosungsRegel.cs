using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;

public class AuslosungsRegel : BasisEntitaet
{
    public int AuslosungId { get; set; }
    public Auslosung Auslosung { get; set; } = null!;
    public AuslosungsRegelArt Regelart { get; set; }
    public string Beschreibung { get; set; } = string.Empty;
    public bool IstAktiv { get; set; }
}
