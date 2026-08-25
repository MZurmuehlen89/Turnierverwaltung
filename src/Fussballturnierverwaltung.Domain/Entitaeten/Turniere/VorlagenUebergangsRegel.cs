namespace Fussballturnierverwaltung.Domain.Entitaeten.Turniere;

/// <summary>Vorlagen-Variante der <see cref="Uebergangsregel"/> (Issue #166) — referenziert
/// <see cref="VorlagenRunde"/> statt <see cref="TurnierRunde"/>, wird beim Laden einer Vorlage im
/// Runden-Editor in echte Übergangsregeln zwischen den neu angelegten Turnierrunden übersetzt.</summary>
public class VorlagenUebergangsRegel : BasisEntitaet
{
    public int QuellVorlagenRundeId { get; set; }
    public VorlagenRunde QuellVorlagenRunde { get; set; } = null!;

    public int ZielVorlagenRundeId { get; set; }
    public VorlagenRunde ZielVorlagenRunde { get; set; } = null!;

    // null = gilt für alle Gruppen/Paarungen der Quellrunde
    public int? GruppeIndex { get; set; }

    public int PlatzierungVon { get; set; } // inklusiv
    public int PlatzierungBis { get; set; } // inklusiv

    // Wenn gesetzt: gruppenübergreifende Rangfolge-Regel (Issue #217, Vorlagen-Pendant zu Issue #184) -
    // GruppeIndex ist dann null, PlatzierungVon == PlatzierungBis (genau eine Platzierung, z.B. "Platz 3"),
    // Quellrunde muss Gruppenphase sein. Bedeutung: die AnzahlBeste besten Teams an dieser Platzierung über
    // alle Gruppen hinweg ziehen in die Zielrunde ein — wird beim Laden der Vorlage in eine echte
    // Uebergangsregel mit AnzahlBeste übersetzt.
    public int? AnzahlBeste { get; set; }
}
