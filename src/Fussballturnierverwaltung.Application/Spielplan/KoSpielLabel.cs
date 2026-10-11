using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Spielplan;

/// <summary>Kurzbezeichnung eines K.-o.-Spiels in den Spiellisten, z.B. "VF 3" oder "AF 2 R" — ohne Datenbank.
/// Die Kürzel richten sich nach der Teilnehmerzahl der Runde; der Turnierbaum beschriftet seine Knoten
/// dagegen nach dem Rundennamen (<c>RundenBezeichnung</c>).</summary>
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
            Rundenart.KoRunde => Kuerzel(runde.EingangsMannschaftsanzahl),
            _ => null
        };
        if (kuerzel is null) return null;

        return runde.MitRueckspiel
            ? $"{kuerzel} {paarungsNummer} {(spiel.IstRueckspiel ? "R" : "H")}"
            : $"{kuerzel} {paarungsNummer}";
    }

    public static string? Kuerzel(int eingangsMannschaftsanzahl) => eingangsMannschaftsanzahl switch
    {
        4 => "HF",
        8 => "VF",
        16 => "AF",
        32 => "R16",
        64 => "R32",
        128 => "R64",
        _ => eingangsMannschaftsanzahl > 2 ? $"R{eingangsMannschaftsanzahl / 2}" : null
    };
}
