using Fussballturnierverwaltung.Application.Spielplan;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Tabelle;

public static class RangPrognose
{
    /// <summary>Setzt <see cref="TabellenEintrag.BesterRang"/>/<see cref="TabellenEintrag.SchlechtesterRang"/>
    /// einer Liga-Tabelle über die Punkte-Schranken (siehe <see cref="LigaTabelleMitSicherheitBerechnenAsync"/>).
    /// Bleibt aus, solange der Rest der Runde unbekannt ist (noch unaufgelöste Platzhalter-Spiele, Schweizer
    /// System ohne Spieltag-Anzahl).</summary>
    public static void RangSchrankenSetzen(
        IReadOnlyList<TabellenEintrag> tabelle, TurnierRunde? runde, IReadOnlyList<Spiel> alleSpiele, PunkteRegel punkteRegel)
    {
        var verbleibend = VerbleibendeSpieleProTeam(runde, alleSpiele, tabelle);
        if (verbleibend is null) return;

        if (verbleibend.Values.All(n => n == 0))
        {
            // alles gespielt - jeder Rang ist final
            foreach (var e in tabelle) e.BesterRang = e.SchlechtesterRang = e.Rang;
            return;
        }

        int maxProSpiel = Math.Max(punkteRegel.PunkteFuerSieg, Math.Max(punkteRegel.PunkteFuerUnentschieden, punkteRegel.PunkteFuerNiederlage));
        int minProSpiel = Math.Min(punkteRegel.PunkteFuerSieg, Math.Min(punkteRegel.PunkteFuerUnentschieden, punkteRegel.PunkteFuerNiederlage));
        var schranken = tabelle.ToDictionary(
            e => e.Mannschaft.Id,
            e => (Min: e.Punkte + verbleibend[e.Mannschaft.Id] * minProSpiel,
                  Max: e.Punkte + verbleibend[e.Mannschaft.Id] * maxProSpiel));

        foreach (var eintrag in tabelle)
        {
            var (eigenMin, eigenMax) = schranken[eintrag.Mannschaft.Id];
            var andere = schranken.Where(kv => kv.Key != eintrag.Mannschaft.Id).Select(kv => kv.Value).ToList();
            eintrag.BesterRang        = 1 + andere.Count(s => s.Min > eigenMax);
            eintrag.SchlechtesterRang = 1 + andere.Count(s => s.Max >= eigenMin);
        }
    }

    /// <summary>Anzahl noch ausstehender Spiele je Team, oder <c>null</c> wenn diese nicht zuverlässig bestimmbar
    /// ist. Schweizer System: künftige Spieltage sind noch nicht generiert, daher kommen zu den angesetzten offenen
    /// Spielen (<see cref="RundenKonfiguration.AnzahlSpieltage"/> − höchster generierter Spieltag) weitere hinzu
    /// (je Spieltag höchstens ein Spiel pro Team; ein Freilos wird dabei konservativ wie ein Spiel gezählt).</summary>
    private static Dictionary<int, int>? VerbleibendeSpieleProTeam(
        TurnierRunde? runde, IReadOnlyList<Spiel> alleSpiele, IReadOnlyList<TabellenEintrag> tabelle)
    {
        if (alleSpiele.Any(s => s.HeimMannschaftId is null || s.AuswaertsMannschaftId is null)) return null;

        var offen = alleSpiele.Where(s => s.Status != SpielStatus.Abgeschlossen || s.Ergebnis is null).ToList();
        var ergebnis = tabelle.ToDictionary(
            e => e.Mannschaft.Id,
            e => offen.Count(s => s.HeimMannschaftId == e.Mannschaft.Id || s.AuswaertsMannschaftId == e.Mannschaft.Id));

        if (runde?.Rundenart == Rundenart.SchweizerSystem)
        {
            if (RundenKonfiguration.AusJson(runde.KonfigurationJson).AnzahlSpieltage is not { } anzahlSpieltage) return null;
            int generiert = alleSpiele.Select(s => s.Spieltag ?? 0).DefaultIfEmpty().Max();
            int ausstehend = Math.Max(0, anzahlSpieltage - generiert);
            foreach (var id in ergebnis.Keys) ergebnis[id] += ausstehend;
        }
        return ergebnis;
    }

    // ── Qualifikationsstatus ──────────────────────────────────────────────

