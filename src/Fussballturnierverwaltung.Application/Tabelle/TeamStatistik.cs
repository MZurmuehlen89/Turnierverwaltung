using Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Tabelle;

/// <summary>Statistik je Team aus den Spielen mit Ergebnis, ohne Datenbankzugriff: Spiele, Ausgänge, Tore, Punkte,
/// Fairplay und die Summen über die Gegner. Sortiert werden die Teams in <see cref="TabellenRechner"/> (#505).</summary>
internal static class TeamStatistik
{
    /// <summary>Statistik von <paramref name="teamId"/> aus seinen Spielen mit Ergebnis. Mit
    /// <paramref name="nurHeimspiele"/> zählt nur die Heim- (<c>true</c>) bzw. nur die Auswärtsseite (<c>false</c>).
    /// Die Gegner-Summen fehlen noch, sie ergänzt <see cref="GegnerSummenErgaenzen"/>.</summary>
    internal static RohStats Berechnen(
        int teamId, IReadOnlyList<Spiel> spiele, PunkteRegel punkteRegel, bool? nurHeimspiele = null)
    {
        var zaehler = new Zaehler(teamId);
        foreach (var spiel in spiele.Where(s => ZaehltFuerTeam(s, teamId, nurHeimspiele)))
            zaehler.Hinzufuegen(spiel);
        return zaehler.Ergebnis(punkteRegel);
    }

    /// <summary>Ein Spiel zählt, wenn es ein Ergebnis hat, das Team beteiligt ist und – mit
    /// <paramref name="nurHeimspiele"/> – auf der gewünschten Seite steht.</summary>
    private static bool ZaehltFuerTeam(Spiel spiel, int teamId, bool? nurHeimspiele)
    {
        if (spiel.Ergebnis is null) return false;
        bool istHeim = spiel.HeimMannschaftId == teamId;

        if (!istHeim && spiel.AuswaertsMannschaftId != teamId) return false;
        return nurHeimspiele is not { } nur || istHeim == nur;
    }

    private enum Ausgang { Sieg, Unentschieden, Niederlage }

    /// <summary>Ausgang aus Sicht des Teams: unentschieden nur nach regulärer Spielzeit bei Torgleichheit; sonst
    /// gewinnt, wer mehr Tore hat oder als Sieger eingetragen ist (Verlängerung, Elfmeterschießen).</summary>
    private static Ausgang AusgangErmitteln(SpielErgebnis ergebnis, int teamId, int eigeneTore, int gegentore)
    {
        if (ergebnis.EntscheidungArt == EntscheidungArt.RegulaereSpielzeit && eigeneTore == gegentore)
            return Ausgang.Unentschieden;
        return eigeneTore > gegentore || ergebnis.SiegerMannschaftId == teamId ? Ausgang.Sieg : Ausgang.Niederlage;
    }

    /// <summary>Summiert die Spiele eines Teams auf – nur solche, die nach <see cref="ZaehltFuerTeam"/> zählen.</summary>
    private sealed class Zaehler(int teamId)
    {
        private int _spiele, _siege, _unentschieden, _niederlagen;
        private int _tore, _gegentore, _auswaertsTore, _auswaertsSiege, _fairplay;

        public void Hinzufuegen(Spiel spiel)
        {
            var ergebnis = spiel.Ergebnis!;
            bool istHeim = spiel.HeimMannschaftId == teamId;
            int eigeneTore = istHeim ? ergebnis.HeimToreGesamt      : ergebnis.AuswaertsToreGesamt;
            int gegentore  = istHeim ? ergebnis.AuswaertsToreGesamt : ergebnis.HeimToreGesamt;

            _spiele++;
            _tore      += eigeneTore;
            _gegentore += gegentore;
            if (!istHeim) _auswaertsTore += eigeneTore;

            AusgangZaehlen(AusgangErmitteln(ergebnis, teamId, eigeneTore, gegentore), istHeim);
            _fairplay += FairplayPunkteImSpiel(spiel, teamId);
        }

        private void AusgangZaehlen(Ausgang ausgang, bool istHeim)
        {
            switch (ausgang)
            {
                case Ausgang.Sieg:
                    _siege++;
                    if (!istHeim) _auswaertsSiege++;
                    break;
                case Ausgang.Unentschieden:
                    _unentschieden++;
                    break;
                default:
                    _niederlagen++;
                    break;
            }
        }

        public RohStats Ergebnis(PunkteRegel punkteRegel)
        {
            int punkte = _siege * punkteRegel.PunkteFuerSieg
                       + _unentschieden * punkteRegel.PunkteFuerUnentschieden
                       + _niederlagen * punkteRegel.PunkteFuerNiederlage;

            return new RohStats(_spiele, _siege, _unentschieden, _niederlagen, _tore, _gegentore, _auswaertsTore, punkte, _fairplay)
            {
                AuswaertsSiege = _auswaertsSiege
            };
        }
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
