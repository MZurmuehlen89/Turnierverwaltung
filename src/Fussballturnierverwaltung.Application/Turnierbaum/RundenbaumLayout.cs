using Fussballturnierverwaltung.Domain.Entitaeten.Turnierbaum;

namespace Fussballturnierverwaltung.Application.Turnierbaum;

/// <summary><paramref name="ZeileJeKnoten"/> ist die vertikale Kartenmitte je Knoten in Kartenhöhen (0,5 = Mitte
/// der ersten Zeile), <paramref name="ZeilenAnzahl"/> die Zahl der belegten Zeilen.</summary>
public record RundenbaumLayoutErgebnis(IReadOnlyDictionary<int, double> ZeileJeKnoten, double ZeilenAnzahl);

/// <summary>
/// Zeilen des rundenweisen Turnierbaums (#438, aus <c>TurnierBaumViewModel</c> herausgelöst): Die Blätter stehen
/// fortlaufend untereinander, jeder Elternknoten mittig zwischen seinen Kindern. Reine Geometrie in
/// Rastereinheiten wie <see cref="GesamtbaumLayout"/> — die Pixelumrechnung macht die Ansicht.
/// </summary>
public static class RundenbaumLayout
{
    public static RundenbaumLayoutErgebnis Berechnen(IReadOnlyList<BracketRunde> runden)
    {
        var alleKnoten = runden.SelectMany(r => r.Knoten).ToDictionary(k => k.Id);

        var effLinks = alleKnoten.ToDictionary(kv => kv.Key, kv => kv.Value.LinkerKindKnotenId);
        var effRechts = alleKnoten.ToDictionary(kv => kv.Key, kv => kv.Value.RechterKindKnotenId);

        var posMap = new Dictionary<int, double>();
        double blaetter = 0;

        // Issue #385: Das Spiel um Platz 3 steht hinter dem Finale, gehört aber nicht zur Baumkette.
        var wurzel = runden.LastOrDefault(r => !r.IstSpielUmPlatzDrei)?.Knoten.FirstOrDefault();
        if (wurzel != null)
            AssignierePositionen(wurzel, alleKnoten, posMap, ref blaetter, effLinks, effRechts);

        // Knoten ohne Weg zur Wurzel (Direkteinsteiger, Spiel um Platz 3) in jeder Runde anhängen, nicht nur in
        // der ersten — sonst fehlen sie in der Anzeige.
        foreach (var k in runden.SelectMany(r => r.Knoten).Where(k => !posMap.ContainsKey(k.Id)))
        {
            posMap[k.Id] = blaetter + 0.5;
            blaetter++;
        }

        return new RundenbaumLayoutErgebnis(posMap, blaetter);
    }

    private static void AssignierePositionen(
        TurnierBaumKnoten knoten,
        Dictionary<int, TurnierBaumKnoten> alle,
        Dictionary<int, double> posMap,
        ref double blaetter,
        Dictionary<int, int?> effLinks,
        Dictionary<int, int?> effRechts)
    {
        if (posMap.ContainsKey(knoten.Id)) return;

        int? lId = effLinks.GetValueOrDefault(knoten.Id);
        int? rId = effRechts.GetValueOrDefault(knoten.Id);

        if (!lId.HasValue && !rId.HasValue)
        {
            posMap[knoten.Id] = blaetter + 0.5;
            blaetter++;
            return;
        }

        if (lId.HasValue && alle.TryGetValue(lId.Value, out var links))
            AssignierePositionen(links, alle, posMap, ref blaetter, effLinks, effRechts);

        if (rId.HasValue && alle.TryGetValue(rId.Value, out var rechts))
            AssignierePositionen(rechts, alle, posMap, ref blaetter, effLinks, effRechts);

        double cy1 = lId.HasValue && posMap.TryGetValue(lId.Value, out var v1) ? v1 : 0;
        double cy2 = rId.HasValue && posMap.TryGetValue(rId.Value, out var v2) ? v2 : cy1;
        posMap[knoten.Id] = (cy1 + cy2) / 2.0;
    }
}
