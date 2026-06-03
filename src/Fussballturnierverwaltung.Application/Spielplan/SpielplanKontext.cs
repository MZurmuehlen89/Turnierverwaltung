using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;

namespace Fussballturnierverwaltung.Application.Spielplan;

/// <summary>Eingabedaten für einen Spielplan-Generator.</summary>
public record SpielplanKontext(
    TurnierRunde Runde,
    IReadOnlyList<Mannschaft> Mannschaften,
    int StartSpielNummer);
