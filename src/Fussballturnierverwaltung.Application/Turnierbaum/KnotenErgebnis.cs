using Fussballturnierverwaltung.Application.Spiele;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;

namespace Fussballturnierverwaltung.Application.Turnierbaum;

/// <summary>Ergebnisanzeige eines Knotens im Turnierbaum (#438, aus <c>TurnierBaumViewModel</c> herausgelöst).
/// <paramref name="Detail"/> steht in der großen Karte zwischen den Mannschaften, <paramref name="PaarungsText"/>
/// als Fußzeile einer Hin-/Rückspiel-Paarung (Issue #340), <paramref name="EntscheidungKurz"/> in der kompakten
/// Karte der Gesamtübersicht (Issue #238).</summary>
public sealed record KnotenErgebnis(
    bool HatErgebnis, string HeimScore, string AuswaertsScore, string Detail, string PaarungsText,
    string EntscheidungKurz, int? SiegerMannschaftId)
{
    private static readonly KnotenErgebnis _offen = new(false, "", "", "", "", "", null);

    public bool IstSieger(int? mannschaftId) => SiegerMannschaftId is not null && SiegerMannschaftId == mannschaftId;

    /// <summary><paramref name="rueckspiel"/> ist bei Hin-/Rückspiel-Runden das Rückspiel zum Knoten-Spiel, sonst
    /// <c>null</c>.</summary>
    public static KnotenErgebnis Fuer(Spiel spiel, Spiel? rueckspiel) => spiel.Ergebnis switch
    {
        null => _offen,
        var hinspiel when rueckspiel is not null => Paarung(hinspiel, rueckspiel.Ergebnis),
        var ergebnis => Einzelspiel(ergebnis)
    };

    private static KnotenErgebnis Einzelspiel(SpielErgebnis e) => new(
        true, e.HeimToreGesamt.ToString(), e.AuswaertsToreGesamt.ToString(),
        SpielErgebnisFormatierer.DetailText(e),
        PaarungsText: "",
        SpielErgebnisFormatierer.EntscheidungKuerzel(e.EntscheidungArt),
        e.SiegerMannschaftId);

    // Issue #340: Gesamtergebnis beider Spiele, Sieger aus dem Rückspiel (das Hinspiel-Ergebnis trägt nur den
    // Sieger des Einzelspiels). Ohne Rückspiel-Ergebnis steht der Stand nach dem Hinspiel ohne Sieger-Markierung.
    private static KnotenErgebnis Paarung(SpielErgebnis hinspiel, SpielErgebnis? rueckspiel)
    {
        string paarungsText = SpielErgebnisFormatierer.PaarungsEinzelergebnisse(hinspiel, rueckspiel);
        if (rueckspiel is null)
            return new(true, hinspiel.HeimToreGesamt.ToString(), hinspiel.AuswaertsToreGesamt.ToString(),
                Detail: "", paarungsText, EntscheidungKurz: "", SiegerMannschaftId: null);

        var (teamA, teamB) = HinRueckspielWertung.GesamtTore(hinspiel, rueckspiel);
        return new(true, teamA.ToString(), teamB.ToString(),
            SpielErgebnisFormatierer.PaarungsEntscheidung(rueckspiel), paarungsText,
            SpielErgebnisFormatierer.EntscheidungKuerzel(rueckspiel.EntscheidungArt), rueckspiel.SiegerMannschaftId);
    }
}
