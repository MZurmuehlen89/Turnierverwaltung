namespace Fussballturnierverwaltung.Application.Spielplan;

/// <summary>Was beim Zurücksetzen/Löschen einer Auslosung außer dem Auslosungsergebnis verloren geht (#405, #496).</summary>
public sealed class SpielplanVerlustVorschau(SpielplanService spielplanService, PlatzhalterKaskadeService kaskade)
{
    /// <summary>Der Spielplan der Runde samt eingetragener Ergebnisse (#320) und über die Kaskade bereits aufgelöste
    /// Paarungen in Folgerunden (#306). <c>Hinweis</c> ist der an die Rückfrage anzuhängende Text (leer ohne
    /// Spielplan), <c>DatenBetroffen</c> gilt bei eingetragenen Ergebnissen oder betroffenen Folgerunden.</summary>
    public async Task<(string Hinweis, bool DatenBetroffen)> ErmittelnAsync(int turnierInstanzId, int rundeId)
    {
        var spiele = await spielplanService.SpielplanAbrufenAsync(rundeId);
        if (spiele.Count == 0) return ("", false);

        int mitErgebnis = spiele.Count(s => s.Ergebnis is not null);
        var hinweis = mitErgebnis > 0
            ? $"\n\nDer Spielplan der Runde wird mitgelöscht: {spiele.Count} Spiel(e), davon {mitErgebnis} mit " +
              "eingetragenem Ergebnis."
            : $"\n\nDer Spielplan der Runde wird mitgelöscht: {spiele.Count} Spiel(e), noch ohne Ergebnis.";

        // Vorschau wie vor "Ergebnisse zurücksetzen" im Spielplan (#389): alle Spiele der Runde gelten als
        // entfernt. Neu zu generierende leere Runden zählen nicht — dort geht nichts verloren.
        var plan = await kaskade.PlanenAsync(
            turnierInstanzId, spiele.Select(s => s.Id).ToHashSet());
        var folgeSchritte = plan.Schritte.OfType<KaskadenSpielSchritt>().Where(s => s.RundeId != rundeId).ToList();
        if (folgeSchritte.Count > 0)
        {
            var runden = string.Join(", ", folgeSchritte.Select(s => s.RundeName).Distinct());
            int folgeErgebnisse = folgeSchritte.Count(s => s.HatteErgebnis);
            hinweis += folgeErgebnisse > 0
                ? $"\n\nZusätzlich werden {folgeErgebnisse} Ergebnis(se) in Folgerunden zurückgesetzt, deren " +
                  $"Paarungen davon abhängen ({runden})."
                : $"\n\nZusätzlich werden bereits feststehende Paarungen in Folgerunden zurückgesetzt ({runden}).";
        }

        return (hinweis, mitErgebnis > 0 || folgeSchritte.Count > 0);
    }
}
