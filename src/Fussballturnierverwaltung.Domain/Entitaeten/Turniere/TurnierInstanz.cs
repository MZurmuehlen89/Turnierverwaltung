using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;
using Fussballturnierverwaltung.Domain.Entitaeten.Simulation;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;

namespace Fussballturnierverwaltung.Domain.Entitaeten.Turniere;

public class TurnierInstanz : BasisEntitaet
{
    public string Name { get; set; } = string.Empty;
    public string Beschreibung { get; set; } = string.Empty;
    // Kurze Formatangabe, z. B. "32 Teams · 1998–2022" (Issue #367) — unterscheidet gleichnamige Vorlagen.
    public string Variante { get; set; } = string.Empty;
    public string SaisonOderJahr { get; set; } = string.Empty;
    public TurnierKategorie Kategorie { get; set; }
    public int? ZielMannschaftsanzahl { get; set; }
    public byte[]? Logo { get; set; }
    public DateOnly? Startdatum { get; set; }
    public DateOnly? Enddatum { get; set; }
    public TurnierStatus Status { get; set; }

    public int? StandardRegelwerkId { get; set; }
    public Regelwerk? StandardRegelwerk { get; set; }

    public int? VerbandId { get; set; }
    public Verband? Verband { get; set; }
    public int? LandId { get; set; }
    public Land? Land { get; set; }
    public MannschaftsartFilter MannschaftsartFilter { get; set; } = MannschaftsartFilter.Alle;

    // Optionaler Titelverteidiger (Issue #349) — kommt bei Runden mit TitelverteidigerInTopf1 in Topf 1.
    public int? TitelverteidigerId { get; set; }
    public Mannschaft? Titelverteidiger { get; set; }

    // Gastgeber (Issue #352) — behalten auf neutralem Platz (TurnierRunde.NeutralerSpielort) ihren Heimvorteil.
    // Mehrere möglich (WM 2026, EM 2008/2012); bei Vereinen z.B. der Klub, in dessen Stadion das Finale stattfindet.
    public ICollection<TurnierGastgeber> Gastgeber { get; set; } = new List<TurnierGastgeber>();

    public ICollection<TurnierRunde> Runden { get; set; } = new List<TurnierRunde>();
    public ICollection<Spiel> Spiele { get; set; } = new List<Spiel>();
    public ICollection<Auslosung> Auslosungen { get; set; } = new List<Auslosung>();
    public ICollection<SimulationsLauf> SimulationsLaeufe { get; set; } = new List<SimulationsLauf>();
    public ICollection<VerbandsQuote> VerbandsQuoten { get; set; } = new List<VerbandsQuote>();
    public ICollection<LandesQuote> LandesQuoten { get; set; } = new List<LandesQuote>();
}
