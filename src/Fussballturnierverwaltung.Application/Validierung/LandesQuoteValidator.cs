using Fussballturnierverwaltung.Application.Abstraktionen;
using Fussballturnierverwaltung.Application.Ranglisten;
using Fussballturnierverwaltung.Application.Turniere.Eingaben;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;

namespace Fussballturnierverwaltung.Application.Validierung;

public class LandesQuoteValidator
{
    /// <summary>Validiert die Landes-Quoten-Zeilen eines Turniers auf einmal (Turnierkopf). Ein Land darf mehrere
    /// Kontingente haben (#572), deren Platzbereiche in der Ligapyramide des Landes liegen und sich nicht
    /// überschneiden, dazu höchstens ein Pokalsieger-Kontingent (#583); eine Zeile für das ganze Land steht allein. <paramref name="laender"/> liefert
    /// Namen und Ranglisten (mit Abschnitten) der Länder. <paramref name="eingangsKapazitaet"/> begrenzt optional die
    /// Summe aller Zeilen (Eingang der ersten Turnierrunde).</summary>
    public ValidationErgebnis Validieren(
        IReadOnlyList<LandesQuoteZeileEingabe> zeilen, IReadOnlyList<Land> laender, int? eingangsKapazitaet = null)
    {
        var laenderNachId = laender.ToDictionary(l => l.Id);
        string[] fehler =
        [
            .. zeilen.SelectMany(z => ZeilePruefen(z, laenderNachId.GetValueOrDefault(z.LandId))),
            .. zeilen.GroupBy(z => z.LandId).SelectMany(g => LandPruefen(g.ToList(), Name(g.Key, laenderNachId))),
            .. KapazitaetPruefen(zeilen, eingangsKapazitaet),
        ];
        return fehler.Length == 0 ? ValidationErgebnis.Ok() : ValidationErgebnis.MitFehlern(fehler);
    }

    private static string Name(int landId, IReadOnlyDictionary<int, Land> laender) =>
        laender.GetValueOrDefault(landId)?.Name ?? $"Land-Id {landId}";

    private static IEnumerable<string> ZeilePruefen(LandesQuoteZeileEingabe zeile, Land? land)
    {
        if (zeile.Anzahl < 1)
        {
            yield return "Die Anzahl muss mindestens 1 sein.";
            yield break;
        }
        var name = land?.Name ?? $"Land-Id {zeile.LandId}";
        if (zeile.Pokalsieger)
        {
            foreach (var fehler in PokalsiegerPruefen(zeile, land, name))
                yield return fehler;
            yield break;
        }
        if (zeile.Spielklasse is not { } spielklasse)
            yield break;

        int abPlatz = zeile.GespeicherterAbPlatz!.Value;
        if (land?.Rangliste is not { Abschnitte.Count: > 0 } pyramide)
            yield return $"{name} hat keine Ligapyramide, eine Spielklasse lässt sich nicht wählen.";
        else if (Kontingente.Bereich(pyramide, spielklasse, abPlatz, zeile.Anzahl) is null)
            yield return $"{name}: {zeile.Anzahl} Team(s) ab Platz {abPlatz} passen nicht in Spielklasse {spielklasse}.";
    }

    /// <summary>Genau ein Platz, ohne Spielklasse; „Nachrücken ab“ liegt in der obersten Spielklasse der Pyramide.</summary>
    private static IEnumerable<string> PokalsiegerPruefen(LandesQuoteZeileEingabe zeile, Land? land, string name)
    {
        if (zeile.Anzahl != 1)
            yield return $"{name}: Das Pokalsieger-Kontingent hat genau einen Platz.";
        if (zeile.Spielklasse is not null)
            yield return $"{name}: Das Pokalsieger-Kontingent hat keine Spielklasse.";

        int abPlatz = zeile.GespeicherterAbPlatz!.Value;
        if (land?.Rangliste is not { Abschnitte.Count: > 0 } pyramide)
            yield return $"{name} hat keine Ligapyramide, ein Pokalsieger-Kontingent ist nicht möglich.";
        else if (Kontingente.Nachrueckbereich(pyramide, abPlatz) is null)
            yield return $"{name}: Nachrücken ab Platz {abPlatz} liegt nicht in der {Kontingente.Spielklassen(pyramide)[0].Name}.";
    }

    /// <summary>Mehrere Zeilen eines Landes: keine für das ganze Land, höchstens ein Pokalsieger, Bereiche je
    /// Spielklasse ohne Überschneidung.</summary>
    private static IEnumerable<string> LandPruefen(IReadOnlyList<LandesQuoteZeileEingabe> zeilen, string name)
    {
        if (zeilen.Count < 2)
            yield break;

        if (zeilen.Any(z => z.GanzesLand))
        {
            yield return $"{name}: Eine Quote ohne Spielklasse gilt für das ganze Land, weitere Quoten sind dann nicht möglich.";
            yield break;
        }

        if (zeilen.Count(z => z.Pokalsieger) > 1)
            yield return $"{name}: Ein Land hat höchstens ein Pokalsieger-Kontingent.";

        foreach (var klasse in zeilen.Where(z => z.Spielklasse is not null).GroupBy(z => z.Spielklasse))
        {
            var bereiche = klasse
                .Select(z => (Von: z.GespeicherterAbPlatz!.Value, z.Anzahl))
                .Select(b => (b.Von, Bis: b.Von + b.Anzahl - 1))
                .OrderBy(b => b.Von)
                .ToList();
            if (bereiche.Zip(bereiche.Skip(1)).Any(p => p.Second.Von <= p.First.Bis))
                yield return $"{name}: Die Quoten in Spielklasse {klasse.Key} überschneiden sich.";
        }
    }

    private static IEnumerable<string> KapazitaetPruefen(IReadOnlyList<LandesQuoteZeileEingabe> zeilen, int? eingangsKapazitaet)
    {
        var summe = zeilen.Sum(z => z.Anzahl);
        if (eingangsKapazitaet is { } kapazitaet && summe > kapazitaet)
            yield return $"Die Summe der Landes-Quoten ({summe}) übersteigt die Eingangskapazität der ersten Runde ({kapazitaet}).";
    }
}
