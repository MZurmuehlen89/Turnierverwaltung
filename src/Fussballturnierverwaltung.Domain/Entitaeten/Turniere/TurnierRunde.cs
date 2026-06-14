using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;

namespace Fussballturnierverwaltung.Domain.Entitaeten.Turniere;

public class TurnierRunde : BasisEntitaet
{
    public int TurnierInstanzId { get; set; }
    public TurnierInstanz TurnierInstanz { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public int Reihenfolge { get; set; }
    public Rundenart Rundenart { get; set; }
    public int EingangsMannschaftsanzahl { get; set; }
    public int AusgangsMannschaftsanzahl { get; set; }
    public int RegelwerkId { get; set; }
    public Regelwerk Regelwerk { get; set; } = null!;

    public bool MitRueckspiel { get; set; }

    // Nur aktiv wenn MitRueckspiel=true: Sieger bei Aggregat-Gleichstand via Auswärtstore ermitteln.
    public bool AuswaertstorRegelAktiv { get; set; }

    public string? KonfigurationJson { get; set; }

    public ICollection<Spiel> Spiele { get; set; } = new List<Spiel>();
}
