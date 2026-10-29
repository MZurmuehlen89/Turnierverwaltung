using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Stammdaten.Eingaben;

public record LandErstellenEingabe(
    string Name,
    string Kurzname,
    string FifaCode,
    Kontinent Kontinent,
    int VerbandId,
    byte[]? Flagge = null,
    int? RanglisteId = null);

public record LandAktualisierenEingabe(
    string Name,
    string Kurzname,
    string FifaCode,
    Kontinent Kontinent,
    int VerbandId,
    byte[]? Flagge = null,
    int? RanglisteId = null);
