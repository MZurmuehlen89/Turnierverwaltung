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
    int? Spieltag = null,
    /// <summary>Team-IDs, die in einer vorherigen Runde bereits ein Freilos hatten — für SchweizerSystem
    /// zur Freilos-Rotation (kein Team soll mehrfach aussetzen, solange andere Teams noch nie ausgesetzt haben).</summary>
    IReadOnlySet<int>? FreilosHistorie = null,
    /// <summary>Bisherige Heimspiele je Team-ID — für SchweizerSystem zum Farbausgleich (das Team mit
    /// weniger Heimspielen bekommt bei einer neuen Paarung den Heimplatz).</summary>
    IReadOnlyDictionary<int, int>? HeimBilanz = null);
