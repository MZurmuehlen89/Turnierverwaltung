using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Turniere.Eingaben;

public record VorlagenRundeEingabe(
    string Name,
    Rundenart Rundenart,
    int EingangsMannschaftsanzahl,
    int AusgangsMannschaftsanzahl,
    bool AuswaertstorRegelAktiv = false,
    int? RegelwerkId = null);

public record TurnierVorlageErstellenEingabe(
    string Name,
    string Beschreibung,
    int ErforderlicheMannschaftsanzahl,
    VorlagenKategorie VorlagenKategorie,
    bool IstHistorisch,
    DateOnly? GueltigVon,
    DateOnly? GueltigBis);

public record TurnierVorlageAktualisierenEingabe(
    string Name,
    string Beschreibung,
    int ErforderlicheMannschaftsanzahl,
    VorlagenKategorie VorlagenKategorie,
    bool IstHistorisch,
    DateOnly? GueltigVon,
    DateOnly? GueltigBis);

public record TurnierInstanzVollAktualisierenEingabe(
    string Name,
    string SaisonOderJahr,
    int? TurnierVorlageId,
    DateOnly? Startdatum,
    DateOnly? Enddatum,
    TurnierStatus Status);

public record TurnierInstanzErstellenEingabe(
    string Name,
    string SaisonOderJahr,
    int? TurnierVorlageId,
    DateOnly? Startdatum,
    DateOnly? Enddatum);

public record TurnierAktualisierenEingabe(
    string Name,
    string SaisonOderJahr);

public record TurnierRundeAktualisierenEingabe(
    string Name,
    int RegelwerkId,
    bool AuswaertstorRegelAktiv);

public record TurnierRundeEingabe(
    string Name,
    Rundenart Rundenart,
    int EingangsMannschaftsanzahl,
    int AusgangsMannschaftsanzahl,
    int RegelwerkId,
    bool AuswaertstorRegelAktiv = false,
    string? KonfigurationJson = null);

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
