using Fussballturnierverwaltung.Application.Simulation;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Tests.Simulation.Realismus;

/// <summary>Ausgang aus Sicht des Führenden: Anteile von Sieg, Remis und Niederlage bei <paramref name="Anzahl"/> Spielen.</summary>
internal readonly record struct Fuehrungsausgang(int Anzahl, double Sieg, double Remis, double Niederlage);

/// <summary>Chancen der regulären Spielzeit (#431): <paramref name="Grosschancen"/> zählt Großchancen und Elfmeter, ob
/// Tor oder vergeben; <paramref name="Tore"/> sind alle Tore mit Chancenart (Elfmetertore eingeschlossen).</summary>
internal readonly record struct Chancenbilanz(
    int Spiele, int Grosschancen, int GrosschancenTore, int Elfmeter, int ElfmeterTore, int Tore)
{
    public double GrosschancenJeTeam => Grosschancen / (2.0 * Spiele);
    public double Verwertung => Grosschancen > 0 ? (double)GrosschancenTore / Grosschancen : 0;
    public double ElfmeterJeSpiel => (double)Elfmeter / Spiele;
    public double ElfmeterQuote => Elfmeter > 0 ? (double)ElfmeterTore / Elfmeter : 0;
    public double AnteilElfmeterTore => Tore > 0 ? (double)ElfmeterTore / Tore : 0;
}

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
        // Zählschleife statt LINQ-Listen: der Jackknife (#683) ruft das je Kennzahl hundertfach für alle Spiele auf.
        int heim = 0, gast = 0;
        foreach (var e in ereignisse)
        {
            if (e.Art != EreignisArt.Tor || e.Minute > minute) continue;
            if (e.IstHeim) heim++; else gast++;
        }
        return (heim, gast);
    }

    /// <summary>Anteile der Tore der regulären Spielzeit je 15 Minuten (1–15 … 76–90, Nachspielzeit bei 45 bzw. 90).</summary>
    public static double[] TorminutenAnteile(IEnumerable<SimuliertesSpiel> spiele)
    {
        // Zählschleife statt LINQ-Listen (Jackknife, #683).
        var anzahl = new int[ANZAHL_ABSCHNITTE];
        int tore = 0;
        foreach (var e in RegulaereTore(spiele))
        {
            tore++;
            int abschnitt = (e.Minute - 1) / 15;
            if (abschnitt is >= 0 and < ANZAHL_ABSCHNITTE) anzahl[abschnitt]++;
        }
        return [.. anzahl.Select(a => (double)a / tore)];
    }

    public static double AnteilErsteHalbzeit(IEnumerable<SimuliertesSpiel> spiele)
    {
        int tore = 0, ersteHalbzeit = 0;
        foreach (var e in RegulaereTore(spiele))
        {
            tore++;
            if (e.Minute <= 45) ersteHalbzeit++;
        }
        return (double)ersteHalbzeit / tore;
    }

    public static double AnteilNachspielzeit(IEnumerable<SimuliertesSpiel> spiele)
    {
        var tore = RegulaereTore(spiele).ToList();
        return (double)tore.Count(e => e.Nachspielminute != null) / tore.Count;
    }

    /// <summary>Anteil der Spiele mit mindestens einem Tor der regulären Spielzeit ab <paramref name="minute"/> (90+ zählt mit).</summary>
    public static double AnteilMitTorAb(IEnumerable<SimuliertesSpiel> spiele, int minute) =>
        Anteil(spiele, s => s.Ergebnis.Ereignisse.Any(e => e.Art == EreignisArt.Tor && e.Minute >= minute && e.Minute <= ENDE_REGULAER));

    /// <summary>Länge der längsten Phase ohne Tor in Minuten (0 bis 90; Nachspielzeit-Tore zählen zu 45 bzw. 90).</summary>
    public static int LaengsteTorlosePhase(IEnumerable<SimuliertesEreignis> ereignisse)
    {
        var punkte = new[] { 0 }
            .Concat(ereignisse.Where(e => e.Art == EreignisArt.Tor && e.Minute <= ENDE_REGULAER).Select(e => e.Minute).Order())
            .Append(ENDE_REGULAER).ToList();
        return punkte.Zip(punkte.Skip(1), (a, b) => b - a).Max();
    }

    /// <summary>Wie ein Team mit Vorsprung <paramref name="vorsprung"/> nach <paramref name="minute"/> das Spiel beendet.
    /// <paramref name="filter"/> bekommt Spiel und „Führender ist Heim“; <paramref name="mindestens"/> zählt auch
    /// größere Vorsprünge mit (#674).</summary>
    public static Fuehrungsausgang FuehrungsAusgang(
        IEnumerable<SimuliertesSpiel> spiele, int minute, int vorsprung, Func<SimuliertesSpiel, bool, bool>? filter = null,
        bool mindestens = false)
    {
        int n = 0, sieg = 0, remis = 0, niederlage = 0;
        foreach (var spiel in spiele)
        {
            var (heim, gast) = Stand(spiel.Ergebnis.Ereignisse, minute);
            if (!VorsprungPasst(Math.Abs(heim - gast), vorsprung, mindestens)) continue;
            bool fuehrerIstHeim = heim > gast;
            if (filter != null && !filter(spiel, fuehrerIstHeim)) continue;
            int differenz = (spiel.HeimTore - spiel.GastTore) * (fuehrerIstHeim ? 1 : -1);
            n++;
            if (differenz > 0) sieg++; else if (differenz == 0) remis++; else niederlage++;
        }
        return n == 0 ? new(0, 0, 0, 0) : new(n, (double)sieg / n, (double)remis / n, (double)niederlage / n);
    }

    /// <summary>Mittlere Tore der 2. Halbzeit (#674) von Führendem und Zurückliegendem bei <paramref name="vorsprung"/>
    /// zur Pause (Halbzeitstand des Ergebnisses, wie BL); bei Gleichstand Heim und Gast. <paramref name="mindestens"/>
    /// zählt auch größere Vorsprünge mit.</summary>
    public static (double Fuehrender, double Zurueckliegender) ToreZweiteHalbzeit(
        IEnumerable<SimuliertesSpiel> spiele, int vorsprung, bool mindestens = false)
    {
        int n = 0, fuehrender = 0, zurueckliegender = 0;
        foreach (var spiel in spiele)
        {
            int heimPause = spiel.Ergebnis.HalbzeitHeimTore ?? 0, gastPause = spiel.Ergebnis.HalbzeitAuswaertsTore ?? 0;
            if (!VorsprungPasst(Math.Abs(heimPause - gastPause), vorsprung, mindestens)) continue;
            int heimDanach = spiel.HeimTore - heimPause, gastDanach = spiel.GastTore - gastPause;
            bool heimFuehrt = heimPause >= gastPause;
            n++;
            fuehrender       += heimFuehrt ? heimDanach : gastDanach;
            zurueckliegender += heimFuehrt ? gastDanach : heimDanach;
        }
        return n == 0 ? (0, 0) : ((double)fuehrender / n, (double)zurueckliegender / n);
    }

    private static bool VorsprungPasst(int abstand, int vorsprung, bool mindestens) =>
        mindestens ? abstand >= vorsprung : abstand == vorsprung;

    /// <summary>Rote Karten der regulären Spielzeit je Mannschaft (Heim, Gast).</summary>
    public static (int Heim, int Gast) RoteKarten(SimuliertesSpiel spiel)
    {
        var rote = spiel.Ergebnis.Ereignisse.Where(e => e.Art == EreignisArt.RoteKarte && e.Minute <= ENDE_REGULAER).ToList();
        return (rote.Count(e => e.IstHeim), rote.Count(e => !e.IstHeim));
    }

    /// <summary>Großchancen, Elfmeter und deren Tore der regulären Spielzeit (#431). Ereignisse ohne Chancenart
    /// (Altdaten) und Golden Goals zählen nicht.</summary>
    public static Chancenbilanz Chancen(IReadOnlyCollection<SimuliertesSpiel> spiele)
    {
        // Zählschleife statt LINQ-Listen (Jackknife, #683).
        int grosschancen = 0, grosschancenTore = 0, elfmeter = 0, elfmeterTore = 0, tore = 0;
        foreach (var spiel in spiele)
        {
            foreach (var e in spiel.Ergebnis.Ereignisse)
            {
                if (e.Minute > ENDE_REGULAER || e.Chancenart is not { } art || e.Art is not (EreignisArt.Tor or EreignisArt.Grosschance))
                    continue;
                bool tor = e.Art == EreignisArt.Tor;
                if (tor) tore++;
                if (art == Chancenart.Normal) continue;
                grosschancen++;
                if (tor) grosschancenTore++;
                if (art != Chancenart.Elfmeter) continue;
                elfmeter++;
                if (tor) elfmeterTore++;
            }
        }
        return new Chancenbilanz(spiele.Count, grosschancen, grosschancenTore, elfmeter, elfmeterTore, tore);
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

    private static IEnumerable<SimuliertesEreignis> RegulaereTore(IEnumerable<SimuliertesSpiel> spiele)
    {
        foreach (var spiel in spiele)
            foreach (var e in spiel.Ergebnis.Ereignisse)
                if (e.Art == EreignisArt.Tor && e.Minute <= ENDE_REGULAER) yield return e;
    }
}
