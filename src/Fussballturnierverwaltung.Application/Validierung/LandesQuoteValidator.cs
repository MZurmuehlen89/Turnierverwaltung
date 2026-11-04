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
    /// Summe aller Zeilen (Eingang der ersten Turnierrunde). Fehler einer Zeile tragen Feld und Zeilenindex (#595),
    /// Prüfungen über mehrere Zeilen keinen Feldbezug.</summary>
    public ValidationErgebnis Validieren(
        IReadOnlyList<LandesQuoteZeileEingabe> zeilen, IReadOnlyList<Land> laender, int? eingangsKapazitaet = null)
    {
        var laenderNachId = laender.ToDictionary(l => l.Id);
        Feldfehler[] fehler =
        [
            .. zeilen.SelectMany((z, i) => ZeilePruefen(z, laenderNachId.GetValueOrDefault(z.LandId))
                .Select(f => f with { Zeile = i })),
            .. zeilen.GroupBy(z => z.LandId).SelectMany(g => LandPruefen(g.ToList(), Name(g.Key, laenderNachId))),
            .. KapazitaetPruefen(zeilen, eingangsKapazitaet),
        ];
        return ValidationErgebnis.Aus(fehler);
    }

    private static string Name(int landId, IReadOnlyDictionary<int, Land> laender) =>
        laender.GetValueOrDefault(landId)?.Name ?? $"Land-Id {landId}";

    private static IEnumerable<Feldfehler> ZeilePruefen(LandesQuoteZeileEingabe zeile, Land? land)
    {
        if (zeile.Anzahl < 1)
        {
            yield return new("Die Anzahl muss mindestens 1 sein.", nameof(zeile.Anzahl));
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
            yield return new($"{name} hat keine Ligapyramide, eine Spielklasse lässt sich nicht wählen.", nameof(zeile.Spielklasse));
        else if (Kontingente.Bereich(pyramide, spielklasse, abPlatz, zeile.Anzahl) is null)
            yield return new($"{name}: {zeile.Anzahl} Team(s) ab Platz {abPlatz} passen nicht in Spielklasse {spielklasse}.",
                nameof(zeile.AbPlatz));
    }

    /// <summary>Genau ein Platz, ohne Spielklasse; „Nachrücken ab“ liegt in der obersten Spielklasse der Pyramide.</summary>
    private static IEnumerable<Feldfehler> PokalsiegerPruefen(LandesQuoteZeileEingabe zeile, Land? land, string name)
    {
        if (zeile.Anzahl != 1)
            yield return new($"{name}: Das Pokalsieger-Kontingent hat genau einen Platz.", nameof(zeile.Anzahl));
        if (zeile.Spielklasse is not null)
            yield return new($"{name}: Das Pokalsieger-Kontingent hat keine Spielklasse.", nameof(zeile.Spielklasse));

        int abPlatz = zeile.GespeicherterAbPlatz!.Value;
        if (land?.Rangliste is not { Abschnitte.Count: > 0 } pyramide)
            yield return new($"{name} hat keine Ligapyramide, ein Pokalsieger-Kontingent ist nicht möglich.", nameof(zeile.Spielklasse));
        else if (Kontingente.Nachrueckbereich(pyramide, abPlatz) is null)
            yield return new($"{name}: Nachrücken ab Platz {abPlatz} liegt nicht in der {Kontingente.Spielklassen(pyramide)[0].Name}.",
                nameof(zeile.AbPlatz));
    }

    /// <summary>Mehrere Zeilen eines Landes: keine für das ganze Land, höchstens ein Pokalsieger, Bereiche je
    /// Spielklasse ohne Überschneidung.</summary>
    private static IEnumerable<Feldfehler> LandPruefen(IReadOnlyList<LandesQuoteZeileEingabe> zeilen, string name)
    {
        if (zeilen.Count < 2)
            yield break;

        if (zeilen.Any(z => z.GanzesLand))
        {
            yield return new($"{name}: Eine Quote ohne Spielklasse gilt für das ganze Land, weitere Quoten sind dann nicht möglich.");
            yield break;
        }

        if (zeilen.Count(z => z.Pokalsieger) > 1)
            yield return new($"{name}: Ein Land hat höchstens ein Pokalsieger-Kontingent.");

        foreach (var klasse in zeilen.Where(z => z.Spielklasse is not null).GroupBy(z => z.Spielklasse))
        {
            var bereiche = klasse
                .Select(z => (Von: z.GespeicherterAbPlatz!.Value, z.Anzahl))
                .Select(b => (b.Von, Bis: b.Von + b.Anzahl - 1))
                .OrderBy(b => b.Von)
                .ToList();
            if (bereiche.Zip(bereiche.Skip(1)).Any(p => p.Second.Von <= p.First.Bis))
                yield return new($"{name}: Die Quoten in Spielklasse {klasse.Key} überschneiden sich.");
        }
    }

    private static IEnumerable<Feldfehler> KapazitaetPruefen(IReadOnlyList<LandesQuoteZeileEingabe> zeilen, int? eingangsKapazitaet)
    {
        var summe = zeilen.Sum(z => z.Anzahl);
        if (eingangsKapazitaet is { } kapazitaet && summe > kapazitaet)
            yield return new($"Die Summe der Landes-Quoten ({summe}) übersteigt die Eingangskapazität der ersten Runde ({kapazitaet}).");
    }
}
