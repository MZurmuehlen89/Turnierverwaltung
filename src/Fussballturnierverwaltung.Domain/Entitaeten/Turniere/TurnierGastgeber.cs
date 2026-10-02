using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;

namespace Fussballturnierverwaltung.Domain.Entitaeten.Turniere;

/// <summary>Gastgeber eines Turniers (Issue #352): behält auf neutralem Platz seinen Heimvorteil.</summary>
public class TurnierGastgeber : BasisEntitaet
{
    public int TurnierInstanzId { get; set; }
    public TurnierInstanz TurnierInstanz { get; set; } = null!;

    public int MannschaftId { get; set; }
    public Mannschaft Mannschaft { get; set; } = null!;
}
