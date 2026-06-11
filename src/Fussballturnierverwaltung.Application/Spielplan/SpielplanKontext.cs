using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;

namespace Fussballturnierverwaltung.Application.Spielplan;

/// <summary>Eingabedaten für einen Spielplan-Generator.</summary>
public record SpielplanKontext(
    TurnierRunde Runde,
    IReadOnlyList<Mannschaft> Mannschaften,
    int StartSpielNummer,
    IReadOnlyList<(int HeimId, int AuswaertsId)>? Paarungen = null,
    /// <summary>Bereits gespielte Paarungen (reihenfolge-unabhängig) — für SchweizerSystem zur Rematch-Vermeidung.</summary>
    IReadOnlyList<(int TeamAId, int TeamBId)>? BereitsGespielt = null,
    /// <summary>Spieltag-Nummer für diesen Generierungslauf (null = Generator entscheidet selbst).</summary>
    int? Spieltag = null);
