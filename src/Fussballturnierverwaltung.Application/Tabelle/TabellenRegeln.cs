using Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Tabelle;

/// <summary>Wonach eine Tabelle gewertet wird: Punkte je Ausgang, die Ranking-Kriterien in ihrer Reihenfolge und für
/// das Kriterium <see cref="RankingRegelArt.Koeffizient"/> die Plätze in der Standard-Rangliste der Runde
/// (Mannschaft → Platz; ohne Angabe entscheidet es nichts), dazu ob der direkte Vergleich für die nach der
/// Mini-Tabelle noch gleichen Teams neu gerechnet wird (<see cref="RankingRegelSet.DirektenVergleichWiederholen"/>,
/// #628). Gemeinsame Eingabe von Tabellen-, Gruppen-, Verlaufs- und Prognoserechnung (#505).</summary>
public sealed record TabellenRegeln(
    PunkteRegel PunkteRegel,
    IReadOnlyList<RankingRegel> RankingRegeln,
    IReadOnlyDictionary<int, int>? RanglistenPlaetze = null,
    bool DirektenVergleichWiederholen = false);
