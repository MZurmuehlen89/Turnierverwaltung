using Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Tabelle;

/// <summary>Statistik je Team aus den Spielen mit Ergebnis, ohne Datenbankzugriff: Spiele, Ausgänge, Tore, Punkte,
/// Fairplay und die Summen über die Gegner. Sortiert werden die Teams in <see cref="TabellenRechner"/> (#505).</summary>
internal static class TeamStatistik
{
    internal static RohStats Berechnen(
        int teamId, IReadOnlyList<Spiel> spiele, PunkteRegel punkteRegel, bool? nurHeimspiele = null)
    {
        int gespielt = 0, siege = 0, unentschieden = 0, niederlagen = 0;
        int tore = 0, gegentore = 0, auswaertsTore = 0, auswaertsSiege = 0, fairplay = 0;

        foreach (var s in spiele)
        {
            if (s.Ergebnis is null) continue;
            bool istHeim = s.HeimMannschaftId == teamId;

            if (!istHeim && s.AuswaertsMannschaftId != teamId) continue;
            if (nurHeimspiele is { } nur && istHeim != nur) continue;

            gespielt++;
            int eigTore = istHeim ? s.Ergebnis.HeimToreGesamt     : s.Ergebnis.AuswaertsToreGesamt;
            int gegTore = istHeim ? s.Ergebnis.AuswaertsToreGesamt : s.Ergebnis.HeimToreGesamt;

            tore      += eigTore;
            gegentore += gegTore;

            if (!istHeim) auswaertsTore += eigTore;

            if (s.Ergebnis.EntscheidungArt == EntscheidungArt.RegulaereSpielzeit && eigTore == gegTore)
                unentschieden++;
            else if (eigTore > gegTore || s.Ergebnis.SiegerMannschaftId == teamId)
            {
                siege++;
                if (!istHeim) auswaertsSiege++;
            }
            else
                niederlagen++;

            fairplay += FairplayPunkteImSpiel(s, teamId);
        }

        int punkte = siege * punkteRegel.PunkteFuerSieg
                   + unentschieden * punkteRegel.PunkteFuerUnentschieden
                   + niederlagen * punkteRegel.PunkteFuerNiederlage;

        return new RohStats(gespielt, siege, unentschieden, niederlagen, tore, gegentore, auswaertsTore, punkte, fairplay)
        {
            AuswaertsSiege = auswaertsSiege
        };
    }

    /// <summary>Fairplay-Wertung eines Teams in einem Spiel: -(GelbeKarte * 1 + RoteKarte * 3) - der höhere Wert
    /// ist der bessere, die Regel sortiert daher absteigend.</summary>
    private static int FairplayPunkteImSpiel(Spiel spiel, int teamId) =>
        -spiel.Ereignisse
            .Where(e => e.MannschaftId == teamId)
            .Sum(e => e.EreignisArt switch
            {
                EreignisArt.GelbeKarte => 1,
                EreignisArt.RoteKarte  => 3,
                _                      => 0
            });

    /// <summary>Ergänzt je Team die Summen über alle Gegner, gegen die es bisher gespielt hat: aktuelle Punkte
    /// (Buchholz-Zahl des Schweizer Systems, Issue #329), Tordifferenz und erzielte Tore — die Kriterien der
    /// UEFA-Ligaphase ab 2024/25 (Issue #353), in der nicht jeder gegen jeden spielt und ein direkter Vergleich
    /// daher ausscheidet. Ein Gegner zählt je Spiel einmal. Ein Freilos-Spieltag erzeugt kein Spiel für das
    /// aussetzende Team (siehe <see cref="Fussballturnierverwaltung.Application.Spielplan.SchweizerSystemGenerator"/>),
    /// zählt also korrekt nicht als Gegner mit.</summary>
    internal static void GegnerSummenErgaenzen(Dictionary<int, RohStats> stats, IReadOnlyList<Spiel> abgeschlossene)
    {
        var summen = stats.Keys.ToDictionary(id => id, _ => (Punkte: 0, Tordifferenz: 0, Tore: 0));

        foreach (var s in abgeschlossene)
        {
            if (s.HeimMannschaftId is not { } heimId || s.AuswaertsMannschaftId is not { } auswaertsId) continue;
            if (!stats.TryGetValue(heimId, out var heim) || !stats.TryGetValue(auswaertsId, out var auswaerts)) continue;

            summen[heimId]      = Addieren(summen[heimId], auswaerts);
            summen[auswaertsId] = Addieren(summen[auswaertsId], heim);
        }

        foreach (var (id, summe) in summen)
        {
            stats[id] = stats[id] with
            {
                GegnerPunkte       = summe.Punkte,
                GegnerTordifferenz = summe.Tordifferenz,
                GegnerTore         = summe.Tore
            };
        }

        static (int Punkte, int Tordifferenz, int Tore) Addieren((int Punkte, int Tordifferenz, int Tore) summe, RohStats gegner) =>
            (summe.Punkte + gegner.Punkte, summe.Tordifferenz + gegner.Tore - gegner.Gegentore, summe.Tore + gegner.Tore);
    }
}

internal sealed record RohStats(
    int Spiele,
    int Siege,
    int Unentschieden,
    int Niederlagen,
    int Tore,
    int Gegentore,
    int AuswaertsTore,
    int Punkte,
    int FairplayWertung = 0,
    int GegnerPunkte = 0,
    int GegnerTordifferenz = 0,
    int GegnerTore = 0,
    int AuswaertsSiege = 0);
