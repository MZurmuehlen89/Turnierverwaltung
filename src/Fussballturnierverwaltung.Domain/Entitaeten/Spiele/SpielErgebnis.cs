using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;

namespace Fussballturnierverwaltung.Domain.Entitaeten.Spiele;

public class SpielErgebnis : BasisEntitaet
{
    public int SpielId { get; set; }
    public Spiel Spiel { get; set; } = null!;

    public int HeimToreRegulaer { get; set; }
    public int AuswaertsToreRegulaer { get; set; }
    public int? HeimToreVerlaengerung { get; set; }
    public int? AuswaertsToreVerlaengerung { get; set; }
    public int? HeimElfmetertore { get; set; }
    public int? AuswaertsElfmetertore { get; set; }

    public int? SiegerMannschaftId { get; set; }
    public Mannschaft? SiegerMannschaft { get; set; }
    public EntscheidungArt EntscheidungArt { get; set; }

    // Gesamttore inklusive Verlängerung (ohne Elfmeter, da die kein Teil des Spielstands sind)
    public int HeimToreGesamt => HeimToreRegulaer + (HeimToreVerlaengerung ?? 0);
    public int AuswaertsToreGesamt => AuswaertsToreRegulaer + (AuswaertsToreVerlaengerung ?? 0);
}
