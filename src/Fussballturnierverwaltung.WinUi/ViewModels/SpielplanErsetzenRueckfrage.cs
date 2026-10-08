using Fussballturnierverwaltung.Application.Spielplan;
using Fussballturnierverwaltung.WinUi.Dialoge;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>Rückfrage vor einer Aktion, die den Spielplan einer Runde ersetzt (Auslosung durchführen, Paarung der
/// Vorbelegung abschließen). Ersetzt wird ohne Rückfrage — wer auslost oder die Paarungen abschließt, will den
/// Spielplan dazu. Nachgefragt wird nur, wenn dabei bereits eingetragene Ergebnisse verloren gingen (z.B. ein
/// automatisch generiertes und schon gespieltes Finale), und dann VOR der Aktion: ein "Nein" danach hinterließe
/// eine abgeschlossene Auslosung mit einem Spielplan, der nicht zu ihr passt.</summary>
public static class SpielplanErsetzenRueckfrage
{
    /// <param name="titel">Titel des Dialogs, z.B. "Auslosung durchführen".</param>
    /// <param name="vorgang">Satzanfang für das, was den Spielplan ersetzt, z.B. "Die Auslosung".</param>
    /// <param name="verb">Die Aktion in der Schlussfrage, z.B. "auslosen".</param>
    public static async Task<bool> ErgebnisVerlustBestaetigtAsync(
        SpielplanService spielplanService, IDialogService dialogService, int rundeId,
        string titel, string vorgang, string verb)
    {
        var vorhandeneSpiele = await spielplanService.SpielplanAbrufenAsync(rundeId);
        int mitErgebnis = vorhandeneSpiele.Count(s => s.Ergebnis is not null);
        if (mitErgebnis == 0) return true;

        return await dialogService.BestaetigenAsync(
            titel,
            $"Die Runde hat bereits {mitErgebnis} Spiel(e) mit eingetragenem Ergebnis. {vorgang} ersetzt den " +
            $"Spielplan der Runde, diese Ergebnisse gehen dabei verloren. Trotzdem {verb}?");
    }
}
