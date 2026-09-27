using System.Text.Json;
using System.Text.RegularExpressions;

namespace Fussballturnierverwaltung.Application.Turniere;

/// <summary>Typisierter Zugriff auf <see cref="Domain.Entitaeten.Turniere.TurnierRunde.KombinationstabelleJson"/>
/// (Issue #372) — die feste Baum-Zuordnung der besten Gruppendritten nach realem Vorbild (UEFA EURO 2016+,
/// FIFA WM 2026 Annex C). <see cref="Plaetze"/> sind die Platzhalter der Runde (z.B. "3. Gruppe A/D/E/F"),
/// <see cref="Kombinationen"/> bildet die sortierten Gruppenbuchstaben der qualifizierten Dritten (z.B. "CDEF")
/// auf die Gruppe je Platz ab — positionsgleich zu <see cref="Plaetze"/>.</summary>
public sealed record Kombinationstabelle(
    IReadOnlyList<string> Plaetze,
    IReadOnlyDictionary<string, IReadOnlyList<string>> Kombinationen)
{
    private static readonly JsonSerializerOptions _optionen = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    // "3. Gruppe A/D/E/F" → Platzierung 3, Kandidaten A, D, E, F (mindestens zwei Gruppen, sonst ist es ein
    // gewöhnlicher Gruppenrang "3. Gruppe A").
    private static readonly Regex _platzRx = new(@"^(\d+)\. Gruppe ([A-Z](?:/[A-Z])+)$", RegexOptions.Compiled);

    /// <summary>Anzahl der besten Dritten, die in die Runde einziehen (Länge jedes Kombinationsschlüssels).</summary>
    public int AnzahlBeste => Kombinationen.Keys.FirstOrDefault()?.Length ?? 0;

    public static Kombinationstabelle? AusJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            return JsonSerializer.Deserialize<Kombinationstabelle>(json, _optionen);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public string ZuJson() => JsonSerializer.Serialize(this, _optionen);

    public static string Schluessel(IEnumerable<string> gruppen) => string.Concat(gruppen.OrderBy(g => g, StringComparer.Ordinal));

    /// <summary>Erkennt einen Kombinations-Platzhalter und liefert dessen Platzierung (z.B. 3).</summary>
    public static bool IstPlatz(string platzhalter, out int platzierung)
    {
        var m = _platzRx.Match(platzhalter);
        platzierung = m.Success ? int.Parse(m.Groups[1].Value) : 0;
        return m.Success;
    }

    /// <summary>Gruppe, deren Dritter bei den qualifizierten Gruppen <paramref name="qualifizierteGruppen"/>
    /// den Platz <paramref name="platzhalter"/> einnimmt — null, wenn Platz oder Kombination nicht in der
    /// Tabelle stehen.</summary>
    public string? GruppeFuer(string platzhalter, IEnumerable<string> qualifizierteGruppen)
    {
        int index = Plaetze.ToList().IndexOf(platzhalter);
        if (index < 0) return null;
        return Kombinationen.TryGetValue(Schluessel(qualifizierteGruppen), out var zuordnung) && index < zuordnung.Count
            ? zuordnung[index]
            : null;
    }
}
