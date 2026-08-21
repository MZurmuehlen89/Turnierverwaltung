using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Spiele.Eingaben;

public record SpielErgebnisEingabe(
    int HeimMannschaftId,
    int AuswaertsMannschaftId,
    int HeimToreRegulaer,
    int AuswaertsToreRegulaer,
    int? HeimToreVerlaengerung,
    int? AuswaertsToreVerlaengerung,
    int? HeimElfmetertore,
    int? AuswaertsElfmetertore,
    EntscheidungArt EntscheidungArt,
    int? SiegerMannschaftId,
    int? HalbzeitHeimTore = null,
    int? HalbzeitAuswaertsTore = null);
