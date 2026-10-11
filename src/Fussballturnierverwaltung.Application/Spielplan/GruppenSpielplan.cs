using Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;

namespace Fussballturnierverwaltung.Application.Spielplan;

/// <summary>Ein Teilnehmerplatz einer Gruppe: feste Mannschaft oder (noch) Platzhalter.</summary>
public readonly record struct GruppenSlot(int? MannschaftId, string? PlatzhalterName);

/// <summary>Ein geplantes Gruppenspiel in Spielnummer-Reihenfolge; <see cref="HinspielIndex"/> zeigt bei
/// Rückspielen auf den Listenindex des zugehörigen Hinspiels.</summary>
public sealed record GeplantesGruppenSpiel(
    GruppenSlot Heim, GruppenSlot Auswaerts, int GruppenNummer, int Spieltag, bool IstRueckspiel, int? HinspielIndex);

public static class GruppenSpielplan
{
    /// <summary>Gruppen-Auslosungen speichern einen Eintrag pro Team (nur Heim-Seite, PaarungsNummer =
    /// Gruppe). Innerhalb der Gruppe nach Id sortiert, damit Generierung und Kaskade unabhängig von der
    /// Ladereihenfolge denselben Plan bauen.</summary>
    public static List<IReadOnlyList<GruppenSlot>> GruppenSlotsAusAuslosung(IEnumerable<AuslosungsErgebnis> ergebnisse) =>
        [.. ergebnisse
            .GroupBy(e => e.PaarungsNummer)
            .OrderBy(g => g.Key)
            .Select(g => (IReadOnlyList<GruppenSlot>)[.. g
                .OrderBy(e => e.Id)
                .Select(e => new GruppenSlot(e.HeimMannschaftId, e.HeimPlatzhalterName))])];

    /// <summary>Reine Funktion: Berger-Round-Robin je Gruppe, sortiert nach Spieltag und Gruppe — exakt die
    /// Reihenfolge, in der <see cref="SpielplanService.GenerierenAusGruppenMitPlatzhalternAsync"/> die Spielnummern vergibt.
    /// Die Kaskade baut damit den Soll-Spielplan nach und vergleicht Spiel mit Spiel (#395).</summary>
    public static List<GeplantesGruppenSpiel> GruppenSpielplanPlanen(
        IReadOnlyList<IReadOnlyList<GruppenSlot>> gruppen, bool mitRueckspiel)
    {
        var geplant = new List<(int GruppenIndex, int Spieltag, int Nr, GruppenSlot Heim, GruppenSlot Auswaerts, int? HinspielNr)>();
        int nr = 0;

        for (int gi = 0; gi < gruppen.Count; gi++)
        {
            var slots = gruppen[gi].Select(s => (GruppenSlot?)s).ToList();
            if (slots.Count < 2) continue;
            if (slots.Count % 2 == 1) slots.Add(null);

            int gesamt    = slots.Count;
            int spieltage = gesamt - 1;
            var kreis     = Enumerable.Range(1, gesamt - 1).ToArray();
            var hinspiele = new List<(int Spieltag, int Nr, GruppenSlot Heim, GruppenSlot Auswaerts)>();

            void Hinzufuegen(GruppenSlot? heim, GruppenSlot? ausw, int spieltag)
            {
                if (heim is null || ausw is null) return;
                hinspiele.Add((spieltag, nr, heim.Value, ausw.Value));
                geplant.Add((gi, spieltag, nr++, heim.Value, ausw.Value, null));
            }

            for (int spieltag = 0; spieltag < spieltage; spieltag++)
            {
                if (spieltag % 2 == 0) Hinzufuegen(slots[0], slots[kreis[^1]], spieltag + 1);
                else                   Hinzufuegen(slots[kreis[^1]], slots[0], spieltag + 1);

                for (int i = 0; i < (gesamt - 2) / 2; i++)
                    Hinzufuegen(slots[kreis[i]], slots[kreis[gesamt - 3 - i]], spieltag + 1);

                var letzter = kreis[^1];
                for (int k = gesamt - 2; k > 0; k--) kreis[k] = kreis[k - 1];
                kreis[0] = letzter;
            }

            if (mitRueckspiel)
            {
                foreach (var h in hinspiele)
                    geplant.Add((gi, h.Spieltag + spieltage, nr++, h.Auswaerts, h.Heim, h.Nr));
            }
        }

        var sortiert = geplant
            .OrderBy(x => x.Spieltag)
            .ThenBy(x => x.GruppenIndex)
            .ThenBy(x => x.Nr)
            .ToList();
        var indexJeNr = sortiert.Select((x, i) => (x.Nr, i)).ToDictionary(x => x.Nr, x => x.i);

        return [.. sortiert.Select(x => new GeplantesGruppenSpiel(
            x.Heim, x.Auswaerts, x.GruppenIndex + 1, x.Spieltag, x.HinspielNr.HasValue,
            x.HinspielNr is int hinNr ? indexJeNr[hinNr] : null))];
    }

    /// <summary>Plausibilitätsprüfung vor dem Spiel-zu-Spiel-Vergleich der Kaskade: gleiche Anzahl, gleiche
    /// Hin-/Rückspiel-Lage, feste Teams an derselben Stelle und noch offene Platzhalter mit demselben Namen.
    /// Bereits aufgelöste Platzhalter-Seiten sind nicht prüfbar (der Name wird beim Auflösen entfernt).</summary>
    public static bool PasstZuGruppenPlan(IReadOnlyList<GeplantesGruppenSpiel> geplant, IReadOnlyList<Spiel> spiele)
    {
        if (geplant.Count != spiele.Count) return false;

        static bool SeitePasst(GruppenSlot soll, int? mannschaftId, string? platzhalterName) =>
            soll.MannschaftId is int festId
                ? mannschaftId == festId
                : mannschaftId.HasValue || string.Equals(platzhalterName, soll.PlatzhalterName ?? "?", StringComparison.OrdinalIgnoreCase);

        return geplant.Zip(spiele).All(x =>
            x.First.IstRueckspiel == x.Second.IstRueckspiel &&
            SeitePasst(x.First.Heim, x.Second.HeimMannschaftId, x.Second.HeimPlatzhalterName) &&
            SeitePasst(x.First.Auswaerts, x.Second.AuswaertsMannschaftId, x.Second.AuswaertsPlatzhalterName));
    }
}
