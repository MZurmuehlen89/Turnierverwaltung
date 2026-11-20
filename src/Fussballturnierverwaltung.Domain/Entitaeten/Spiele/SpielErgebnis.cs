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
    /// <summary>#547: Wiederholungsspiel als Teil des Ergebnisses – Tore nach 90 Minuten und ggf. in der Verlängerung des
    /// Wiederholungsspiels, aus Sicht des Heimteams dieses Spiels (auch bei getauschtem Heimrecht). null = kein
    /// Wiederholungsspiel (alle Bestandsdaten). <see cref="HeimToreGesamt"/> bleibt der Stand des ersten Spiels.</summary>
    public int? HeimToreWiederholung { get; set; }
    public int? AuswaertsToreWiederholung { get; set; }
    public int? HeimToreWiederholungVerlaengerung { get; set; }
    public int? AuswaertsToreWiederholungVerlaengerung { get; set; }
    public int? HalbzeitHeimTore { get; set; }
    public int? HalbzeitAuswaertsTore { get; set; }

    public int? SiegerMannschaftId { get; set; }
    public Mannschaft? SiegerMannschaft { get; set; }
    public EntscheidungArt EntscheidungArt { get; set; }

    /// <summary>Ausführlich simuliert mit Chancenmodell (#431): Tore und Großchancen tragen ihre Chancenart. Manuell
    /// erfasste Ergebnisse und Simulationen von davor haben keine Chancendaten.</summary>
    public bool MitChancendaten { get; set; }

    // Gesamttore inklusive Verlängerung (ohne Elfmeter, da die kein Teil des Spielstands sind)
    public int HeimToreGesamt => HeimToreRegulaer + (HeimToreVerlaengerung ?? 0);
    public int AuswaertsToreGesamt => AuswaertsToreRegulaer + (AuswaertsToreVerlaengerung ?? 0);
}
