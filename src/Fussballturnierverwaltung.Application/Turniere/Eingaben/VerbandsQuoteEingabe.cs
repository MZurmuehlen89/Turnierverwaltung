namespace Fussballturnierverwaltung.Application.Turniere.Eingaben;

public record VerbandsQuoteEingabe(
    int TurnierInstanzId,
    int VerbandId,
    int MaxAnzahl,
    int? MinAnzahl);

public record VerbandsQuoteZeileEingabe(int VerbandId, int MaxAnzahl, int? MinAnzahl);
