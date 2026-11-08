namespace Fussballturnierverwaltung.Application.Stammdaten;

/// <summary>Was noch an einem Verband hängt und sein Löschen verhindert (#559).</summary>
public record VerbandBezuege(int Laender, int TurniereMitQuote);

/// <summary>Was noch an einem Land hängt und sein Löschen verhindert (#559).</summary>
public record LandBezuege(int Mannschaften, int TurniereMitQuote);

/// <summary>Verständlicher Grund, warum sich ein Stammdatensatz nicht löschen lässt (#559); null = löschbar.
/// Die Datenbank lehnt das Löschen sonst mit einer technischen EF-Meldung ab.</summary>
public static class LoeschSperre
{
    public static string? FuerVerband(string name, VerbandBezuege bezuege) => Text(
        $"Der Verband „{name}“ kann nicht gelöscht werden",
        Anzahl(bezuege.Laender, "Land", "Länder"),
        QuotenText(bezuege.TurniereMitQuote, "Verbandsquote"));

    public static string? FuerLand(string name, LandBezuege bezuege) => Text(
        $"Das Land „{name}“ kann nicht gelöscht werden",
        Anzahl(bezuege.Mannschaften, "Mannschaft", "Mannschaften"),
        QuotenText(bezuege.TurniereMitQuote, "Landesquote"));

    private static string? Text(string einleitung, params string?[] gruende)
    {
        var vorhanden = gruende.OfType<string>().ToList();
        return vorhanden.Count == 0 ? null : $"{einleitung}. Noch zugeordnet: {string.Join(", ", vorhanden)}.";
    }

    private static string? QuotenText(int turniere, string quote) =>
        turniere == 0 ? null : $"{quote} in {Anzahl(turniere, "Turnier", "Turnieren")}";

    private static string? Anzahl(int anzahl, string einzahl, string mehrzahl) => anzahl switch
    {
        0 => null,
        1 => $"1 {einzahl}",
        _ => $"{anzahl} {mehrzahl}"
    };
}
