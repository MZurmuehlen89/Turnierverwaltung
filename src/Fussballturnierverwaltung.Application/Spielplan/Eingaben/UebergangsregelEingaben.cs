namespace Fussballturnierverwaltung.Application.Spielplan.Eingaben;

public record UebergangsregelEingabe(
    int QuellRundeId,
    int ZielRundeId,
    int? GruppeIndex,
    int PlatzierungVon,
    int PlatzierungBis);
