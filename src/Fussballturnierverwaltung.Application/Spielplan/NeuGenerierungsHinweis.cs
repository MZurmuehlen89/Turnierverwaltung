using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;

namespace Fussballturnierverwaltung.Application.Spielplan;

/// <summary>Meldungen zu „Spielplan neu generieren“ in der Turnierdurchführung (#490 aus dem
/// <c>SpielplanViewModel</c> herausgelöst).</summary>
public static class NeuGenerierungsHinweis
{
    /// <summary>Issue #433: fest vorgegebene Paarungen entstehen unter „Turniere“ im Reiter „Vorbelegung“ und
    /// liegen erst nach dem Abschließen dort als abgeschlossene Auslosung vor — für solche Runden gibt es in
    /// der Turnierdurchführung keine Auslosung, auf die die Meldung verweisen könnte. Eine Runde mit zwei
    /// Teilnehmern bekommt ihr Spiel ohne Auslosung automatisch (#113,
    /// <c>FinaleAutomatik.AbgleichenAsync</c>) — auch dort führt der Hinweis auf die Auslosung in
    /// die Irre.</summary>
    public static string FehlendeAuslosung(TurnierRunde runde, bool hatSpiele)
    {
        if (runde.PaarungFestVorgegeben)
            return "Für diese Runde sind die fest vorgegebenen Paarungen noch nicht festgelegt. " +
                   "Bitte zuerst unter „Turniere“ im Reiter „Vorbelegung“ die Paarungen festlegen und abschließen.";

        if (runde.EingangsMannschaftsanzahl != 2)
            return "Für diese Runde wurde noch keine Auslosung abgeschlossen. " +
                   "Bitte zuerst die Auslosung durchführen.";

        return hatSpiele
            ? "Das Spiel dieser Runde wurde ohne Auslosung automatisch aus der vorherigen Runde angelegt. " +
              "Ein Neugenerieren ist hier nicht nötig."
            : "Diese Runde hat nur zwei Teilnehmer und braucht keine Auslosung: Das Spiel wird automatisch " +
              "angelegt, sobald beide Teilnehmer aus der vorherigen Runde feststehen.";
    }
}
