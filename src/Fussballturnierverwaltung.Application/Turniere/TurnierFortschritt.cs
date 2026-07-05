namespace Fussballturnierverwaltung.Application.Turniere;

public record TurnierFortschritt(
    int RundenAbgeschlossen,
    int RundenGesamt,
    int SpieleGespielt,
    int SpieleGesamt);
