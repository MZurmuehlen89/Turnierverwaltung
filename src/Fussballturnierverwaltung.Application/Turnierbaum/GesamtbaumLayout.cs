using Fussballturnierverwaltung.Domain.Entitaeten.Turnierbaum;

namespace Fussballturnierverwaltung.Application.Turnierbaum;

public enum GesamtbaumSeite
{
    Links,
    Mitte,
    Rechts
}

/// <summary>Position einer Karte in der Gesamtübersicht in Rastereinheiten: <paramref name="Spalte"/> zählt von
/// links (0) bis <see cref="GesamtbaumLayoutErgebnis.SpaltenAnzahl"/> − 1, <paramref name="Zeile"/> ist die
/// vertikale Kartenmitte in Kartenhöhen (0,5 = Mitte der ersten Zeile).</summary>
public record GesamtbaumPosition(int KnotenId, int Spalte, double Zeile, GesamtbaumSeite Seite);

/// <summary>Kopf einer Spalte. <paramref name="Zeile"/> ist nur bei Karten der Mitte gesetzt (Finale, Spiel um
/// Platz 3) — dort steht die Beschriftung direkt über der Karte statt oben über der Spalte.</summary>
public record GesamtbaumBeschriftung(int Spalte, string Name, double? Zeile = null);

public record GesamtbaumLayoutErgebnis(
    IReadOnlyList<GesamtbaumPosition> Positionen,
    IReadOnlyList<GesamtbaumBeschriftung> Beschriftungen,
    int SpaltenAnzahl,
    double ZeilenAnzahl);

/// <summary>
/// Gespiegeltes Layout der Turnierbaum-Gesamtübersicht (Issue #238, Vorbild WM-2026-Grafik): Das Finale steht
/// in der mittleren Spalte, alles aus seinem linken Kind wächst nach links, alles aus dem rechten nach rechts.
/// Das Spiel um Platz 3 (Issue #385) steht unter dem Finale. Knoten ohne Weg zum Finale (Direkteinsteiger,
/// unverknüpfte Alt-Bäume) werden nach ihrer Reihenfolge in der Runde hälftig auf beide Seiten verteilt, ihre
/// Kinder folgen ihnen. Reine Geometrie in Rastereinheiten — die Pixelumrechnung macht die Ansicht.
/// </summary>
public static class GesamtbaumLayout
{
    /// <summary>Abstand der Platz-3-Karte unter dem Finale, in Kartenhöhen (Platz für ihre Beschriftung).</summary>
    private const double PLATZ_DREI_ABSTAND = 1.75;

