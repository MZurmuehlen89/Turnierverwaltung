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
        var alle = runden.SelectMany(r => r.Knoten).ToDictionary(k => k.Id);
        var zeile = new Dictionary<int, double>();
        double blaetter = 0;

        // Issue #385: Das Spiel um Platz 3 steht hinter dem Finale, gehört aber nicht zur Baumkette.
        var wurzel = runden.LastOrDefault(r => !r.IstSpielUmPlatzDrei)?.Knoten.FirstOrDefault();
        if (wurzel is not null)
            KartenZeilen.ZeileZuweisen(wurzel, alle, zeile, ref blaetter);

        // Knoten ohne Weg zur Wurzel (Direkteinsteiger, Spiel um Platz 3) in jeder Runde anhängen, nicht nur in
        // der ersten — sonst fehlen sie in der Anzeige.
        foreach (var k in runden.SelectMany(r => r.Knoten).Where(k => !zeile.ContainsKey(k.Id)))
            KartenZeilen.NaechsteZeileBelegen(k, zeile, ref blaetter);

        return new RundenbaumLayoutErgebnis(zeile, blaetter);
    }
}
