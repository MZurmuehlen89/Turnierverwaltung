namespace Fussballturnierverwaltung.Domain.Enums;

public enum Rundenart
{
    Liga              = 0,
    Gruppenphase      = 1,
    TopfLiga          = 2,
    KoRunde           = 3,
    // 4 war KoRundeMitHinUndRueckspiel — migriert zu KoRunde + MitRueckspiel=true
    SchweizerSystem   = 5,
    Sonderrunde       = 6,
    Finale            = 7,
    SpielUmPlatzDrei  = 8
}
