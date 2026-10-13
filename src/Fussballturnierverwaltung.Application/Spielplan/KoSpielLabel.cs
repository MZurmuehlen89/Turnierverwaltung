using Fussballturnierverwaltung.Application.Turnierbaum;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Spielplan;

/// <summary>Kurzbezeichnung eines K.-o.-Spiels in den Spiellisten, z.B. "VF 3" oder "AF 2 R" — ohne Datenbank.
/// Das Kürzel einer K.-o.-Runde ist dasselbe wie im Turnierbaum (<see cref="RundenBezeichnung"/>, #455): eine
/// Runde heißt überall gleich.</summary>
public static class KoSpielLabel
{
    /// <summary>Label des Spiels aus Rundenkürzel und Paarungsnummer, bei Hin-/Rückspiel mit "H"/"R";
    /// <c>null</c> für Runden ohne K.-o.-Paarungen und für K.-o.-Runden mit höchstens zwei Teilnehmern.</summary>
    public static string? Fuer(Spiel spiel, TurnierRunde? runde, int paarungsNummer)
    {
        if (runde is null) return null;

        string? kuerzel = runde.Rundenart switch
        {
            Rundenart.Finale => "Finale",
            Rundenart.SpielUmPlatzDrei => "3. Platz",
            Rundenart.KoRunde => Kuerzel(runde),
            _ => null
        };
        if (kuerzel is null) return null;

        return runde.MitRueckspiel
            ? $"{kuerzel} {paarungsNummer} {(spiel.IstRueckspiel ? "R" : "H")}"
            : $"{kuerzel} {paarungsNummer}";
    }

    // Der Trennstrich mancher Kürzel ("L32-") gehört zum Knoten-Label des Turnierbaums ("L32-1"); hier trennt
    // das Leerzeichen vor der Paarungsnummer.
    private static string? Kuerzel(TurnierRunde runde) => runde.EingangsMannschaftsanzahl > 2
        ? RundenBezeichnung.Kuerzel(runde.Name, runde.EingangsMannschaftsanzahl / 2).TrimEnd('-')
        : null;
}
