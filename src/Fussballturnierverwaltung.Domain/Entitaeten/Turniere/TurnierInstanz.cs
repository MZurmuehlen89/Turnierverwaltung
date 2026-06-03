using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;
using Fussballturnierverwaltung.Domain.Entitaeten.Simulation;

namespace Fussballturnierverwaltung.Domain.Entitaeten.Turniere;

public class TurnierInstanz : BasisEntitaet
{
    public int? TurnierVorlageId { get; set; }
    public TurnierVorlage? TurnierVorlage { get; set; }
    public string Name { get; set; } = string.Empty;
    public string SaisonOderJahr { get; set; } = string.Empty;
    public DateOnly? Startdatum { get; set; }
    public DateOnly? Enddatum { get; set; }
    public TurnierStatus Status { get; set; }

    public ICollection<TurnierRunde> Runden { get; set; } = new List<TurnierRunde>();
    public ICollection<Spiel> Spiele { get; set; } = new List<Spiel>();
    public ICollection<Auslosung> Auslosungen { get; set; } = new List<Auslosung>();
    public ICollection<SimulationsLauf> SimulationsLaeufe { get; set; } = new List<SimulationsLauf>();
}
