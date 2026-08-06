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
}
