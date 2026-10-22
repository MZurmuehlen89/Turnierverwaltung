namespace Fussballturnierverwaltung.Application.Spielplan;

/// <summary>Was beim Zurücksetzen/Löschen einer Auslosung außer dem Auslosungsergebnis verloren geht (#405):
/// <see cref="Hinweis"/> ist der an die Rückfrage anzuhängende Text (leer ohne Spielplan),
/// <see cref="DatenBetroffen"/> gilt bei eingetragenen Ergebnissen oder betroffenen Folgerunden.</summary>
public sealed record SpielplanVerlust(string Hinweis, bool DatenBetroffen)
{
    public static readonly SpielplanVerlust Keiner = new("", false);
}

/// <summary>Ermittelt den <see cref="SpielplanVerlust"/> einer Runde (#496): ihren Spielplan samt eingetragener
/// Ergebnisse (#320) und über die Kaskade bereits aufgelöste Paarungen in Folgerunden (#306).</summary>
public sealed class SpielplanVerlustVorschau(SpielplanService spielplanService, PlatzhalterKaskadeService kaskade)
{
    public async Task<SpielplanVerlust> ErmittelnAsync(int turnierInstanzId, int rundeId)
    {
        var spiele = await spielplanService.SpielplanAbrufenAsync(rundeId);
        if (spiele.Count == 0) return SpielplanVerlust.Keiner;

        // Vorschau wie vor "Ergebnisse zurücksetzen" im Spielplan (#389): alle Spiele der Runde gelten als
        // entfernt. Neu zu generierende leere Runden zählen nicht — dort geht nichts verloren.
        var plan = await kaskade.PlanenAsync(turnierInstanzId, spiele.Select(s => s.Id).ToHashSet());
        var folgeSchritte = plan.Schritte.OfType<KaskadenSpielSchritt>().Where(s => s.RundeId != rundeId).ToList();

        return Aus(spiele.Count, spiele.Count(s => s.Ergebnis is not null), folgeSchritte);
    }

    /// <summary>Baut Hinweistext und Betroffenheit aus den Spielen der Runde und den Kaskaden-Schritten in
    /// Folgerunden.</summary>
    public static SpielplanVerlust Aus(int spiele, int mitErgebnis, IReadOnlyList<KaskadenSpielSchritt> folgeSchritte)
    {
        if (spiele == 0) return SpielplanVerlust.Keiner;

        var hinweis = mitErgebnis > 0
            ? $"\n\nDer Spielplan der Runde wird mitgelöscht: {spiele} Spiel(e), davon {mitErgebnis} mit " +
              "eingetragenem Ergebnis."
            : $"\n\nDer Spielplan der Runde wird mitgelöscht: {spiele} Spiel(e), noch ohne Ergebnis.";

        if (folgeSchritte.Count > 0)
        {
            var runden = string.Join(", ", folgeSchritte.Select(s => s.RundeName).Distinct());
            int folgeErgebnisse = folgeSchritte.Count(s => s.HatteErgebnis);
            hinweis += folgeErgebnisse > 0
                ? $"\n\nZusätzlich werden {folgeErgebnisse} Ergebnis(se) in Folgerunden zurückgesetzt, deren " +
                  $"Paarungen davon abhängen ({runden})."
                : $"\n\nZusätzlich werden bereits feststehende Paarungen in Folgerunden zurückgesetzt ({runden}).";
        }

        return new SpielplanVerlust(hinweis, mitErgebnis > 0 || folgeSchritte.Count > 0);
    }
}
