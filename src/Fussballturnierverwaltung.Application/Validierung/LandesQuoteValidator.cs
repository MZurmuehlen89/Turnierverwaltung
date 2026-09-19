using Fussballturnierverwaltung.Application.Abstraktionen;
using Fussballturnierverwaltung.Application.Turniere.Eingaben;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;

namespace Fussballturnierverwaltung.Application.Validierung;

public class LandesQuoteValidator
{
    /// <summary>Validiert eine Landes-Quote gegen die bestehenden Quoten desselben Turniers, analog
    /// VerbandsQuoteValidator. <paramref name="bearbeiteteId"/> schließt die eigene Quote von der
    /// Duplikat-Prüfung aus (Bearbeiten-Fall).</summary>
    public ValidationErgebnis Validieren(
        LandesQuoteEingabe eingabe,
        IReadOnlyList<LandesQuote> bestehende,
        int? bearbeiteteId = null)
    {
        var fehler = new List<string>();

        if (eingabe.Anzahl < 1)
            fehler.Add("Die Anzahl muss mindestens 1 sein.");

        bool duplikat = bestehende.Any(q => q.LandId == eingabe.LandId && q.Id != bearbeiteteId);
        if (duplikat)
            fehler.Add("Für dieses Land existiert in diesem Turnier bereits eine Quote.");

        return fehler.Count == 0 ? ValidationErgebnis.Ok() : ValidationErgebnis.MitFehlern([.. fehler]);
    }

    /// <summary>Validiert eine komplette Liste von Landes-Quoten-Zeilen auf einmal (WinUi-Turnierkopf-Grid),
    /// analog VerbandsQuoteValidator. <paramref name="eingangsKapazitaet"/> begrenzt optional die Summe
    /// aller Zeilen (Eingangskapazität der ersten Turnierrunde) — bei null entfällt dieser Check.</summary>
    public ValidationErgebnis Validieren(IReadOnlyList<LandesQuoteZeileEingabe> zeilen, int? eingangsKapazitaet = null)
    {
        var fehler = new List<string>();
        foreach (var z in zeilen)
        {
            if (z.Anzahl < 1) fehler.Add("Die Anzahl muss mindestens 1 sein.");
        }
        foreach (var dup in zeilen.GroupBy(z => z.LandId).Where(g => g.Count() > 1).Select(g => g.Key))
            fehler.Add($"Mehrere Quoten für dasselbe Land in diesem Turnier (Land-Id {dup}).");

        if (eingangsKapazitaet is { } kapazitaet)
        {
            var summe = zeilen.Sum(z => z.Anzahl);
            if (summe > kapazitaet)
                fehler.Add($"Die Summe der Landes-Quoten ({summe}) übersteigt die Eingangskapazität der ersten Runde ({kapazitaet}).");
        }

        return fehler.Count == 0 ? ValidationErgebnis.Ok() : ValidationErgebnis.MitFehlern([.. fehler]);
    }
}
