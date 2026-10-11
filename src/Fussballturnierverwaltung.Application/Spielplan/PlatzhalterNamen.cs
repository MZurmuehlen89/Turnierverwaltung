using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using System.Text.RegularExpressions;

namespace Fussballturnierverwaltung.Application.Spielplan;

/// <summary>Worauf sich ein Platzhaltername bezieht — das Ergebnis von <see cref="PlatzhalterNamen.Erkennen"/>.</summary>
public abstract record PlatzhalterBezug
{
    private PlatzhalterBezug() { }

    /// <summary>"Sieger Gruppe A" (Rang 1) oder "2. Gruppe A".</summary>
    public sealed record GruppenRang(string GruppenKennung, int Rang) : PlatzhalterBezug;

    /// <summary>"3. Gruppe A/D/E/F" — Gruppendritter laut Kombinationstabelle der Runde (Issue #372).</summary>
    public sealed record KombinationsPlatz(int Platzierung) : PlatzhalterBezug;

    /// <summary>"2. bester 3. Platz" — gruppenübergreifende Rangfolge (Issue #213).</summary>
    public sealed record BestePlatzierung(int Rang, int Platzierung) : PlatzhalterBezug;

    /// <summary>"Sieger Halbfinale 1" oder "Verlierer Halbfinale 1" — <see cref="RundenKuerzel"/> ist der
    /// Rundenname oder ein Kürzel wie "HF".</summary>
    public sealed record KoSpiel(string RundenKuerzel, int SpielNummer, bool Verlierer) : PlatzhalterBezug;

    /// <summary>"Platz 3 (Ligaphase)" — Tabellenrang einer Liga-/TopfLiga-/Schweizer-Runde (Format aus
    /// <see cref="PlatzhalterHilfsmittel"/>, Issue #343).</summary>
    public sealed record TabellenPlatz(int Platz, string RundenName) : PlatzhalterBezug;
}