    public static void QualifikationsstatusSetzen(
        GruppenTabelle gruppe,
        IReadOnlyList<Spiel> gruppenSpiele,
        int qualifiziertePlaetze,
        PunkteRegel punkteRegel,
        List<RankingRegel> rankingRegeln)
    {
        var abgeschlossen = gruppenSpiele
            .Where(s => s.Status == SpielStatus.Abgeschlossen && s.Ergebnis is not null)
            .ToList();
        var offen = gruppenSpiele
            .Where(s => s.Status != SpielStatus.Abgeschlossen
                     && s.HeimMannschaftId.HasValue && s.AuswaertsMannschaftId.HasValue)
            .ToList();

        if (!offen.Any())
        {
            foreach (var e in gruppe.Tabelle)
            {
                e.Qualifikationsstatus = e.Rang <= qualifiziertePlaetze
                    ? Qualifikationsstatus.SicherQualifiziert
                    : Qualifikationsstatus.SicherAusgeschieden;
                e.SicherPositioniert = true; // keine offenen Spiele mehr - jeder Rang ist final
                e.BesterRang = e.SchlechtesterRang = e.Rang;
            }
            return;
        }

        foreach (var eintrag in gruppe.Tabelle)
        {
            int teamId = eintrag.Mannschaft.Id;

            int bestRang = ExtremRangErmitteln(
                abgeschlossen, offen, teamId, punkteRegel, rankingRegeln, teamGewinntAlles: true, bestenfalls: true);
            int worstRang = ExtremRangErmitteln(
                abgeschlossen, offen, teamId, punkteRegel, rankingRegeln, teamGewinntAlles: false, bestenfalls: false);

            eintrag.Qualifikationsstatus = bestRang > qualifiziertePlaetze
                ? Qualifikationsstatus.SicherAusgeschieden
                : worstRang <= qualifiziertePlaetze
                    ? Qualifikationsstatus.SicherQualifiziert
                    : Qualifikationsstatus.Offen;
            eintrag.SicherPositioniert = bestRang == worstRang;
            eintrag.BesterRang = bestRang;
            eintrag.SchlechtesterRang = worstRang;
        }
    }

    /// <summary>Ermittelt den best- bzw. schlechtestmöglichen Rang von <paramref name="teamId"/>: die eigenen offenen
    /// Spiele sind gemäß <paramref name="teamGewinntAlles"/> fixiert und werden mit einem extremen statt einem
    /// knappen Ergebnis simuliert - für das eigene Spiel gibt es nur zwei Beteiligte (das Team und den Gegner),
    /// ein extremeres Ergebnis wirkt daher immer eindeutig in dieselbe Richtung (nie schwächer).
    ///
    /// Für alle fremden offenen Spiele (die <paramref name="teamId"/> nicht betreffen) wird per Brute-Force jede
    /// Kombination aus Ausgang (Heim-/Auswärtssieg/Unentschieden) UND Tordifferenz (knapp/extrem) durchgerechnet -
    /// macht pro fremdem Spiel 6 statt 2 Varianten, insgesamt 6^n statt 2^n Kombinationen. Notwendig, weil ein
    /// fremdes Spiel einen "Umweg" über einen dritten Verein nimmt: Team X gewinnt gegen Team Y - ein knapper Sieg
    /// schadet Y kaum, ein extremer Sieg zertrümmert Ys Tordifferenz. Ob das für <paramref name="teamId"/> gut oder
    /// schlecht ist, hängt davon ab, ob teamId gerade mit X oder mit Y um einen Platz konkurriert - das kann bei
    /// jedem Aufruf anders liegen, ein pauschal "immer knapp" oder "immer extrem" für fremde Spiele ist daher in
    /// keine Richtung sicher korrekt (beide wurden ausprobiert und haben je einen der beiden unten stehenden Tests
    /// zum Scheitern gebracht). Unentschieden ist ein eigenständiger, nicht auf einen "Sieger" reduzierbarer
    /// Ausgang und fehlte ursprünglich komplett in der Brute-Force (nur 4 Sieg-Varianten) - dadurch konnte ein Team
    /// fälschlich als SicherPositioniert gelten, obwohl erst ein Unentschieden im fremden Spiel diesen Rang absichert
    /// oder gefährdet hätte (Fund aus manuellem #206-Test: Rang 2 einer Gruppe wurde vorschnell aufgelöst, obwohl
    /// nur ein Sieg in beide Richtungen, nicht aber ein Unentschieden im offenen Fremdspiel geprüft wurde).
    /// (AD: Issue #201/#202/#205/#206, Funde aus manuellem Test).</summary>
    private static int ExtremRangErmitteln(
        List<Spiel> abgeschlossen, List<Spiel> offen, int teamId,
        PunkteRegel punkteRegel, List<RankingRegel> rankingRegeln,
        bool teamGewinntAlles, bool bestenfalls)
    {
        var eigene = offen.Where(s => s.HeimMannschaftId == teamId || s.AuswaertsMannschaftId == teamId).ToList();
        var fremde = offen.Where(s => s.HeimMannschaftId != teamId && s.AuswaertsMannschaftId != teamId).ToList();

        var eigeneErgebnisse = eigene
            .Select(s => SpielMitSimuliertemErgebnis(
                s, heimSiegt: s.HeimMannschaftId == teamId ? teamGewinntAlles : !teamGewinntAlles, ExtremesTorergebnis))
            .ToList();

        int extremRang = bestenfalls ? int.MaxValue : int.MinValue;
        // long: bei einer Fünfergruppe mit Rückspiel (12 fremde offene Spiele) passt 6^12 nicht mehr in int.
        long kombinationen = (long)Math.Pow(_fremdSpielVariantenAnzahl, fremde.Count);

        for (long kombi = 0; kombi < kombinationen; kombi++)
        {
            var fremdeErgebnisse = new List<Spiel>(fremde.Count);
            long rest = kombi;
            foreach (var s in fremde)
            {
                int variante = (int)(rest % _fremdSpielVariantenAnzahl);
                rest /= _fremdSpielVariantenAnzahl;
                fremdeErgebnisse.Add(variante switch
                {
                    0 => SpielMitSimuliertemErgebnis(s, heimSiegt: true,  KnappesTorergebnis),
                    1 => SpielMitSimuliertemErgebnis(s, heimSiegt: true,  ExtremesTorergebnis),
                    2 => SpielMitSimuliertemErgebnis(s, heimSiegt: false, KnappesTorergebnis),
                    3 => SpielMitSimuliertemErgebnis(s, heimSiegt: false, ExtremesTorergebnis),
                    4 => SpielMitUnentschieden(s, UnentschiedenKnapp),
                    _ => SpielMitUnentschieden(s, UnentschiedenExtrem)
                });
            }

            var tabelle = TabellenRechner.TabelleBerechnen(
                [..abgeschlossen, ..eigeneErgebnisse, ..fremdeErgebnisse], punkteRegel, rankingRegeln);
            int rang = tabelle.FirstOrDefault(e => e.Mannschaft.Id == teamId)?.Rang ?? int.MaxValue;

            extremRang = bestenfalls ? Math.Min(extremRang, rang) : Math.Max(extremRang, rang);

            // #427: Besser als Platz 1 und schlechter als der letzte Platz geht nicht — ist die Grenze erreicht,
            // ändert keine weitere Kombination mehr etwas. Solange viele Spiele offen sind, ist das fast sofort
            // der Fall; ohne den Abbruch rechnete eine Vierergruppe mit Rückspiel vor dem ersten Ergebnis
            // 6^6 Tabellen je Team und Richtung (gemessen 3,4 s je Gruppe, in der App 26 s für vier Gruppen).
            if (extremRang == (bestenfalls ? 1 : tabelle.Count)) break;
        }

        return extremRang;
    }

