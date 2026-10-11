using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using System.Text.RegularExpressions;

namespace Fussballturnierverwaltung.Application.Spielplan;

public static class PlatzhalterNamen
{
    internal static readonly Regex GruppenSiegerRx      = new(@"^Sieger Gruppe ([A-Z0-9]+)$",   RegexOptions.Compiled | RegexOptions.IgnoreCase);
    internal static readonly Regex GruppenRangRx        = new(@"^(\d+)\. Gruppe ([A-Z0-9]+)$",  RegexOptions.Compiled | RegexOptions.IgnoreCase);
    internal static readonly Regex BestePlatzierungRx   = new(@"^(\d+)\. bester (\d+)\. Platz$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    // Issue #368: Rundenname beliebig (auch mit Leerzeichen/Bindestrich, z.B. "Playoff-Runde", "1. Runde") —
    // gierig bis zur letzten Zahl, die immer die Spielnummer ist. "Sieger Gruppe A" fängt Pattern 1 vorher ab.
    internal static readonly Regex KoSiegerRx           = new(@"^Sieger (.+) (\d+)$",    RegexOptions.Compiled | RegexOptions.IgnoreCase);
    internal static readonly Regex KoVerliererRx        = new(@"^Verlierer (.+) (\d+)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    internal static readonly Regex TabellenPlatzRx      = new(@"^Platz (\d+) \((.+)\)$",            RegexOptions.Compiled | RegexOptions.IgnoreCase);

    internal static int? KuerzelZuEingangsteams(string kuerzel) => kuerzel.ToUpperInvariant() switch
    {
        "HF"      => 4,
        "VF"      => 8,
        "AF"      => 16,
        "FINALE"  => 2,
        _ when kuerzel.StartsWith("R", StringComparison.OrdinalIgnoreCase)
              && int.TryParse(kuerzel[1..], out int n) => n * 2,
        _         => null
    };

    /// <summary>Findet die nächstgelegene VORHERGEHENDE K.o.-Runde, auf die sich ein Platzhaltertext wie
    /// "Sieger Halbfinale 1" bezieht. Zuerst per EXAKTEM Rundennamen (Fund: <see cref="TeilnehmerPoolHilfsmittel"/>
    /// — bzw. deren WPF-Pendant — erzeugt diesen Text immer aus dem echten Rundennamen, nie aus einer Kurzform;
    /// keine der mitgelieferten Turniervorlagen benennt ihre Runden "VF"/"HF"/"AF", wodurch die Kürzel-Tabelle
    /// unten in der Praxis nie traf und K.o.-Platzhalter nach der ersten K.o.-Runde nie automatisch aufgelöst
    /// wurden — Nutzerfund: "Sieger/Verlierer"-Übergänge zwischen zwei K.o.-Runden funktionierten nie, weder
    /// einzeln noch simuliert, weder produktiv noch in der Vorschau). Fällt nur mangels Namenstreffer auf die
    /// alte Kürzel-zu-Eingangsteams-Tabelle zurück (z.B. "VF" → 8) — deckt Turniere ab, deren Runden tatsächlich
    /// so kurz benannt sind, und hält die bestehenden Tests unverändert grün. Bei mehreren K.o.-Zweigen gleicher
    /// Größe/gleichen Namens im selben Turnier ist das die per <see cref="TurnierRunde.Reihenfolge"/> nächste,
    /// nicht immer die erste.</summary>
    public static TurnierRunde? NaechstgelegeneKoQuellrunde(
        string kuerzel, IReadOnlyList<Rundenart> arten, TurnierRunde runde, IReadOnlyList<TurnierRunde> alleRunden)
    {
        var kandidaten = alleRunden.Where(r => arten.Contains(r.Rundenart) && r.Reihenfolge < runde.Reihenfolge);

        var perName = kandidaten
            .Where(r => string.Equals(r.Name, kuerzel, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(r => r.Reihenfolge)
            .FirstOrDefault();
        if (perName is not null) return perName;

        int? eingangTeams = KuerzelZuEingangsteams(kuerzel);
        if (eingangTeams is null) return null;

        return kandidaten
            .Where(r => r.EingangsMannschaftsanzahl == eingangTeams.Value)
            .OrderByDescending(r => r.Reihenfolge)
            .FirstOrDefault();
    }
}