/// <summary>Liest Platzhalternamen wie "Sieger Gruppe A" oder "Verlierer Halbfinale 2" — ohne Datenbank.</summary>
public static class PlatzhalterNamen
{
    private static readonly Regex _gruppenSiegerRx     = new(@"^Sieger Gruppe ([A-Z0-9]+)$",   RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex _gruppenRangRx       = new(@"^(\d+)\. Gruppe ([A-Z0-9]+)$",  RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex _bestePlatzierungRx  = new(@"^(\d+)\. bester (\d+)\. Platz$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    // Issue #368: Rundenname beliebig (auch mit Leerzeichen/Bindestrich, z.B. "Playoff-Runde", "1. Runde") —
    // gierig bis zur letzten Zahl, die immer die Spielnummer ist. "Sieger Gruppe A" fängt das erste Muster vorher ab.
    private static readonly Regex _koSiegerRx          = new(@"^Sieger (.+) (\d+)$",    RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex _koVerliererRx       = new(@"^Verlierer (.+) (\d+)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex _tabellenPlatzRx     = new(@"^Platz (\d+) \((.+)\)$",            RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>Die Muster werden in fester Reihenfolge geprüft: "Sieger Gruppe A" ist ein Gruppenrang, kein
    /// K.o.-Spiel, und "3. Gruppe A/D/E/F" ein Kombinationsplatz, kein Gruppenrang.</summary>
    public static PlatzhalterBezug? Erkennen(string name)
    {
        var m = _gruppenSiegerRx.Match(name);
        if (m.Success) return new PlatzhalterBezug.GruppenRang(m.Groups[1].Value, 1);

        if (Kombinationstabelle.IstPlatz(name, out int kombinationsPlatzierung))
            return new PlatzhalterBezug.KombinationsPlatz(kombinationsPlatzierung);

        m = _gruppenRangRx.Match(name);
        if (m.Success && int.TryParse(m.Groups[1].Value, out int rang))
            return new PlatzhalterBezug.GruppenRang(m.Groups[2].Value, rang);

        m = _bestePlatzierungRx.Match(name);
        if (m.Success && int.TryParse(m.Groups[1].Value, out int bestenRang) && int.TryParse(m.Groups[2].Value, out int platzierung))
            return new PlatzhalterBezug.BestePlatzierung(bestenRang, platzierung);

        if (KoSpielErkennen(name) is { } koSpiel) return koSpiel;

        m = _tabellenPlatzRx.Match(name);
        if (m.Success && int.TryParse(m.Groups[1].Value, out int platz))
            return new PlatzhalterBezug.TabellenPlatz(platz, m.Groups[2].Value);

        return null;
    }

    /// <summary>Prüft nur die beiden K.o.-Muster — für die Quellverknüpfung (Issue #241), die die übrigen
    /// Muster nicht kennt.</summary>
    public static PlatzhalterBezug.KoSpiel? KoSpielErkennen(string name)
    {
        var m = _koSiegerRx.Match(name);
        if (m.Success && int.TryParse(m.Groups[2].Value, out int spielNr))
            return new PlatzhalterBezug.KoSpiel(m.Groups[1].Value, spielNr, Verlierer: false);

        m = _koVerliererRx.Match(name);
        if (m.Success && int.TryParse(m.Groups[2].Value, out spielNr))
            return new PlatzhalterBezug.KoSpiel(m.Groups[1].Value, spielNr, Verlierer: true);

        return null;
    }

    /// <summary>Findet die nächstgelegene VORHERGEHENDE K.o.-Runde, auf die sich ein Platzhaltertext wie
    /// "Sieger Halbfinale 1" bezieht — ein Verlierer kommt nur aus einer K.o.-Runde, ein Sieger auch aus einem
    /// Finale. Zuerst per EXAKTEM Rundennamen (Fund: <c>TeilnehmerPoolHilfsmittel</c>
    /// — bzw. deren WPF-Pendant — erzeugt diesen Text immer aus dem echten Rundennamen, nie aus einer Kurzform;
    /// keine der mitgelieferten Turniervorlagen benennt ihre Runden "VF"/"HF"/"AF", wodurch die Kürzel-Tabelle
    /// unten in der Praxis nie traf und K.o.-Platzhalter nach der ersten K.o.-Runde nie automatisch aufgelöst
    /// wurden — Nutzerfund: "Sieger/Verlierer"-Übergänge zwischen zwei K.o.-Runden funktionierten nie, weder
    /// einzeln noch simuliert, weder produktiv noch in der Vorschau). Fällt nur mangels Namenstreffer auf die
    /// alte Kürzel-zu-Eingangsteams-Tabelle zurück (z.B. "VF" → 8) — deckt Turniere ab, deren Runden tatsächlich
    /// so kurz benannt sind, und hält die bestehenden Tests unverändert grün. Bei mehreren K.o.-Zweigen gleicher
    /// Größe/gleichen Namens im selben Turnier ist das die per <see cref="TurnierRunde.Reihenfolge"/> nächste,
    /// nicht immer die erste.</summary>
    public static TurnierRunde? KoQuellrunde(
        PlatzhalterBezug.KoSpiel bezug, TurnierRunde runde, IReadOnlyList<TurnierRunde> alleRunden)
    {
        var kandidaten = alleRunden.Where(r =>
            (r.Rundenart == Rundenart.KoRunde || (!bezug.Verlierer && r.Rundenart == Rundenart.Finale))
            && r.Reihenfolge < runde.Reihenfolge);

        var perName = kandidaten
            .Where(r => string.Equals(r.Name, bezug.RundenKuerzel, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(r => r.Reihenfolge)
            .FirstOrDefault();
        if (perName is not null) return perName;

        int? eingangTeams = KuerzelZuEingangsteams(bezug.RundenKuerzel);
        if (eingangTeams is null) return null;

        return kandidaten
            .Where(r => r.EingangsMannschaftsanzahl == eingangTeams.Value)
            .OrderByDescending(r => r.Reihenfolge)
            .FirstOrDefault();
    }

    private static int? KuerzelZuEingangsteams(string kuerzel) => kuerzel.ToUpperInvariant() switch
    {
        "HF"      => 4,
        "VF"      => 8,
        "AF"      => 16,
        "FINALE"  => 2,
        _ when kuerzel.StartsWith("R", StringComparison.OrdinalIgnoreCase)
              && int.TryParse(kuerzel[1..], out int n) => n * 2,
        _         => null
    };
}
