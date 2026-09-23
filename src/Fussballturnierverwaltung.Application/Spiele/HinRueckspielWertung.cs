using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Spiele;

/// <summary>
/// Gesamtwertung einer K.-o.-Paarung mit Hin- und Rückspiel (Issue #346). Gemeinsam genutzt von
/// <see cref="SpielErgebnisService"/> (Sieger-Ermittlung), <see cref="Simulation.SimulationsService"/>
/// (Entscheidungsverfahren nur bei ausgeglichenem Gesamtergebnis) und der WinUi-Ergebniseingabe
/// (Vorschlag der Entscheidungsart). Im Rückspiel sind die Rollen vertauscht: dessen Heimteam war im
/// Hinspiel das Auswärtsteam.
/// </summary>
public static class HinRueckspielWertung
{
    public static bool IstKoRunde(TurnierRunde runde) =>
        runde.Rundenart is Rundenart.KoRunde or Rundenart.Finale or Rundenart.SpielUmPlatzDrei;

    /// <summary>Hinspiel einer K.-o.-Paarung mit Rückspiel — entscheidet für sich allein nichts.</summary>
    public static bool IstKoHinspiel(Spiel spiel, TurnierRunde runde) =>
        !spiel.IstRueckspiel && runde.MitRueckspiel && IstKoRunde(runde);

    /// <summary>Rückspiel einer K.-o.-Paarung — sein Ergebnis trägt den Gesamtsieger.</summary>
    public static bool IstKoRueckspiel(Spiel spiel, TurnierRunde runde) =>
        spiel.IstRueckspiel && spiel.HinspielId is not null && IstKoRunde(runde);

    /// <summary>Tore einer Seite des Hinspiels. Altbestand kann noch Hinspiele mit Verlängerung
    /// enthalten (vor #346 lief dort das Entscheidungsverfahren) — die zählen mit.</summary>
    public static int HinspielTore(SpielErgebnis hinspiel, bool heim) => heim
        ? hinspiel.HeimToreRegulaer      + (hinspiel.HeimToreVerlaengerung      ?? 0)
        : hinspiel.AuswaertsToreRegulaer + (hinspiel.AuswaertsToreVerlaengerung ?? 0);

    /// <summary>True, wenn nach der regulären Spielzeit des Rückspiels weder das Gesamtergebnis noch —
    /// bei aktiver Auswärtstorregel (AD-001) — die Auswärtstore entscheiden, also das
    /// Entscheidungsverfahren (Verlängerung/Elfmeterschießen) im Rückspiel fällig ist.</summary>
    public static bool EntscheidungsverfahrenNoetig(
        int hinspielHeimTore, int hinspielAuswaertsTore,
        int rueckspielHeimTore, int rueckspielAuswaertsTore,
        bool auswaertstorRegelAktiv)
    {
        // Team A = Hinspiel-Heim = Rückspiel-Auswärts; Team B umgekehrt.
        int teamA = hinspielHeimTore      + rueckspielAuswaertsTore;
        int teamB = hinspielAuswaertsTore + rueckspielHeimTore;
        if (teamA != teamB) return false;

        return !auswaertstorRegelAktiv || rueckspielAuswaertsTore == hinspielAuswaertsTore;
    }
}
