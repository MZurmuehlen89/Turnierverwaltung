using Fussballturnierverwaltung.Application.Stammdaten;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Tests.Simulation;

/// <summary>
/// Feste Kopie der Seed-Ratings (Saison 2025/26) für die Monte-Carlo-Tests (#404, #678), damit spätere Seed-Pflege
/// die Kalibrierung nicht unbemerkt verschiebt.
/// </summary>
internal static class KalibrierungsTeams
{
    // (Off, Mid, Def, Heim, Ausw)
    public static readonly (int, int, int, int, int)[] Bundesliga =
    [
        (90, 88, 86, 95, 82), (87, 86, 84, 88, 80), (84, 82, 78, 88, 76), (83, 82, 80, 84, 76),
        (79, 78, 77, 82, 73), (78, 77, 76, 80, 72), (75, 75, 73, 77, 69), (74, 74, 74, 78, 69),
        (73, 73, 73, 79, 68), (73, 73, 72, 76, 67), (74, 73, 72, 76, 68), (73, 73, 71, 76, 67),
        (72, 72, 71, 75, 67), (70, 70, 70, 73, 65), (68, 68, 67, 73, 62), (67, 67, 66, 70, 61),
        (73, 72, 71, 77, 67), (70, 69, 68, 75, 63)
    ];

    // 3. Liga + Regionalliga-Pokalteilnehmer
    public static readonly (int, int, int, int, int)[] Unterklassig =
    [
        (64, 63, 63, 67, 57), (62, 62, 61, 64, 55), (62, 61, 61, 64, 55), (62, 61, 61, 66, 56),
        (62, 61, 60, 66, 55), (62, 61, 61, 67, 55), (61, 61, 60, 66, 55), (61, 60, 60, 64, 55),
        (61, 60, 59, 66, 55), (61, 60, 60, 65, 55), (61, 60, 60, 66, 54), (60, 60, 60, 65, 54),
        (60, 59, 59, 64, 54), (60, 60, 59, 63, 54), (60, 59, 59, 64, 53), (59, 59, 58, 62, 53),
        (56, 56, 56, 60, 51), (56, 56, 55, 60, 50), (56, 55, 55, 60, 50), (55, 55, 54, 59, 50),
        (55, 55, 54, 60, 49), (55, 55, 54, 59, 49), (54, 54, 54, 59, 49), (54, 54, 53, 58, 49),
        (54, 53, 53, 58, 48), (53, 53, 53, 58, 48), (53, 53, 52, 57, 48), (53, 52, 52, 57, 47)
    ];

    /// <param name="mitSpielweise">Grundausrichtung aus dem Rating-Vorschlag wie im Seed (#667); sonst ausgeglichen.</param>
    public static List<Mannschaft> Teams(
        (int Off, int Mid, int Def, int Heim, int Ausw)[] ratings, int idBasis, bool mitSpielweise = false) =>
        ratings.Select((r, i) => new Mannschaft
        {
            Id = idBasis + i, Name = $"Team {idBasis + i}", Kurzname = $"T{idBasis + i}",
            OffensivRating = r.Off, MittelfeldRating = r.Mid, DefensivRating = r.Def,
            Heimstaerke = r.Heim, Auswaertsstaerke = r.Ausw,
            Grundausrichtung = mitSpielweise ? SpielweisenVorschlag.AusRatings(r.Off, r.Def) : Grundausrichtung.Ausgeglichen
        }).ToList();
}