    public static GesamtbaumLayoutErgebnis Berechnen(IReadOnlyList<BracketRunde> runden)
    {
        var baumRunden = runden.Where(r => !r.IstSpielUmPlatzDrei).ToList();
        var platzDreiRunden = runden.Where(r => r.IstSpielUmPlatzDrei).ToList();
        if (baumRunden.Count == 0)
            return new GesamtbaumLayoutErgebnis([], [], 0, 0);

        int letzte = baumRunden.Count - 1;
        int mitte = letzte;
        int spaltenAnzahl = 2 * letzte + 1;

        var alle = baumRunden.SelectMany(r => r.Knoten).ToDictionary(k => k.Id);
        var rundeJeKnoten = new Dictionary<int, int>();
        for (int ri = 0; ri < baumRunden.Count; ri++)
            foreach (var k in baumRunden[ri].Knoten)
                rundeJeKnoten[k.Id] = ri;

        // ── Seiten zuordnen: von oben nach unten, Kinder erben die Seite ihres Elternknotens ──────────
        var seite = new Dictionary<int, GesamtbaumSeite>();
        var wurzeln = new List<TurnierBaumKnoten>();
        foreach (var k in baumRunden[letzte].Knoten)
        {
            seite[k.Id] = GesamtbaumSeite.Mitte;
            if (KindVon(k.LinkerKindKnotenId, alle) is { } l) SeiteVererben(l, GesamtbaumSeite.Links, alle, seite);
            if (KindVon(k.RechterKindKnotenId, alle) is { } r) SeiteVererben(r, GesamtbaumSeite.Rechts, alle, seite);
        }
        for (int ri = letzte - 1; ri >= 0; ri--)
        {
            var offen = baumRunden[ri].Knoten.Where(k => !seite.ContainsKey(k.Id)).ToList();
            var rundenKnoten = baumRunden[ri].Knoten;
            foreach (var k in offen)
            {
                int index = IndexVon(rundenKnoten, k);
                var s = index < (rundenKnoten.Count + 1) / 2 ? GesamtbaumSeite.Links : GesamtbaumSeite.Rechts;
                SeiteVererben(k, s, alle, seite);
                wurzeln.Add(k);
            }
        }

        // ── Zeilen je Seite: Blätter fortlaufend, Eltern mittig zwischen ihren Kindern ─────────────────
        var zeile = new Dictionary<int, double>();
        double linkeZeilen = ZeilenVergeben(GesamtbaumSeite.Links);
        double rechteZeilen = ZeilenVergeben(GesamtbaumSeite.Rechts);

        double ZeilenVergeben(GesamtbaumSeite s)
        {
            double blaetter = 0;
            var startKnoten = baumRunden[letzte].Knoten
                .SelectMany(f => new[]
                {
                    s == GesamtbaumSeite.Links ? KindVon(f.LinkerKindKnotenId, alle) : KindVon(f.RechterKindKnotenId, alle)
                })
                .OfType<TurnierBaumKnoten>()
                .Concat(wurzeln.Where(w => seite[w.Id] == s));
            foreach (var k in startKnoten)
                ZeileZuweisen(k, alle, zeile, ref blaetter);
            return blaetter;
        }

        double hoehe = Math.Max(Math.Max(linkeZeilen, rechteZeilen), 1);

        // Beide Seiten vertikal zentrieren.
        var positionen = new List<GesamtbaumPosition>();
        foreach (var (id, z) in zeile)
        {
            var s = seite[id];
            double versatz = (hoehe - (s == GesamtbaumSeite.Links ? linkeZeilen : rechteZeilen)) / 2;
            int ri = rundeJeKnoten[id];
            int spalte = s == GesamtbaumSeite.Links ? ri : spaltenAnzahl - 1 - ri;
            positionen.Add(new GesamtbaumPosition(id, spalte, z + versatz, s));
        }

        // ── Mitte: Finale auf halber Höhe, darunter das Spiel um Platz 3 ───────────────────────────────
        var beschriftungen = new List<GesamtbaumBeschriftung>();
        double mitteZeile = hoehe / 2 - (baumRunden[letzte].Knoten.Count - 1) / 2.0;
        foreach (var k in baumRunden[letzte].Knoten)
        {
            positionen.Add(new GesamtbaumPosition(k.Id, mitte, mitteZeile, GesamtbaumSeite.Mitte));
            mitteZeile += 1;
        }
        beschriftungen.Add(new GesamtbaumBeschriftung(mitte, RundenBezeichnung.Name(baumRunden[letzte]),
            hoehe / 2 - (baumRunden[letzte].Knoten.Count - 1) / 2.0));

        double untersteZeile = mitteZeile - 1;
        foreach (var p3 in platzDreiRunden)
        {
            double p3Zeile = untersteZeile + PLATZ_DREI_ABSTAND;
            beschriftungen.Add(new GesamtbaumBeschriftung(mitte, RundenBezeichnung.Name(p3), p3Zeile));
            foreach (var k in p3.Knoten)
            {
                positionen.Add(new GesamtbaumPosition(k.Id, mitte, p3Zeile, GesamtbaumSeite.Mitte));
                untersteZeile = p3Zeile;
                p3Zeile += 1;
            }
        }

        for (int ri = 0; ri < letzte; ri++)
        {
            string name = RundenBezeichnung.Name(baumRunden[ri]);
            beschriftungen.Add(new GesamtbaumBeschriftung(ri, name));
            beschriftungen.Add(new GesamtbaumBeschriftung(spaltenAnzahl - 1 - ri, name));
        }

        return new GesamtbaumLayoutErgebnis(positionen, beschriftungen, spaltenAnzahl,
            Math.Max(hoehe, untersteZeile + 0.5));
    }

    private static TurnierBaumKnoten? KindVon(int? id, Dictionary<int, TurnierBaumKnoten> alle) =>
        id is int wert ? alle.GetValueOrDefault(wert) : null;

    private static int IndexVon(IReadOnlyList<TurnierBaumKnoten> liste, TurnierBaumKnoten knoten)
    {
        for (int i = 0; i < liste.Count; i++)
            if (liste[i].Id == knoten.Id) return i;
        return -1;
    }

    private static void SeiteVererben(TurnierBaumKnoten knoten, GesamtbaumSeite s,
        Dictionary<int, TurnierBaumKnoten> alle, Dictionary<int, GesamtbaumSeite> seite)
    {
        if (!seite.TryAdd(knoten.Id, s)) return;
        if (KindVon(knoten.LinkerKindKnotenId, alle) is { } l) SeiteVererben(l, s, alle, seite);
        if (KindVon(knoten.RechterKindKnotenId, alle) is { } r) SeiteVererben(r, s, alle, seite);
    }

    private static void ZeileZuweisen(TurnierBaumKnoten knoten, Dictionary<int, TurnierBaumKnoten> alle,
        Dictionary<int, double> zeile, ref double blaetter)
    {
        if (zeile.ContainsKey(knoten.Id)) return;

        var links = KindVon(knoten.LinkerKindKnotenId, alle);
        var rechts = KindVon(knoten.RechterKindKnotenId, alle);
        if (links is null && rechts is null)
        {
            zeile[knoten.Id] = blaetter + 0.5;
            blaetter++;
            return;
        }

        if (links is not null) ZeileZuweisen(links, alle, zeile, ref blaetter);
        if (rechts is not null) ZeileZuweisen(rechts, alle, zeile, ref blaetter);

        double z1 = links is not null ? zeile[links.Id] : zeile[rechts!.Id];
        double z2 = rechts is not null ? zeile[rechts.Id] : z1;
        zeile[knoten.Id] = (z1 + z2) / 2;
    }
}
