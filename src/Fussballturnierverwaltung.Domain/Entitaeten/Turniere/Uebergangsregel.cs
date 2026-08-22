namespace Fussballturnierverwaltung.Domain.Entitaeten.Turniere;

public class Uebergangsregel : BasisEntitaet
{
    public int QuellRundeId { get; set; }
    public TurnierRunde QuellRunde { get; set; } = null!;

    public int ZielRundeId { get; set; }
    public TurnierRunde ZielRunde { get; set; } = null!;

    // null = gilt für alle Gruppen/Paarungen der Quellrunde
    public int? GruppeIndex { get; set; }

    public int PlatzierungVon { get; set; } // inklusiv
    public int PlatzierungBis { get; set; } // inklusiv

    // Wenn gesetzt: gruppenübergreifende Rangfolge-Regel (Issue #184) - GruppeIndex ist dann null,
    // PlatzierungVon == PlatzierungBis (genau eine Platzierung, z.B. "Platz 3"), Quellrunde muss
    // Gruppenphase sein. Bedeutung: die AnzahlBeste besten Teams an dieser Platzierung über alle
    // Gruppen hinweg (verglichen nach den Ranking-Regeln des Regelwerks) ziehen in die Zielrunde ein.
    public int? AnzahlBeste { get; set; }
}
