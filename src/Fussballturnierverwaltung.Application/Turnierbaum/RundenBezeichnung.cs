namespace Fussballturnierverwaltung.Application.Turnierbaum;

/// <summary>Namen, Kürzel und Knoten-Labels der Runden im Turnierbaum (#438, aus <c>TurnierBaumViewModel</c>
/// herausgelöst). Maßgeblich ist der Name der Turnierrunde; fehlt er, greift der Rückfall über die Spielanzahl.</summary>
public static class RundenBezeichnung
{
    private const string KUERZEL_PLATZ_DREI = "P3";

    public static string Name(BracketRunde runde) => runde.RundenName ?? NameAusSpielanzahl(runde.Knoten.Count);

    /// <summary>Kürzel der Runde, z.B. "AF". Endet es auf "-" (z.B. "L32-"), trennt der Strich das Kürzel von der
    /// Nummer im Knoten-Label ("L32-1").</summary>
    public static string Kuerzel(BracketRunde runde) =>
        Kuerzel(runde.RundenName, runde.Knoten.Count, runde.IstSpielUmPlatzDrei);

    /// <summary>Dasselbe Kürzel ohne Turnierbaum, für die Spiellisten (#455): aus dem Namen der Turnierrunde,
    /// ohne verwertbaren Namen aus der Anzahl ihrer Paarungen — das Spiel um Platz 3 hat wie das Finale nur
    /// eine und hieße sonst "F".</summary>
    public static string Kuerzel(string? rundenName, int anzahlSpiele, bool istSpielUmPlatzDrei = false) =>
        KuerzelAusName(rundenName) ?? (istSpielUmPlatzDrei ? KUERZEL_PLATZ_DREI : KuerzelAusSpielanzahl(anzahlSpiele));

    /// <summary>Label des Knotens an <paramref name="position"/> (ab 0, in Spielreihenfolge), z.B. "AF3"; die
    /// Runde mit nur einem Spiel trägt das Kürzel allein ("F").</summary>
    public static string KnotenLabel(BracketRunde runde, int position)
    {
        string kuerzel = Kuerzel(runde);
        return runde.Knoten.Count == 1 ? kuerzel.TrimEnd('-') : $"{kuerzel}{position + 1}";
    }

    /// <summary>Die Paarungen stehen fest vor: Eine spätere Runde nennt Platzhalter statt Mannschaften. Nur dann
    /// tragen die Knoten Labels.</summary>
    public static bool HatFestePaarungen(IReadOnlyList<BracketRunde> runden) =>
        runden.Skip(1).Any(r => r.Knoten.Any(k =>
            k.Spiel is not null && (k.Spiel.HeimPlatzhalterName is not null || k.Spiel.AuswaertsPlatzhalterName is not null)));

    /// <summary>Kürzel aus dem echten Rundennamen (Issue #365), z.B. "Playoff-Runde" → "PO", "Achtelfinale" → "AF".
    /// Unbekannte Namen: Anfangsbuchstaben der Wörter. <c>null</c> ohne Namen.</summary>
    private static string? KuerzelAusName(string? name)
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
            _ when normiert.Contains("platz 3") || normiert.Contains("platz drei") => KUERZEL_PLATZ_DREI,
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

    private static string NameAusSpielanzahl(int anzahl) => anzahl switch
    {
        1 => "Finale",
        2 => "Halbfinale",
        4 => "Viertelfinale",
        8 => "Achtelfinale",
        16 => "Letzte 32",
        32 => "Letzte 64",
        _ => $"{anzahl} Spiele"
    };

    private static string KuerzelAusSpielanzahl(int anzahl) => anzahl switch
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
