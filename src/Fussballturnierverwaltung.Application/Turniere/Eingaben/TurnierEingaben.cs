using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Turniere.Eingaben;

public record VorlagenRundeEingabe(
    string Name,
    Rundenart Rundenart,
    int EingangsMannschaftsanzahl,
    int AusgangsMannschaftsanzahl,
    bool MitRueckspiel = false,
    bool AuswaertstorRegelAktiv = false,
    int? RegelwerkId = null,
    int? GruppenAnzahl = null,
    bool PaarungFestVorgegeben = false);

/// <summary>Vorlagen-Variante der festen Paarungszuweisung (Issue #277, Vorlagen-Pendant zu
/// <c>ManuellePaarungEingabe</c>) — referenziert die Runde über die 1-basierte
/// <see cref="VorlagenRundeEingabe"/>-Listenposition, aus demselben Grund wie
/// <see cref="VorlagenUebergangsRegelEingabe"/>.</summary>
public record VorlagenPaarungEingabe(
    int RundenReihenfolge,
    int PaarungsNummer,
    int SlotNr,
    string PlatzhalterBezeichnung);

/// <summary>Vorlagen-Variante der Übergangsregel (Issue #166) — referenziert Quell-/Zielrunde über die
/// 1-basierte <see cref="VorlagenRundeEingabe"/>-Listenposition (Reihenfolge) statt über eine reale
/// TurnierRunde-Id, da beim Speichern einer Vorlage alle VorlagenRunden ersetzt werden (neue Ids).</summary>
public record VorlagenUebergangsRegelEingabe(
    int QuellReihenfolge,
    int ZielReihenfolge,
    int? GruppeIndex,
    int PlatzierungVon,
    int PlatzierungBis,
    int? AnzahlBeste = null);

public record TurnierVorlageErstellenEingabe(
    string Name,
    string Beschreibung,
    int ErforderlicheMannschaftsanzahl,
    VorlagenKategorie VorlagenKategorie,
    bool IstHistorisch,
    DateOnly? GueltigVon,
    DateOnly? GueltigBis,
    byte[]? Logo = null);

public record TurnierVorlageAktualisierenEingabe(
    string Name,
    string Beschreibung,
    int ErforderlicheMannschaftsanzahl,
    VorlagenKategorie VorlagenKategorie,
    bool IstHistorisch,
    DateOnly? GueltigVon,
    DateOnly? GueltigBis,
    byte[]? Logo = null);

public record TurnierInstanzVollAktualisierenEingabe(
    string Name,
    string SaisonOderJahr,
    int? TurnierVorlageId,
    DateOnly? Startdatum,
    DateOnly? Enddatum,
    TurnierStatus Status,
    int? VerbandId = null,
    int? LandId = null,
    MannschaftsartFilter MannschaftsartFilter = MannschaftsartFilter.Alle,
    int? StandardRegelwerkId = null);

public record TurnierInstanzErstellenEingabe(
    string Name,
    string SaisonOderJahr,
    int? TurnierVorlageId,
    DateOnly? Startdatum,
    DateOnly? Enddatum,
    int? VerbandId = null,
    int? LandId = null,
    MannschaftsartFilter MannschaftsartFilter = MannschaftsartFilter.Alle,
    int? StandardRegelwerkId = null);

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
    bool MitRueckspiel = false,
    bool AuswaertstorRegelAktiv = false,
    string? KonfigurationJson = null,
    int? GruppenAnzahl = null,
    bool EingangUeberUebergangsregeln = false,
    bool PaarungFestVorgegeben = false);

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
