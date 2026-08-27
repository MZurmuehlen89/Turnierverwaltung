using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Domain.Entitaeten.Turniere;

public class TurnierVorlage : BasisEntitaet
{
    public string Name { get; set; } = string.Empty;
    public string Beschreibung { get; set; } = string.Empty;
    public int ErforderlicheMannschaftsanzahl { get; set; }
    public VorlagenKategorie VorlagenKategorie { get; set; }
    public bool IstHistorisch { get; set; }
    public DateOnly? GueltigVon { get; set; }
    public DateOnly? GueltigBis { get; set; }

    public byte[]? Logo { get; set; }

    public ICollection<TurnierInstanz> TurnierInstanzen { get; set; } = new List<TurnierInstanz>();
    public ICollection<VorlagenRunde> VorlagenRunden { get; set; } = new List<VorlagenRunde>();
}
