using Fussballturnierverwaltung.Application.Turnierbaum;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Spielplan;

/// <summary>Kurzbezeichnung eines K.-o.-Spiels in den Spiellisten, z.B. "VF 3", "AF 2 R" oder "F" — ohne
/// Datenbank. Das Kürzel der Runde ist dasselbe wie im Turnierbaum (<see cref="RundenBezeichnung"/>, #455): eine
/// Runde heißt überall gleich.</summary>
public static class KoSpielLabel
{
    /// <summary>Label des Spiels aus Rundenkürzel und Paarungsnummer, bei Hin-/Rückspiel mit "H"/"R"; die Runde
    /// mit nur einer Paarung trägt das Kürzel ohne Nummer ("F", "P3"). <c>null</c> für Runden ohne
    /// K.-o.-Paarungen.</summary>
    public static string? Fuer(Spiel spiel, TurnierRunde? runde, int paarungsNummer)
    {
        if (runde is null || AnzahlPaarungen(runde) is not int anzahl) return null;

        // Der Trennstrich mancher Kürzel ("L32-") gehört zum Knoten-Label des Turnierbaums ("L32-1"); hier
        // trennt das Leerzeichen vor der Paarungsnummer.
        string kuerzel = RundenBezeichnung
            .Kuerzel(runde.Name, anzahl, runde.Rundenart == Rundenart.SpielUmPlatzDrei)
            .TrimEnd('-');
        string label = anzahl == 1 ? kuerzel : $"{kuerzel} {paarungsNummer}";

        return runde.MitRueckspiel ? $"{label} {(spiel.IstRueckspiel ? "R" : "H")}" : label;
    }

    private static int? AnzahlPaarungen(TurnierRunde runde) => runde.Rundenart switch
    {
        Rundenart.Finale or Rundenart.SpielUmPlatzDrei => 1,
        Rundenart.KoRunde when runde.EingangsMannschaftsanzahl >= 2 => runde.EingangsMannschaftsanzahl / 2,
        _ => null
    };
}
