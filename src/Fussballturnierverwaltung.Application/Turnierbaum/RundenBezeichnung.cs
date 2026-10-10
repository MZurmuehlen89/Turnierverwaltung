namespace Fussballturnierverwaltung.Application.Turnierbaum;

/// <summary>Namen und Kürzel der Runden im Turnierbaum (#438, aus <c>TurnierBaumViewModel</c> herausgelöst).</summary>
public static class RundenBezeichnung
{
    /// <summary>Kürzel der Knoten-Labels aus dem echten Rundennamen (Issue #365), z.B. "Playoff-Runde" → "PO",
    /// "Achtelfinale" → "AF". Unbekannte Namen: Anfangsbuchstaben der Wörter. <c>null</c> ohne Namen — dann greift
    /// der Rückfall über die Spielanzahl.</summary>
    public static string? RundenKuerzelAusName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;

        string normiert = name.Trim().ToLowerInvariant();
        string? bekannt = normiert switch
        {
            "finale" => "F",
            "halbfinale" => "HF",
            "viertelfinale" => "VF",
            "achtelfinale" => "AF",
            "sechzehntelfinale" => "L32-",
            _ when normiert.Contains("playoff") || normiert.Contains("play-off") => "PO",
            _ => null
        };
        if (bekannt is not null) return bekannt;

        string initialen = string.Concat(name
            .Split([' ', '-', '.'], StringSplitOptions.RemoveEmptyEntries)
            .Select(w => char.ToUpperInvariant(w[0])));
        if (initialen.Length == 0) return null;
        return char.IsDigit(initialen[^1]) ? initialen + "-" : initialen;
    }

    public static string RundenAnzeigeName(int anzahl) => anzahl switch
    {
        1 => "Finale",
        2 => "Halbfinale",
        4 => "Viertelfinale",
        8 => "Achtelfinale",
        16 => "Letzte 32",
        32 => "Letzte 64",
        _ => $"{anzahl} Spiele"
    };

    public static string RundenKuerzel(int anzahl) => anzahl switch
    {
        1 => "F",
        2 => "HF",
        4 => "VF",
        8 => "AF",
        16 => "L32-",
        32 => "L64-",
        _ => $"R{anzahl}-"
    };
}
