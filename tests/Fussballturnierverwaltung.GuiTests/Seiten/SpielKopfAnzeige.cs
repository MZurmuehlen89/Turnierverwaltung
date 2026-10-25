using System.Text.RegularExpressions;

namespace Fussballturnierverwaltung.GuiTests.Seiten;

/// <summary>Ein Spielkopf, wie ihn die Einzelspiel-Simulation zeigt (#536). <see cref="Score"/> ist der große Stand in
/// der Box – nach Elfmeterschießen der Elfmeterstand –, <see cref="Entscheidung"/> der Hinweis darunter („im
/// Elfmeterschießen“, im Gesamtkopf „nach 180 Minuten“ usw.), <see cref="Zwischenstaende"/> der Streifen
/// („HZ 1:0 · 90' 1:1 · n.V. 2:1“). Leere Felder sind leere Texte.</summary>
public sealed partial record SpielKopfAnzeige(
    string Heim, string Auswaerts, string Score, string Entscheidung = "", string Zwischenstaende = "")
{
    public bool ImElfmeterschiessen => Entscheidung.StartsWith("im Elfmeterschießen", StringComparison.Ordinal);

    /// <summary>Stand nach der regulären Spielzeit.</summary>
    public (int Heim, int Auswaerts)? ToreNach90 => Zwischenstand("90'") ?? ToreOhneElfmeter(Score);

    /// <summary>Endstand ohne Elfmeterschießen, also einschließlich Verlängerung.</summary>
    public (int Heim, int Auswaerts)? Tore => Zwischenstand("n.V.") ?? Zwischenstand("90'") ?? ToreOhneElfmeter(Score);

    public (int Heim, int Auswaerts)? Elfmeter => ImElfmeterschiessen ? Spielstand.Tore(Score) : null;

    private (int, int)? ToreOhneElfmeter(string score) => ImElfmeterschiessen ? null : Spielstand.Tore(score);

    private (int, int)? Zwischenstand(string marke) =>
        Zwischenstaende.Split('·', StringSplitOptions.TrimEntries)
            .Select(teil => ZwischenstandMuster().Match(teil))
            .Where(m => m.Success && m.Groups[1].Value == marke)
            .Select(m => ((int, int)?)(int.Parse(m.Groups[2].Value), int.Parse(m.Groups[3].Value)))
            .FirstOrDefault();

    [GeneratedRegex(@"^(\S+)\s+(\d+):(\d+)$")]
    private static partial Regex ZwischenstandMuster();
}

/// <summary>Was Gesamtkopf und Siegerzeile einer Hin-/Rückspiel-Paarung zeigen müssen, abgeleitet aus den beiden
/// Spielköpfen – zustandslos, damit es ohne laufende App getestet werden kann.</summary>
public static class HinUndRueckspiel
{
    public const string UNENTSCHIEDEN = "Unentschieden";

    /// <summary>Gesamtstand aus Sicht des Hinspiel-Heimteams: Hinspiel plus Rückspiel einschließlich Verlängerung (im
    /// Rückspiel hat das Hinspiel-Heimteam Auswärtsrecht). Sieger hat mehr Tore; bei Gleichstand entscheidet ein
    /// Elfmeterschießen im Rückspiel, ohne eines bleibt es unentschieden.</summary>
    public static (string Score, string Siegerzeile) Erwartet(SpielKopfAnzeige hinspiel, SpielKopfAnzeige rueckspiel)
    {
        var (hinHeim, hinAuswaerts) = hinspiel.Tore ?? throw Unlesbar(hinspiel);
        var (rueckHeim, rueckAuswaerts) = rueckspiel.Tore ?? throw Unlesbar(rueckspiel);
        int heim = hinHeim + rueckAuswaerts;
        int auswaerts = hinAuswaerts + rueckHeim;

        string? sieger = (heim - auswaerts, rueckspiel.Elfmeter) switch
        {
            ( > 0, _) => hinspiel.Heim,
            ( < 0, _) => hinspiel.Auswaerts,
            (_, (int elfHeim, int elfAuswaerts)) when elfHeim != elfAuswaerts =>
                elfHeim > elfAuswaerts ? rueckspiel.Heim : rueckspiel.Auswaerts,
            _ => null,
        };
        return ($"{heim}:{auswaerts}", sieger is null ? UNENTSCHIEDEN : $"Sieger: {sieger}");
    }

    /// <summary>Ob es nach 180 Minuten unentschieden steht – erst dann greift das Regelwerk.</summary>
    public static bool GleichstandNach180(SpielKopfAnzeige hinspiel, SpielKopfAnzeige rueckspiel)
    {
        var (hinHeim, hinAuswaerts) = hinspiel.ToreNach90 ?? throw Unlesbar(hinspiel);
        var (rueckHeim, rueckAuswaerts) = rueckspiel.ToreNach90 ?? throw Unlesbar(rueckspiel);
        return hinHeim + rueckAuswaerts == hinAuswaerts + rueckHeim;
    }

    private static FormatException Unlesbar(SpielKopfAnzeige kopf) => new($"Spielstand nicht lesbar: {kopf}");
}
