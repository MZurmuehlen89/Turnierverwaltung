namespace Fussballturnierverwaltung.Application.Spielplan.Eingaben;

public record UebergangsregelEingabe(
    int QuellRundeId,
    int ZielRundeId,
    int? GruppeIndex,
    int PlatzierungVon,
    int PlatzierungBis);

/// <summary>Eine Zeile innerhalb eines Übergangsregel-Blocks (siehe <see cref="Fussballturnierverwaltung.Application.Spielplan.UebergangsregelService.BlockSpeichernAsync"/>).
/// <see cref="Id"/> ist <c>null</c> für eine noch nicht gespeicherte Zeile.</summary>
public record UebergangsregelZeileEingabe(
    int? Id,
    int? GruppeIndex,
    int PlatzierungVon,
    int PlatzierungBis);
