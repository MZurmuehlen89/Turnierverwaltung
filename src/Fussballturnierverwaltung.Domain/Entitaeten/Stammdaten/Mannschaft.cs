using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;

public class Mannschaft : BasisEntitaet
{
    public string Name { get; set; } = string.Empty;
    public string Kurzname { get; set; } = string.Empty;
    public Mannschaftsart Mannschaftsart { get; set; }
    public int LandId { get; set; }
    public Land Land { get; set; } = null!;

    public int OffensivRating { get; set; }
    public int MittelfeldRating { get; set; }
    public int DefensivRating { get; set; }
    public int Heimstaerke { get; set; }
    public int Auswaertsstaerke { get; set; }

    // Nicht gespeichert, da aus den Teilratings berechenbar
    public decimal GesamtRating => (OffensivRating + MittelfeldRating + DefensivRating) / 3m;
}
