namespace Fussballturnierverwaltung.Application.Ranglisten.Import;

/// <summary>Stand einer Zeile im Import-Reiter (#477): automatisch erkannt, von Hand zugeordnet, noch offen oder
/// bewusst übersprungen. Offene und übersprungene Zeilen werden nicht importiert.</summary>
public enum ImportZeilenStatus { Automatisch, Zugeordnet, Offen, Uebersprungen }

/// <summary>Texte des Import-Reiters zum Zeilenstand (#477) — zustandslos, damit testbar.</summary>
public static class ImportZusammenfassung
{
    /// <summary>„312 Zeilen · 290 automatisch · 5 zugeordnet · 12 offen“ — Stände ohne Zeile entfallen; leer ohne Zeilen.</summary>
    public static string Text(IEnumerable<ImportZeilenStatus> status)
    {
        var anzahl = status.GroupBy(s => s).ToDictionary(g => g.Key, g => g.Count());
        var gesamt = anzahl.Values.Sum();
        if (gesamt == 0) return "";

        var teile = new List<string> { gesamt == 1 ? "1 Zeile" : $"{gesamt} Zeilen" };
        foreach (var (stand, wort) in new[]
                 {
                     (ImportZeilenStatus.Automatisch, "automatisch"), (ImportZeilenStatus.Zugeordnet, "zugeordnet"),
                     (ImportZeilenStatus.Offen, "offen"), (ImportZeilenStatus.Uebersprungen, "übersprungen")
                 })
        {
            if (anzahl.TryGetValue(stand, out var n)) teile.Add($"{n} {wort}");
        }
        return string.Join(" · ", teile);
    }

    /// <summary>Rückfrage vor dem Import — bei offenen Zeilen und/oder beim Überschreiben einer bestehenden Rangliste;
    /// <c>null</c>, wenn nichts zu bestätigen ist.</summary>
    public static string? Rueckfrage(int offen, string? ueberschriebeneRangliste)
    {
        var saetze = new List<string>();
        if (offen == 1)
            saetze.Add("1 Zeile ist noch nicht zugeordnet und wird nicht importiert.");
        else if (offen > 1)
            saetze.Add($"{offen} Zeilen sind noch nicht zugeordnet und werden nicht importiert.");
        if (ueberschriebeneRangliste is not null)
            saetze.Add($"Alle bisherigen Einträge von '{ueberschriebeneRangliste}' werden ersetzt.");

        return saetze.Count == 0 ? null : string.Join(" ", saetze) + " Fortfahren?";
    }
}
