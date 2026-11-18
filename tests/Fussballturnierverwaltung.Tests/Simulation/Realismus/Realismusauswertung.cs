using Fussballturnierverwaltung.Application.Simulation;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Tests.Simulation.Realismus;

/// <summary>Ausgang aus Sicht des Führenden: Anteile von Sieg, Remis und Niederlage bei <paramref name="Anzahl"/> Spielen.</summary>
internal readonly record struct Fuehrungsausgang(int Anzahl, double Sieg, double Remis, double Niederlage);

/// <summary>#678: reine Auswertungsfunktionen über die Ereignisse und Ergebnisse der Realismus-Stichprobe.</summary>
internal static class Realismusauswertung
{
    public const int ENDE_REGULAER = 90;
    public const int ANZAHL_ABSCHNITTE = 6;

    public static double Anteil(IEnumerable<SimuliertesSpiel> spiele, Func<SimuliertesSpiel, bool> bedingung)
    {
        var liste = spiele as IReadOnlyCollection<SimuliertesSpiel> ?? spiele.ToList();
        return (double)liste.Count(bedingung) / liste.Count;
    }

    /// <summary>Tore je Mannschaft nach <paramref name="minute"/>; ein Tor in der Nachspielzeit (45+2) trägt Minute 45
    /// und zählt zum Stand dieser Minute – wie in den Länderspieldaten.</summary>
    public static (int Heim, int Gast) Stand(IEnumerable<SimuliertesEreignis> ereignisse, int minute)
    {
        var tore = ereignisse.Where(e => e.Art == EreignisArt.Tor && e.Minute <= minute).ToList();
        return (tore.Count(e => e.IstHeim), tore.Count(e => !e.IstHeim));
    }

    /// <summary>Anteile der Tore der regulären Spielzeit je 15 Minuten (1–15 … 76–90, Nachspielzeit bei 45 bzw. 90).</summary>
    public static double[] TorminutenAnteile(IEnumerable<SimuliertesSpiel> spiele)
    {
        var minuten = RegulaereTore(spiele).Select(e => e.Minute).ToList();
        return Enumerable.Range(0, ANZAHL_ABSCHNITTE)
            .Select(i => (double)minuten.Count(m => (m - 1) / 15 == i) / minuten.Count).ToArray();
    }

    public static double AnteilErsteHalbzeit(IEnumerable<SimuliertesSpiel> spiele)
    {
        var minuten = RegulaereTore(spiele).Select(e => e.Minute).ToList();
        return (double)minuten.Count(m => m <= 45) / minuten.Count;
    }

    public static double AnteilNachspielzeit(IEnumerable<SimuliertesSpiel> spiele)
    {
        var tore = RegulaereTore(spiele).ToList();
        return (double)tore.Count(e => e.Nachspielminute != null) / tore.Count;
    }

    /// <summary>Länge der längsten Phase ohne Tor in Minuten (0 bis 90; Nachspielzeit-Tore zählen zu 45 bzw. 90).</summary>
    public static int LaengsteTorlosePhase(IEnumerable<SimuliertesEreignis> ereignisse)
    {
        var punkte = new[] { 0 }
            .Concat(ereignisse.Where(e => e.Art == EreignisArt.Tor && e.Minute <= ENDE_REGULAER).Select(e => e.Minute).Order())
            .Append(ENDE_REGULAER).ToList();
        return punkte.Zip(punkte.Skip(1), (a, b) => b - a).Max();
    }

    /// <summary>Wie ein Team mit Vorsprung <paramref name="vorsprung"/> nach <paramref name="minute"/> das Spiel beendet.
    /// <paramref name="filter"/> bekommt Spiel und „Führender ist Heim“.</summary>
    public static Fuehrungsausgang FuehrungsAusgang(
        IEnumerable<SimuliertesSpiel> spiele, int minute, int vorsprung, Func<SimuliertesSpiel, bool, bool>? filter = null)
    {
        int n = 0, sieg = 0, remis = 0, niederlage = 0;
        foreach (var spiel in spiele)
        {
            var (heim, gast) = Stand(spiel.Ergebnis.Ereignisse, minute);
            if (Math.Abs(heim - gast) != vorsprung) continue;
            bool fuehrerIstHeim = heim > gast;
            if (filter != null && !filter(spiel, fuehrerIstHeim)) continue;
            int differenz = (spiel.HeimTore - spiel.GastTore) * (fuehrerIstHeim ? 1 : -1);
            n++;
            if (differenz > 0) sieg++; else if (differenz == 0) remis++; else niederlage++;
        }
        return n == 0 ? new(0, 0, 0, 0) : new(n, (double)sieg / n, (double)remis / n, (double)niederlage / n);
    }

    /// <summary>Rote Karten der regulären Spielzeit je Mannschaft (Heim, Gast).</summary>
    public static (int Heim, int Gast) RoteKarten(SimuliertesSpiel spiel)
    {
        var rote = spiel.Ergebnis.Ereignisse.Where(e => e.Art == EreignisArt.RoteKarte && e.Minute <= ENDE_REGULAER).ToList();
        return (rote.Count(e => e.IstHeim), rote.Count(e => !e.IstHeim));
    }

    /// <summary>Stückweise lineare Interpolation über die nach x aufsteigenden Stützstellen; außerhalb gilt der Randwert.</summary>
    public static double Interpolieren(IReadOnlyList<(double X, double Y)> stuetzstellen, double x)
    {
        if (x <= stuetzstellen[0].X) return stuetzstellen[0].Y;
        for (int i = 1; i < stuetzstellen.Count; i++)
        {
            var (x0, y0) = stuetzstellen[i - 1];
            var (x1, y1) = stuetzstellen[i];
            if (x <= x1) return y0 + (y1 - y0) * (x - x0) / (x1 - x0);
        }
        return stuetzstellen[^1].Y;
    }

    private static IEnumerable<SimuliertesEreignis> RegulaereTore(IEnumerable<SimuliertesSpiel> spiele) =>
        spiele.SelectMany(s => s.Ergebnis.Ereignisse).Where(e => e.Art == EreignisArt.Tor && e.Minute <= ENDE_REGULAER);
}
