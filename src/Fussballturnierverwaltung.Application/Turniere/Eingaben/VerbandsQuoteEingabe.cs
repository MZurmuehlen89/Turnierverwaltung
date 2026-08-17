namespace Fussballturnierverwaltung.Application.Turniere.Eingaben;

public record VerbandsQuoteEingabe(
    int TurnierInstanzId,
    int VerbandId,
    int Anzahl);

public record VerbandsQuoteZeileEingabe(int VerbandId, int Anzahl);
