namespace Fussballturnierverwaltung.Application.Spielplan.Eingaben;

public record QualifikationsRegelEingabe(
    int QuellRundeId,
    int ZielRundeId,
    int? GruppeIndex,
    int PlatzierungVon,
    int PlatzierungBis);
