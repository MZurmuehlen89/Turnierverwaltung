using System.Text.RegularExpressions;

namespace Fussballturnierverwaltung.Application.Auslosungen;

/// <summary>Herkunft eines Teilnehmers aus einer vorangegangenen Gruppenphase (Issue #331) — Grundlage für
/// die Auslosungsregel „Keine gleiche Gruppe“ und für die Rangfolge bei „Heimrecht nach Setzliste“.
/// Folgerunden-Platzhalter tragen die Herkunft im Namen („1. Gruppe A“, „Sieger Gruppe B“, erzeugt von
/// <c>PlatzhalterHilfsmittel</c>); Sammel-Plätze aus einer Kombinationstabelle („3. Gruppe A/D/E/F“) haben
/// keine eindeutige Gruppe und liefern daher <c>null</c>.</summary>
public static class GruppenHerkunft
{
    private static readonly Regex _gruppenRangRx    = new(@"^(\d+)\. Gruppe ([A-Z0-9]+)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex _gruppenSiegerRx  = new(@"^Sieger Gruppe ([A-Z0-9]+)$",  RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static (string Gruppe, int Platz)? AusPlatzhalter(string? platzhalterName)
    {
        if (string.IsNullOrWhiteSpace(platzhalterName)) return null;
        var name = platzhalterName.Trim();

        var rang = _gruppenRangRx.Match(name);
        if (rang.Success)
            return (rang.Groups[2].Value.ToUpperInvariant(), int.Parse(rang.Groups[1].Value));

        var sieger = _gruppenSiegerRx.Match(name);
        if (sieger.Success)
            return (sieger.Groups[1].Value.ToUpperInvariant(), 1);

        return null;
    }

    /// <summary>Gruppenkennung zur Gruppennummer (1 = „A“) — dieselbe Konvention wie in den Platzhalternamen.</summary>
    public static string Buchstabe(int gruppenNr) =>
        gruppenNr <= 26 ? ((char)('A' + gruppenNr - 1)).ToString() : $"G{gruppenNr}";
}
