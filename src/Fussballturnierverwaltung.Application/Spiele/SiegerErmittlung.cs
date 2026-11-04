using Fussballturnierverwaltung.Application.Spiele.Eingaben;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Spiele;

public static class SiegerErmittlung
{
    // Bestimmt Sieger und EndgültigeEntscheidungsArt.
    // Wenn hinspielErgebnis übergeben wird (K.-o.-Rückspiel, Issue #346), entscheidet das Gesamtergebnis
    // beider Spiele; bei Gleichstand — und nur mit aktiver Regel — die Auswärtstore (AD-001), danach das
    // Entscheidungsverfahren, dessen Tore (Verlängerung/Elfmeter) allein im Rückspiel fallen.
    public static (int? sieger, EntscheidungArt art) BestimmeSieger(
        int heimId, int auswaertsId,
        ErgebnisErfassenEingabe e,
        SpielErgebnis? hinspielErgebnis,
        bool auswaertstorRegelAktiv)
    {
        if (hinspielErgebnis is not null)
        {
            // Rückspiel:  HeimId = Team B (war Auswärts im Hinspiel)
            //             AuswaertsId = Team A (war Heim im Hinspiel)
            int teamA_gesamt = HinRueckspielWertung.HinspielTore(hinspielErgebnis, heim: true)  + e.AuswaertsToreRegulaer;
            int teamB_gesamt = HinRueckspielWertung.HinspielTore(hinspielErgebnis, heim: false) + e.HeimToreRegulaer;

            if (teamA_gesamt != teamB_gesamt)
                return teamA_gesamt > teamB_gesamt
                    ? (auswaertsId, EntscheidungArt.RegulaereSpielzeit)
                    : (heimId,      EntscheidungArt.RegulaereSpielzeit);

            if (auswaertstorRegelAktiv)
            {
                // Gesamtergebnis gleich → Auswärtstore vergleichen
                int teamA_weg = e.AuswaertsToreRegulaer;                                          // Team A als Auswärts im Rückspiel
                int teamB_weg = HinRueckspielWertung.HinspielTore(hinspielErgebnis, heim: false); // Team B als Auswärts im Hinspiel

                if (teamA_weg != teamB_weg)
                    return teamA_weg > teamB_weg
                        ? (auswaertsId, EntscheidungArt.Auswaertstorregel)
                        : (heimId,      EntscheidungArt.Auswaertstorregel);
            }

            // Gesamtergebnis (und ggf. Auswärtstore) gleich → Entscheidungsverfahren. Da der Gesamtstand
            // nach 90 Minuten ausgeglichen ist, entscheiden allein die Tore der Verlängerung bzw. des
            // Elfmeterschießens — nicht (wie bei einem Einzelspiel) Regulär- plus Verlängerungstore.
            return (e.EntscheidungArt switch
            {
                EntscheidungArt.Verlaengerung or EntscheidungArt.GoldenGoal or EntscheidungArt.SilverGoal
                    when e.HeimToreVerlaengerung.HasValue && e.AuswaertsToreVerlaengerung.HasValue
                         && e.HeimToreVerlaengerung != e.AuswaertsToreVerlaengerung
                    => e.HeimToreVerlaengerung > e.AuswaertsToreVerlaengerung ? heimId : auswaertsId,
                EntscheidungArt.Elfmeterschiessen
                    when e.HeimElfmetertore.HasValue && e.AuswaertsElfmetertore.HasValue
                    => e.HeimElfmetertore.Value > e.AuswaertsElfmetertore.Value ? heimId : auswaertsId,
                EntscheidungArt.Losentscheid or EntscheidungArt.Muenzwurf
                    => LosSieger(heimId, auswaertsId, e),
                _ => null
            }, e.EntscheidungArt);
        }

        return (SiegerAbleiten(heimId, auswaertsId, e), e.EntscheidungArt);
    }

    private static int? SiegerAbleiten(int heimId, int auswaertsId, ErgebnisErfassenEingabe e)
    {
        return e.EntscheidungArt switch
        {
            EntscheidungArt.RegulaereSpielzeit when e.HeimToreRegulaer > e.AuswaertsToreRegulaer
                => heimId,
            EntscheidungArt.RegulaereSpielzeit when e.AuswaertsToreRegulaer > e.HeimToreRegulaer
                => auswaertsId,
            EntscheidungArt.RegulaereSpielzeit
                => null,
            EntscheidungArt.Verlaengerung or EntscheidungArt.GoldenGoal or EntscheidungArt.SilverGoal
                when e.HeimToreVerlaengerung.HasValue && e.AuswaertsToreVerlaengerung.HasValue
                => (e.HeimToreRegulaer + e.HeimToreVerlaengerung.Value) >
                   (e.AuswaertsToreRegulaer + e.AuswaertsToreVerlaengerung.Value)
                    ? heimId : auswaertsId,
            EntscheidungArt.Elfmeterschiessen
                when e.HeimElfmetertore.HasValue && e.AuswaertsElfmetertore.HasValue
                => e.HeimElfmetertore.Value > e.AuswaertsElfmetertore.Value ? heimId : auswaertsId,
            EntscheidungArt.Losentscheid or EntscheidungArt.Muenzwurf
                => LosSieger(heimId, auswaertsId, e),
            _ => null
        };
    }

    // Issue #383: bei Los/Münze geben die Tore den Sieger nicht her — ohne LosGewinnerHeim blieb er null,
    // und die K.-o.-Folgepaarung wurde nie aufgelöst.
    private static int? LosSieger(int heimId, int auswaertsId, ErgebnisErfassenEingabe e) => e.LosGewinnerHeim switch
    {
        true  => heimId,
        false => auswaertsId,
        null  => null
    };
}
