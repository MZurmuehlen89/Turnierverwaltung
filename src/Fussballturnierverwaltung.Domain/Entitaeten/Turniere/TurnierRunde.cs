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
    public int? GruppenAnzahl { get; set; }
    public int RegelwerkId { get; set; }
    public Regelwerk Regelwerk { get; set; } = null!;

    public bool MitRueckspiel { get; set; }

    // Nur aktiv wenn MitRueckspiel=true: Sieger bei Aggregat-Gleichstand via Auswärtstore ermitteln.
    public bool AuswaertstorRegelAktiv { get; set; }

    public string? KonfigurationJson { get; set; }

    // Wenn true, wird der Eingang dieser Runde nicht gegen die unmittelbar vorangehende Runde in der Liste
    // geprüft, sondern über eine oder mehrere Uebergangsregeln gespeist (Issue #178).
    public bool EingangUeberUebergangsregeln { get; set; }

    // Wenn true, stehen die Paarungen dieser Runde von vornherein fest (Auslosungsart.Manuell) und werden
    // in der Turnierdefinition (Tab "Paarungen") statt in Turnierdurchführung → Auslosung festgelegt
    // (Issue #251-Folgefund) — ohne dieses Feld erschien dort jede Runde initial editierbar, bis anderswo
    // eine echte Auslosung angelegt wurde.
    public bool PaarungFestVorgegeben { get; set; }

    public ICollection<Spiel> Spiele { get; set; } = new List<Spiel>();

    // Übergangsregeln, die aus dieser Runde in eine (ggf. andere) Zielrunde führen.
    public ICollection<Uebergangsregel> Uebergangsregeln { get; set; } = new List<Uebergangsregel>();
}
