using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Spiele.Eingaben;

public record ErgebnisErfassenEingabe(
    int HeimToreRegulaer,
    int AuswaertsToreRegulaer,
    EntscheidungArt EntscheidungArt,
    int? HeimToreVerlaengerung = null,
    int? AuswaertsToreVerlaengerung = null,
    int? HeimElfmetertore = null,
    int? AuswaertsElfmetertore = null);
