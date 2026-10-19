using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;

namespace Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;

public class Auslosung : BasisEntitaet
{
    public int TurnierInstanzId { get; set; }
    public TurnierInstanz TurnierInstanz { get; set; } = null!;
    public int TurnierRundeId { get; set; }
    public TurnierRunde TurnierRunde { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public DateOnly? Datum { get; set; }
    public Auslosungsart Auslosungsart { get; set; }

    // Nur Gruppenphase (Issue #475): wie die Position innerhalb der Gruppe entsteht.
    public Gruppenposition Gruppenposition { get; set; }
    public AuslosungsStatus Status { get; set; }
    public bool IstAutomatisch { get; set; }

    public ICollection<Lostopf> Lostoepfe { get; set; } = new List<Lostopf>();
    public ICollection<AuslosungsRegel> Regeln { get; set; } = new List<AuslosungsRegel>();
    public ICollection<AuslosungsErgebnis> Ergebnisse { get; set; } = new List<AuslosungsErgebnis>();
}
