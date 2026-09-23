using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Turniere.Eingaben;

public record TurnierInstanzVollAktualisierenEingabe(
    string Name,
    string Beschreibung,
    string SaisonOderJahr,
    TurnierKategorie Kategorie,
    int? ZielMannschaftsanzahl,
    DateOnly? Startdatum,
    DateOnly? Enddatum,
    TurnierStatus Status,
    int? VerbandId = null,
    int? LandId = null,
    MannschaftsartFilter MannschaftsartFilter = MannschaftsartFilter.Alle,
    int? StandardRegelwerkId = null,
    byte[]? Logo = null);

public record TurnierInstanzErstellenEingabe(
    string Name,
    string Beschreibung,
    string SaisonOderJahr,
    TurnierKategorie Kategorie,
    int? ZielMannschaftsanzahl,
    DateOnly? Startdatum,
    DateOnly? Enddatum,
    TurnierStatus Status = TurnierStatus.Geplant,
    int? VerbandId = null,
    int? LandId = null,
    MannschaftsartFilter MannschaftsartFilter = MannschaftsartFilter.Alle,
    int? StandardRegelwerkId = null,
    byte[]? Logo = null);

public record TurnierAktualisierenEingabe(
    string Name,
    string SaisonOderJahr);

public record TurnierRundeAktualisierenEingabe(
    string Name,
    int? RegelwerkId,
    bool AuswaertstorRegelAktiv);

public record TurnierRundeEingabe(
    string Name,
    Rundenart Rundenart,
    int EingangsMannschaftsanzahl,
    int AusgangsMannschaftsanzahl,
    int? RegelwerkId,
    bool MitRueckspiel = false,
    bool AuswaertstorRegelAktiv = false,
    string? KonfigurationJson = null,
    int? GruppenAnzahl = null,
    bool EingangUeberUebergangsregeln = false,
    bool PaarungFestVorgegeben = false,
    Auslosungsart? VorgegebeneAuslosungsart = null);

public record RegelwerkErstellenEingabe(
    string Name,
    string Beschreibung,
    DateOnly? GueltigVon,
    DateOnly? GueltigBis,
    int? RankingRegelSetId,
    int PunkteFuerSieg,
    int PunkteFuerUnentschieden,
    int PunkteFuerNiederlage,
    IReadOnlyList<VerfahrenArt> EntscheidungsVerfahrenReihenfolge);

public record RegelwerkAktualisierenEingabe(
    string Name,
    string Beschreibung,
    DateOnly? GueltigVon,
    DateOnly? GueltigBis,
    int? RankingRegelSetId,
    int PunkteFuerSieg,
    int PunkteFuerUnentschieden,
    int PunkteFuerNiederlage,
    IReadOnlyList<VerfahrenArt> EntscheidungsVerfahrenReihenfolge);