    // 6 Varianten pro fremdem Spiel: Heim-/Auswärtssieg × knapp/extrem, plus Unentschieden × knapp/extrem.
    private const int _fremdSpielVariantenAnzahl = 6;

    private const int KnappesTorergebnis    = 1;
    private const int ExtremesTorergebnis   = 1000;
    private const int UnentschiedenKnapp    = 0;
    private const int UnentschiedenExtrem   = 500;

    private static Spiel SpielMitSimuliertemErgebnis(Spiel original, bool heimSiegt, int torergebnis) => new()
    {
        Spielnummer           = original.Spielnummer,
        HeimMannschaftId      = original.HeimMannschaftId,
        HeimMannschaft        = original.HeimMannschaft,
        AuswaertsMannschaftId = original.AuswaertsMannschaftId,
        AuswaertsMannschaft   = original.AuswaertsMannschaft,
        Status                = SpielStatus.Abgeschlossen,
        Ergebnis = new SpielErgebnis
        {
            HeimToreRegulaer      = heimSiegt ? torergebnis : 0,
            AuswaertsToreRegulaer = heimSiegt ? 0 : torergebnis,
            EntscheidungArt       = EntscheidungArt.RegulaereSpielzeit
        }
    };

    private static Spiel SpielMitUnentschieden(Spiel original, int tore) => new()
    {
        Spielnummer           = original.Spielnummer,
        HeimMannschaftId      = original.HeimMannschaftId,
        HeimMannschaft        = original.HeimMannschaft,
        AuswaertsMannschaftId = original.AuswaertsMannschaftId,
        AuswaertsMannschaft   = original.AuswaertsMannschaft,
        Status                = SpielStatus.Abgeschlossen,
        Ergebnis = new SpielErgebnis
        {
            HeimToreRegulaer      = tore,
            AuswaertsToreRegulaer = tore,
            EntscheidungArt       = EntscheidungArt.RegulaereSpielzeit
        }
    };
}
