namespace Fussballturnierverwaltung.Domain.Enums;

public enum Rundenart
{
    Liga              = 0,
    Gruppenphase      = 1,
    TopfLiga          = 2,
    KoRunde           = 3,
    // 4 war KoRundeMitHinUndRueckspiel — migriert zu KoRunde + MitRueckspiel=true
    SchweizerSystem   = 5,
    // 6 war Sonderrunde — entfernt, Liga/Gruppenphase/K.-o.-Runde decken alle Fälle ab (#570)
    Finale            = 7,
    SpielUmPlatzDrei  = 8
}
