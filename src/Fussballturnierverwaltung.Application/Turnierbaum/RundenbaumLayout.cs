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
            ZeileZuweisen(wurzel, alle, zeile, ref blaetter);

        // Knoten ohne Weg zur Wurzel (Direkteinsteiger, Spiel um Platz 3) in jeder Runde anhängen, nicht nur in
        // der ersten — sonst fehlen sie in der Anzeige.
        foreach (var k in runden.SelectMany(r => r.Knoten).Where(k => !zeile.ContainsKey(k.Id)))
            NaechsteZeileBelegen(k, zeile, ref blaetter);

        return new RundenbaumLayoutErgebnis(zeile, blaetter);
    }

    private static void ZeileZuweisen(
        TurnierBaumKnoten knoten, Dictionary<int, TurnierBaumKnoten> alle, Dictionary<int, double> zeile, ref double blaetter)
    {
        if (zeile.ContainsKey(knoten.Id)) return;

        var links = KindVon(knoten.LinkerKindKnotenId, alle);
        var rechts = KindVon(knoten.RechterKindKnotenId, alle);
        if (links is null && rechts is null)
        {
            NaechsteZeileBelegen(knoten, zeile, ref blaetter);
            return;
        }

        if (links is not null) ZeileZuweisen(links, alle, zeile, ref blaetter);
        if (rechts is not null) ZeileZuweisen(rechts, alle, zeile, ref blaetter);

        // #450: Mit nur einem Kind steht der Knoten auf dessen Zeile — auch wenn das Kind rechts hängt.
        double z1 = links is not null ? zeile[links.Id] : zeile[rechts!.Id];
        double z2 = rechts is not null ? zeile[rechts.Id] : z1;
        zeile[knoten.Id] = (z1 + z2) / 2.0;
    }

    private static TurnierBaumKnoten? KindVon(int? id, Dictionary<int, TurnierBaumKnoten> alle) =>
        id is int wert ? alle.GetValueOrDefault(wert) : null;

    private static void NaechsteZeileBelegen(TurnierBaumKnoten knoten, Dictionary<int, double> zeile, ref double blaetter)
    {
        zeile[knoten.Id] = blaetter + 0.5;
        blaetter++;
    }
}
