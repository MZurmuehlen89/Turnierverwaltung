namespace Fussballturnierverwaltung.Application.Turniere.Eingaben;

public record LandesQuoteEingabe(
    int TurnierInstanzId,
    int LandId,
    int Anzahl);

public record LandesQuoteZeileEingabe(int LandId, int Anzahl);
