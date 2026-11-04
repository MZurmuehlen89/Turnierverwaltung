using Fussballturnierverwaltung.Application.Abstraktionen;
using Fussballturnierverwaltung.Application.Turniere.Eingaben;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;

namespace Fussballturnierverwaltung.Application.Validierung;

public class VerbandsQuoteValidator
{
    /// <summary>Validiert eine Verband-Quote gegen die bestehenden Quoten desselben Turniers.
    /// <paramref name="bearbeiteteId"/> schließt die eigene Quote von der Duplikat-Prüfung aus (Bearbeiten-Fall).</summary>
    public ValidationErgebnis Validieren(
        VerbandsQuoteEingabe eingabe,
        IReadOnlyList<VerbandsQuote> bestehende,
        int? bearbeiteteId = null)
    {
        var fehler = new List<string>();

        if (eingabe.Anzahl < 1)
            fehler.Add("Die Anzahl muss mindestens 1 sein.");

        bool duplikat = bestehende.Any(q => q.VerbandId == eingabe.VerbandId && q.Id != bearbeiteteId);
        if (duplikat)
            fehler.Add("Für diesen Verband existiert in diesem Turnier bereits eine Quote.");

        return fehler.Count == 0 ? ValidationErgebnis.Ok() : ValidationErgebnis.MitFehlern([.. fehler]);
    }

    /// <summary>Validiert eine komplette Liste von Verband-Quoten-Zeilen auf einmal (WPF-Turnierkopf-Grid) —
    /// prüft den vollständigen beabsichtigten Endzustand vor jedem Schreibzugriff, statt zeilenweise.
    /// <paramref name="eingangsKapazitaet"/> begrenzt optional die Summe aller Zeilen (Issue #154, Eingangskapazität
    /// der ersten Turnierrunde) — bei null (z.B. Turnier ohne Runden) entfällt dieser Check. Fehler einer Zeile tragen
    /// Feld und Zeilenindex (#595).</summary>
    public ValidationErgebnis Validieren(IReadOnlyList<VerbandsQuoteZeileEingabe> zeilen, int? eingangsKapazitaet = null)
    {
        var fehler = new List<Feldfehler>();
        for (int i = 0; i < zeilen.Count; i++)
        {
            if (zeilen[i].Anzahl < 1)
                fehler.Add(new("Die Anzahl muss mindestens 1 sein.", nameof(VerbandsQuoteZeileEingabe.Anzahl), i));
        }
        foreach (var dup in zeilen.GroupBy(z => z.VerbandId).Where(g => g.Count() > 1).Select(g => g.Key))
            fehler.Add(new($"Mehrere Quoten für denselben Verband in diesem Turnier (Verband-Id {dup})."));

        if (eingangsKapazitaet is { } kapazitaet)
        {
            var summe = zeilen.Sum(z => z.Anzahl);
            if (summe > kapazitaet)
                fehler.Add(new($"Die Summe der Verband-Quoten ({summe}) übersteigt die Eingangskapazität der ersten Runde ({kapazitaet})."));
        }

        return ValidationErgebnis.Aus(fehler);
    }
}
