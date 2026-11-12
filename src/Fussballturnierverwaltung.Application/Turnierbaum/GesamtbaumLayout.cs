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

        return new Aufbau(baumRunden, platzDreiRunden).Berechnen();
    }

    /// <summary>Ein Rechenlauf: hält die Zwischenstände (Seite und Zeile je Knoten), die die Phasen
    /// nacheinander füllen.</summary>
    private sealed class Aufbau
    {
        private readonly List<BracketRunde> _baumRunden;
        private readonly List<BracketRunde> _platzDreiRunden;
        private readonly int _letzte;
        private readonly int _spaltenAnzahl;
        private readonly Dictionary<int, TurnierBaumKnoten> _alle;
        private readonly Dictionary<int, int> _rundeJeKnoten = [];
        private readonly Dictionary<int, GesamtbaumSeite> _seite = [];
        private readonly List<TurnierBaumKnoten> _wurzeln = [];
        private readonly Dictionary<int, double> _zeile = [];
        private readonly List<GesamtbaumPosition> _positionen = [];
        private readonly List<GesamtbaumBeschriftung> _beschriftungen = [];

        public Aufbau(List<BracketRunde> baumRunden, List<BracketRunde> platzDreiRunden)
        {
            _baumRunden = baumRunden;
            _platzDreiRunden = platzDreiRunden;
            _letzte = baumRunden.Count - 1;
            _spaltenAnzahl = 2 * _letzte + 1;

            _alle = baumRunden.SelectMany(r => r.Knoten).ToDictionary(k => k.Id);
            for (int ri = 0; ri < baumRunden.Count; ri++)
                foreach (var k in baumRunden[ri].Knoten)
                    _rundeJeKnoten[k.Id] = ri;
        }

        private int Mitte => _letzte;

        public GesamtbaumLayoutErgebnis Berechnen()
        {
            SeitenZuordnen();

            double linkeZeilen = ZeilenVergeben(GesamtbaumSeite.Links);
            double rechteZeilen = ZeilenVergeben(GesamtbaumSeite.Rechts);
            double hoehe = Math.Max(Math.Max(linkeZeilen, rechteZeilen), 1);

            SeitenPositionieren(hoehe, linkeZeilen, rechteZeilen);
            double untersteZeile = FinaleSetzen(hoehe);
            untersteZeile = PlatzDreiSetzen(untersteZeile);
            SeitenBeschriften();

            return new GesamtbaumLayoutErgebnis(_positionen, _beschriftungen, _spaltenAnzahl,
                Math.Max(hoehe, untersteZeile + 0.5));
        }

        // ── Seiten zuordnen: von oben nach unten, Kinder erben die Seite ihres Elternknotens ──────────
        private void SeitenZuordnen()
        {
            foreach (var k in _baumRunden[_letzte].Knoten)
            {
                _seite[k.Id] = GesamtbaumSeite.Mitte;
                if (KindVon(k.LinkerKindKnotenId) is { } l) SeiteVererben(l, GesamtbaumSeite.Links);
                if (KindVon(k.RechterKindKnotenId) is { } r) SeiteVererben(r, GesamtbaumSeite.Rechts);
            }
            for (int ri = _letzte - 1; ri >= 0; ri--)
            {
                var offen = _baumRunden[ri].Knoten.Where(k => !_seite.ContainsKey(k.Id)).ToList();
                var rundenKnoten = _baumRunden[ri].Knoten;
                foreach (var k in offen)
                {
                    int index = IndexVon(rundenKnoten, k);
                    var s = index < (rundenKnoten.Count + 1) / 2 ? GesamtbaumSeite.Links : GesamtbaumSeite.Rechts;
                    SeiteVererben(k, s);
                    _wurzeln.Add(k);
                }
            }
        }

        // ── Zeilen je Seite (KartenZeilen): Blätter fortlaufend, Eltern mittig zwischen ihren Kindern ───
        private double ZeilenVergeben(GesamtbaumSeite s)
        {
            double blaetter = 0;
            var startKnoten = _baumRunden[_letzte].Knoten
                .Select(f => KindVon(s == GesamtbaumSeite.Links ? f.LinkerKindKnotenId : f.RechterKindKnotenId))
                .OfType<TurnierBaumKnoten>()
                .Concat(_wurzeln.Where(w => _seite[w.Id] == s));
            foreach (var k in startKnoten)
                KartenZeilen.ZeileZuweisen(k, _alle, _zeile, ref blaetter);
            return blaetter;
        }

        // Beide Seiten vertikal zentrieren.
        private void SeitenPositionieren(double hoehe, double linkeZeilen, double rechteZeilen)
        {
            foreach (var (id, z) in _zeile)
            {
                var s = _seite[id];
                double versatz = (hoehe - (s == GesamtbaumSeite.Links ? linkeZeilen : rechteZeilen)) / 2;
                int ri = _rundeJeKnoten[id];
                int spalte = s == GesamtbaumSeite.Links ? ri : _spaltenAnzahl - 1 - ri;
                _positionen.Add(new GesamtbaumPosition(id, spalte, z + versatz, s));
            }
        }

        // ── Mitte: Finale auf halber Höhe, darunter das Spiel um Platz 3 ───────────────────────────────
        private double FinaleSetzen(double hoehe)
        {
            var finalRunde = _baumRunden[_letzte];
            double ersteZeile = hoehe / 2 - (finalRunde.Knoten.Count - 1) / 2.0;
            double mitteZeile = ersteZeile;
            foreach (var k in finalRunde.Knoten)
            {
                _positionen.Add(new GesamtbaumPosition(k.Id, Mitte, mitteZeile, GesamtbaumSeite.Mitte));
                mitteZeile += 1;
            }
            _beschriftungen.Add(new GesamtbaumBeschriftung(Mitte, RundenBezeichnung.Name(finalRunde), ersteZeile));

            return mitteZeile - 1;
        }

        private double PlatzDreiSetzen(double untersteZeile)
        {
            foreach (var p3 in _platzDreiRunden)
            {
                double p3Zeile = untersteZeile + PLATZ_DREI_ABSTAND;
                _beschriftungen.Add(new GesamtbaumBeschriftung(Mitte, RundenBezeichnung.Name(p3), p3Zeile));
                foreach (var k in p3.Knoten)
                {
                    _positionen.Add(new GesamtbaumPosition(k.Id, Mitte, p3Zeile, GesamtbaumSeite.Mitte));
                    untersteZeile = p3Zeile;
                    p3Zeile += 1;
                }
            }
            return untersteZeile;
        }

        private void SeitenBeschriften()
        {
            for (int ri = 0; ri < _letzte; ri++)
            {
                string name = RundenBezeichnung.Name(_baumRunden[ri]);
                _beschriftungen.Add(new GesamtbaumBeschriftung(ri, name));
                _beschriftungen.Add(new GesamtbaumBeschriftung(_spaltenAnzahl - 1 - ri, name));
            }
        }

        // ── Hilfsmethoden ─────────────────────────────────────────────────────

        private TurnierBaumKnoten? KindVon(int? id) => id is int wert ? _alle.GetValueOrDefault(wert) : null;

        private static int IndexVon(IReadOnlyList<TurnierBaumKnoten> liste, TurnierBaumKnoten knoten)
        {
            for (int i = 0; i < liste.Count; i++)
                if (liste[i].Id == knoten.Id) return i;
            return -1;
        }

        private void SeiteVererben(TurnierBaumKnoten knoten, GesamtbaumSeite s)
        {
            if (!_seite.TryAdd(knoten.Id, s)) return;
            if (KindVon(knoten.LinkerKindKnotenId) is { } l) SeiteVererben(l, s);
            if (KindVon(knoten.RechterKindKnotenId) is { } r) SeiteVererben(r, s);
        }
    }
}
