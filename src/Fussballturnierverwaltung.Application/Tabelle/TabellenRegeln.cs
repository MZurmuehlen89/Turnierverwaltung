using Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Tabelle;

/// <summary>Wonach eine Tabelle gewertet wird: Punkte je Ausgang, die Ranking-Kriterien in ihrer Reihenfolge und für
/// das Kriterium <see cref="RankingRegelArt.Koeffizient"/> die Plätze in der Standard-Rangliste der Runde
/// (Mannschaft → Platz; ohne Angabe entscheidet es nichts), dazu ob der direkte Vergleich für die nach der
/// Mini-Tabelle noch gleichen Teams neu gerechnet wird (<see cref="RankingRegelSet.DirektenVergleichWiederholen"/>,
/// #628). Gemeinsame Eingabe von Tabellen-, Gruppen-, Verlaufs- und Prognoserechnung (#505).
/// Punktevortrag (#355): <paramref name="Vortraege"/> ist der Startwert je Team (fehlt ein Team, startet es bei null),
/// <paramref name="VorrundenPlaetze"/> der Platz in der Quellrunde für <see cref="RankingRegelArt.PlatzVorrunde"/>.</summary>
public sealed record TabellenRegeln(
    PunkteRegel PunkteRegel,
    IReadOnlyList<RankingRegel> RankingRegeln,
    IReadOnlyDictionary<int, int>? RanglistenPlaetze = null,
    bool DirektenVergleichWiederholen = false,
    IReadOnlyDictionary<int, Vortragswert>? Vortraege = null,
    IReadOnlyDictionary<int, int>? VorrundenPlaetze = null)
{
    /// <summary>Der Vortrag eines Teams; ohne Eintrag <see cref="Vortragswert.Keiner"/>.</summary>
    public Vortragswert VortragVon(int mannschaftId) =>
        Vortraege is not null && Vortraege.TryGetValue(mannschaftId, out var wert) ? wert : Vortragswert.Keiner;
}
