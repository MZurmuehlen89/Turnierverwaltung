using Fussballturnierverwaltung.Domain.Entitaeten.Turnierbaum;

namespace Fussballturnierverwaltung.Application.Turnierbaum;

/// <summary><paramref name="ZeileJeKnoten"/> ist die vertikale Kartenmitte je Knoten in Kartenhöhen (0,5 = Mitte
/// der ersten Zeile), <paramref name="ZeilenAnzahl"/> die Zahl der belegten Zeilen.
/// <paramref name="VorgaengerJeKnoten"/> sind die Karten, aus denen die Linien der rundenweisen Ansicht in eine Karte
/// führen (#567).</summary>
public record RundenbaumLayoutErgebnis(IReadOnlyDictionary<int, double> ZeileJeKnoten, double ZeilenAnzahl,
    IReadOnlyDictionary<int, KartenVorgaenger> VorgaengerJeKnoten);

/// <summary>Die Karten, aus denen in der rundenweisen Ansicht Linien in eine Karte führen — meist ihre Kindknoten,
/// beim Spiel um Platz 3 nicht (#567).</summary>
public readonly record struct KartenVorgaenger(int? Links, int? Rechts);

/// <summary>
/// Zeilen des rundenweisen Turnierbaums (#438, aus <c>TurnierBaumViewModel</c> herausgelöst): Die Blätter stehen
/// fortlaufend untereinander, jeder Elternknoten mittig zwischen seinen Kindern. Reine Geometrie in
/// Rastereinheiten wie <see cref="GesamtbaumLayout"/> — die Pixelumrechnung macht die Ansicht.
/// </summary>
public static class RundenbaumLayout
{
    public static RundenbaumLayoutErgebnis Berechnen(IReadOnlyList<BracketRunde> runden)
    {
        var alle = runden.SelectMany(r => r.Knoten).ToDictionary(k => k.Id);
        var zeile = new Dictionary<int, double>();
        double blaetter = 0;

        // Issue #385: Das Spiel um Platz 3 gehört nicht zur Baumkette, die Wurzel ist das Finale.
        var wurzel = runden.LastOrDefault(r => !r.IstSpielUmPlatzDrei)?.Knoten.FirstOrDefault();
        var platzDrei = runden.Where(r => r.IstSpielUmPlatzDrei).SelectMany(r => r.Knoten).ToList();
        if (wurzel is not null)
        {
            KartenZeilen.ZeileZuweisen(wurzel, alle, zeile, ref blaetter);
            // #567: Platz 3 steht wie bei kicker.de auf der Höhe des Finales, nicht in einer eigenen Zeile.
            foreach (var k in platzDrei)
                zeile[k.Id] = zeile[wurzel.Id];
        }

        // Knoten ohne Weg zur Wurzel (Direkteinsteiger) in jeder Runde anhängen, nicht nur in der ersten — sonst
        // fehlen sie in der Anzeige.
        foreach (var k in runden.SelectMany(r => r.Knoten).Where(k => !zeile.ContainsKey(k.Id)))
            KartenZeilen.NaechsteZeileBelegen(k, zeile, ref blaetter);

        return new RundenbaumLayoutErgebnis(zeile, blaetter, Vorgaenger(alle.Values, wurzel, platzDrei));
    }

    /// <summary>#567: Die Kette Halbfinale → Spiel um Platz 3 → Finale gibt es nur in der Anzeige — im Baum hängt das
    /// Finale an den Halbfinals, und das Spiel um Platz 3 hat keine Kinder.</summary>
    private static Dictionary<int, KartenVorgaenger> Vorgaenger(
        IEnumerable<TurnierBaumKnoten> alle, TurnierBaumKnoten? wurzel, IReadOnlyList<TurnierBaumKnoten> platzDrei)
    {
        var vorgaenger = alle.ToDictionary(k => k.Id, k => new KartenVorgaenger(k.LinkerKindKnotenId, k.RechterKindKnotenId));
        if (wurzel is null || platzDrei.Count == 0) return vorgaenger;

        foreach (var k in platzDrei)
            vorgaenger[k.Id] = vorgaenger[wurzel.Id];
        vorgaenger[wurzel.Id] = new KartenVorgaenger(platzDrei[0].Id, platzDrei.Count > 1 ? platzDrei[^1].Id : null);
        return vorgaenger;
    }
}
