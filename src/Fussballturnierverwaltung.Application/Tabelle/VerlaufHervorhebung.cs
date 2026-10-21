namespace Fussballturnierverwaltung.Application.Tabelle;

/// <summary>Hervorgehobene Mannschaft im Tabellenverlauf (#480) mit ihrer Linienfarbe 1 bis
/// <see cref="VerlaufHervorhebungen.MAX_ANZAHL"/>.</summary>
public sealed record VerlaufHervorhebung(int MannschaftId, string Name, int Farbe);

/// <summary>Auswahl der hervorgehobenen Mannschaften, ohne Oberfläche. Die Farbe folgt der Mannschaft, nicht ihrer
/// Reihenfolge: Wer bleibt, behält seine Farbe; eine neue Mannschaft bekommt die kleinste freie.</summary>
public static class VerlaufHervorhebungen
{
    /// <summary>Mehr Farben sind bei sich kreuzenden Linien nicht sicher unterscheidbar (dataviz-Validator).</summary>
    public const int MAX_ANZAHL = 3;

    /// <summary>Hebt die Mannschaft hervor bzw. nimmt sie heraus. Sind schon alle Farben vergeben, weicht die am
    /// längsten hervorgehobene.</summary>
    public static IReadOnlyList<VerlaufHervorhebung> Umschalten(
        IReadOnlyList<VerlaufHervorhebung> aktuell, int mannschaftId, string name)
    {
        if (aktuell.Any(h => h.MannschaftId == mannschaftId))
            return [.. aktuell.Where(h => h.MannschaftId != mannschaftId)];

        var bleibende = aktuell.Count >= MAX_ANZAHL ? aktuell.Skip(1).ToList() : [.. aktuell];
        int farbe = Enumerable.Range(1, MAX_ANZAHL).First(f => bleibende.All(h => h.Farbe != f));
        return [.. bleibende, new VerlaufHervorhebung(mannschaftId, name, farbe)];
    }

    /// <summary>Nach einem Wechsel von Runde, Gruppe oder Datenstand: Es bleiben nur Mannschaften, die im Verlauf
    /// vorkommen; bleibt keine, wird der aktuelle Erste hervorgehoben.</summary>
    public static IReadOnlyList<VerlaufHervorhebung> Abgleichen(
        IReadOnlyList<VerlaufHervorhebung> aktuell, TabellenVerlauf? verlauf)
    {
        if (verlauf is null) return [];
        var ids = verlauf.Mannschaften.Select(m => m.Mannschaft.Id).ToHashSet();
        var bleibende = aktuell.Where(h => ids.Contains(h.MannschaftId)).ToList();
        if (bleibende.Count > 0 || verlauf.Mannschaften.Count == 0) return bleibende;

        var erster = verlauf.Mannschaften[0].Mannschaft;
        return [new VerlaufHervorhebung(erster.Id, erster.Name, 1)];
    }
}
