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

        if (eingabe.MaxAnzahl < 1)
            fehler.Add("Die maximale Anzahl muss mindestens 1 sein.");

        if (eingabe.MinAnzahl is < 0)
            fehler.Add("Die minimale Anzahl darf nicht negativ sein.");

        if (eingabe.MinAnzahl is { } min && min > eingabe.MaxAnzahl)
            fehler.Add("Die minimale Anzahl darf die maximale Anzahl nicht überschreiten.");

        bool duplikat = bestehende.Any(q => q.VerbandId == eingabe.VerbandId && q.Id != bearbeiteteId);
        if (duplikat)
            fehler.Add("Für diesen Verband existiert in diesem Turnier bereits eine Quote.");

        return fehler.Count == 0 ? ValidationErgebnis.Ok() : ValidationErgebnis.MitFehlern([.. fehler]);
    }

    /// <summary>Validiert eine komplette Liste von Verband-Quoten-Zeilen auf einmal (WPF-Turnierkopf-Grid) —
    /// prüft den vollständigen beabsichtigten Endzustand vor jedem Schreibzugriff, statt zeilenweise.</summary>
    public ValidationErgebnis Validieren(IReadOnlyList<VerbandsQuoteZeileEingabe> zeilen)
    {
        var fehler = new List<string>();
        foreach (var z in zeilen)
        {
            if (z.MaxAnzahl < 1) fehler.Add("Die maximale Anzahl muss mindestens 1 sein.");
            if (z.MinAnzahl is < 0) fehler.Add("Die minimale Anzahl darf nicht negativ sein.");
            if (z.MinAnzahl is { } min && min > z.MaxAnzahl) fehler.Add("Die minimale Anzahl darf die maximale Anzahl nicht überschreiten.");
        }
        foreach (var dup in zeilen.GroupBy(z => z.VerbandId).Where(g => g.Count() > 1).Select(g => g.Key))
            fehler.Add($"Mehrere Quoten für denselben Verband in diesem Turnier (Verband-Id {dup}).");
        return fehler.Count == 0 ? ValidationErgebnis.Ok() : ValidationErgebnis.MitFehlern([.. fehler]);
    }
}
