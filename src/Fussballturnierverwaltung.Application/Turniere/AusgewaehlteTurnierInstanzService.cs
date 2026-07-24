namespace Fussballturnierverwaltung.Application.Turniere;

// Haelt die aktuell gewaehlte TurnierInstanz ueber alle Bewegungsdaten-Editoren hinweg,
// da diese als Transient-ViewModels bei jeder Navigation neu instanziiert werden
// (siehe NavigationService) und ihre eigene Auswahl sonst nicht ueberleben wuerde.
public class AusgewaehlteTurnierInstanzService
{
    public int? TurnierInstanzId { get; set; }
}
