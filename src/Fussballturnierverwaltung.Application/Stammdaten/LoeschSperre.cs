namespace Fussballturnierverwaltung.Application.Stammdaten;

/// <summary>Was noch an einem Verband hängt und sein Löschen verhindert (#559).</summary>
public record VerbandBezuege(int Laender, QuotenVerwendung Quoten);

/// <summary>Was noch an einem Land hängt und sein Löschen verhindert (#559).</summary>
public record LandBezuege(int Mannschaften, QuotenVerwendung Quoten);

/// <summary>In wie vielen Turnieren und Vorlagen eine Quote den Datensatz nennt – getrennt, weil die Vorlagen nicht
/// in der Turnierliste stehen.</summary>
public record QuotenVerwendung(int Turniere, int Vorlagen);

/// <summary>Verständlicher Grund, warum sich ein Stammdatensatz nicht löschen lässt (#559); null = löschbar.
/// Die Datenbank lehnt das Löschen sonst mit einer technischen EF-Meldung ab.</summary>
public static class LoeschSperre
{
    public static string? FuerVerband(string name, VerbandBezuege bezuege) => Text(
        $"Der Verband „{name}“ kann nicht gelöscht werden",
        Anzahl(bezuege.Laender, "Land", "Länder"),
        QuotenText(bezuege.Quoten, "Verbandsquote"));

    public static string? FuerLand(string name, LandBezuege bezuege) => Text(
        $"Das Land „{name}“ kann nicht gelöscht werden",
        Anzahl(bezuege.Mannschaften, "Mannschaft", "Mannschaften"),
        QuotenText(bezuege.Quoten, "Landesquote"));

    private static string? Text(string einleitung, params string?[] gruende)
    {
        var vorhanden = gruende.OfType<string>().ToList();
        return vorhanden.Count == 0 ? null : $"{einleitung}. Noch zugeordnet: {string.Join(", ", vorhanden)}.";
    }

    private static string? QuotenText(QuotenVerwendung quoten, string quote)
    {
        var orte = new[] { Anzahl(quoten.Turniere, "Turnier", "Turnieren"), Anzahl(quoten.Vorlagen, "Vorlage", "Vorlagen") }
            .OfType<string>().ToList();
        return orte.Count == 0 ? null : $"{quote} in {string.Join(" und ", orte)}";
    }

    private static string? Anzahl(int anzahl, string einzahl, string mehrzahl) => anzahl switch
    {
        0 => null,
        1 => $"1 {einzahl}",
        _ => $"{anzahl} {mehrzahl}"
    };
}
