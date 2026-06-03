using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Stammdaten.Eingaben;

public record MannschaftErstellenEingabe(
    string Name,
    string Kurzname,
    Mannschaftsart Mannschaftsart,
    int LandId,
    int OffensivRating,
    int MittelfeldRating,
    int DefensivRating,
    int Heimstaerke,
    int Auswaertsstaerke);

public record MannschaftAktualisierenEingabe(
    string Name,
    string Kurzname,
    int LandId,
    int OffensivRating,
    int MittelfeldRating,
    int DefensivRating,
    int Heimstaerke,
    int Auswaertsstaerke);
