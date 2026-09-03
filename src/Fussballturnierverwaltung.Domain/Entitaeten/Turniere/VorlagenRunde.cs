using Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Domain.Entitaeten.Turniere;

public class VorlagenRunde : BasisEntitaet
{
    public int TurnierVorlageId { get; set; }
    public TurnierVorlage TurnierVorlage { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public int Reihenfolge { get; set; }
    public Rundenart Rundenart { get; set; }
    public int EingangsMannschaftsanzahl { get; set; }
    public int AusgangsMannschaftsanzahl { get; set; }
    public int? GruppenAnzahl { get; set; }
    public bool MitRueckspiel { get; set; }
    public bool AuswaertstorRegelAktiv { get; set; }
    public int? RegelwerkId { get; set; }
    public Regelwerk? Regelwerk { get; set; }

    // Wenn true, stehen die Paarungen dieser Runde von vornherein fest und werden im Vorlagen-Tab
    // "Paarungen" statt per Auslosung festgelegt (Issue #277, Vorlagen-Pendant zu
    // TurnierRunde.PaarungFestVorgegeben). Für die erste Runde einer Vorlage nicht verfügbar — dort
    // existieren noch keine eingehenden Übergangsregeln, aus denen sich Platzhalter ableiten ließen, und
    // jedes reale Turnier durchläuft dort zwingend eine echte Auslosung.
    public bool PaarungFestVorgegeben { get; set; }

    public ICollection<VorlagenUebergangsRegel> AusgehendeUebergangsregeln { get; set; } = new List<VorlagenUebergangsRegel>();
    public ICollection<VorlagenPaarung> Paarungen { get; set; } = new List<VorlagenPaarung>();
}
