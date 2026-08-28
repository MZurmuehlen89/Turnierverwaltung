using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;

namespace Fussballturnierverwaltung.Domain.Entitaeten.Spiele;

public class Spiel : BasisEntitaet
{
    public int TurnierInstanzId { get; set; }
    public TurnierInstanz TurnierInstanz { get; set; } = null!;
    public int TurnierRundeId { get; set; }
    public TurnierRunde TurnierRunde { get; set; } = null!;
    public int Spielnummer { get; set; }
    public int? Spieltag { get; set; }
    public DateOnly? Datum { get; set; }
    public int? HeimMannschaftId { get; set; }
    public Mannschaft? HeimMannschaft { get; set; }
    public string? HeimPlatzhalterName { get; set; }
    public int? AuswaertsMannschaftId { get; set; }
    public Mannschaft? AuswaertsMannschaft { get; set; }
    public string? AuswaertsPlatzhalterName { get; set; }
    public SpielStatus Status { get; set; }
    public SimulationsModus SimulationsModus { get; set; }
    public bool IstRueckspiel { get; set; }
    public int? HinspielId { get; set; }
    public Spiel? Hinspiel { get; set; }

    public int? HeimQuellSpielId { get; set; }
    public Spiel? HeimQuellSpiel { get; set; }
    public SpielQuellErgebnisTyp? HeimQuellErgebnis { get; set; }

    public int? AuswaertsQuellSpielId { get; set; }
    public Spiel? AuswaertsQuellSpiel { get; set; }
    public SpielQuellErgebnisTyp? AuswaertsQuellErgebnis { get; set; }

    public SpielErgebnis? Ergebnis { get; set; }
    public ICollection<SpielEreignis> Ereignisse { get; set; } = new List<SpielEreignis>();
}
