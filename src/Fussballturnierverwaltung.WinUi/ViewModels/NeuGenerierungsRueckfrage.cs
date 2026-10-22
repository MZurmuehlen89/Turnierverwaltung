using Fussballturnierverwaltung.Application.Auslosungen;
using Fussballturnierverwaltung.Application.Spielplan;
using Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.WinUi.Dialoge;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>Vorbereitung von „Spielplan neu generieren“ im Spielplan (#490 aus dem <see cref="SpielplanViewModel"/>
/// herausgelöst).</summary>
public sealed class NeuGenerierungsRueckfrage(AuslosungService auslosungService, IDialogService dialogService)
{
    /// <summary>Die abgeschlossene Auslosung, aus der die Runde neu generiert wird — nach Rückfrage, wenn sie
    /// schon Spiele hat. <c>null</c>, wenn nicht generiert wird: abgebrochen oder Fehlermeldung gesetzt.</summary>
    public async Task<Auslosung?> AuslosungFuerNeuGenerierungAsync(
        TurnierRunde runde, int instanzId, int anzahlSpiele, Action<string> fehlerMelden)
    {
        try
        {
            // Issue #433: vor Rückfrage und Löschen prüfen — ein ohne Auslosung automatisch generiertes Finale
            // (#113) hat Spiele, aber keine abgeschlossene Auslosung; das Löschen davor nahm ihm das Spiel
            // samt Ergebnis, ohne dass etwas neu generiert werden konnte.
            var auslosung = await auslosungService.FindenAbgeschlosseneNachRundeAsync(instanzId, runde.Id);
            if (auslosung is null)
            {
                fehlerMelden(NeuGenerierungsHinweis.FehlendeAuslosung(runde, anzahlSpiele > 0));
                return null;
            }

            if (anzahlSpiele == 0) return auslosung;

            var bestaetigt = await dialogService.BestaetigenAsync(
                "Spielplan neu generieren",
                $"Die Runde '{runde.Name}' hat bereits {anzahlSpiele} Spiel(e). " +
                "Soll der Spielplan gelöscht und neu generiert werden?");
            return bestaetigt ? auslosung : null;
        }
        catch (Exception ex)
        {
            fehlerMelden(ex.Message);
            return null;
        }
    }
}
