using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Tabelle;

/// <summary>Sagt voraus, was aus dem Tabellenstand noch werden kann: bester und schlechtester Rang je Team und
/// daraus, ob Rang oder Weiterkommen schon feststehen. Ohne Datenbankzugriff, die Tabellen selbst rechnet
/// <see cref="TabellenRechner"/> (#439).</summary>
public static class RangPrognose
{
    // ── Liga: Schranken über die Punkte ───────────────────────────────────

    /// <summary>Setzt <see cref="TabellenEintrag.BesterRang"/>/<see cref="TabellenEintrag.SchlechtesterRang"/>
    /// einer Liga-Tabelle (Issue #343, Liga-Gegenstück zu #202). Anders als in der Gruppe
    /// (<see cref="GruppenRaengeSetzen"/>) wird nicht jede Ergebniskombination der offenen Spiele durchgerechnet
    /// (6^n ist bei z. B. 18 offenen Spielen je Ligaphasen-Spieltag nicht berechenbar), sondern konservativ über
    /// Punkte-Schranken: bester Rang = 1 + Anzahl Teams, die selbst im schlechtesten Fall mehr Punkte haben als
    /// das Team im besten Fall; schlechtester Rang = 1 + Anzahl Teams, die im besten Fall mindestens so viele
    /// Punkte erreichen können wie das Team im schlechtesten Fall (Punktgleichheit zählt als "kann vorbeiziehen",
    /// da Tiebreaker bei offenen Spielen nicht vorhersagbar sind). Kann einen tatsächlich schon feststehenden
    /// Rang übersehen, meldet aber nie fälschlich einen zu engen Bereich.
    /// Bleibt aus, solange der Rest der Runde unbekannt ist (noch unaufgelöste Platzhalter-Spiele, Schweizer
    /// System ohne Spieltag-Anzahl).</summary>
    public static void LigaRaengeSetzen(
        IReadOnlyList<TabellenEintrag> tabelle, TurnierRunde runde, IReadOnlyList<Spiel> alleSpiele, PunkteRegel punkteRegel)
    {
        var verbleibend = VerbleibendeSpieleProTeam(runde, alleSpiele, tabelle);
        if (verbleibend is null) return;

        if (verbleibend.Values.All(n => n == 0))
        {
            // alles gespielt - jeder Rang ist final
            foreach (var e in tabelle) e.BesterRang = e.SchlechtesterRang = e.Rang;
            return;
        }

        PunkteSchrankenAnwenden(tabelle, verbleibend, punkteRegel);
    }

    /// <summary>Markiert die Einträge, deren Rang nach <see cref="LigaRaengeSetzen"/> feststeht: bester und
    /// schlechtester Rang sind gleich dem aktuellen.</summary>
    public static void SicherPositionierteMarkieren(IReadOnlyList<TabellenEintrag> tabelle)
    {
        foreach (var eintrag in tabelle)
            eintrag.SicherPositioniert = eintrag.BesterRang == eintrag.Rang && eintrag.SchlechtesterRang == eintrag.Rang;
    }

    private static void PunkteSchrankenAnwenden(
        IReadOnlyList<TabellenEintrag> tabelle, Dictionary<int, int> verbleibend, PunkteRegel punkteRegel)
    {
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
        TurnierRunde runde, IReadOnlyList<Spiel> alleSpiele, IReadOnlyList<TabellenEintrag> tabelle)
    {
        if (alleSpiele.Any(s => s.HeimMannschaftId is null || s.AuswaertsMannschaftId is null)) return null;

        var offen = alleSpiele.Where(s => !TabellenRechner.HatErgebnis(s)).ToList();
        var ergebnis = tabelle.ToDictionary(
            e => e.Mannschaft.Id,
            e => offen.Count(s => s.HeimMannschaftId == e.Mannschaft.Id || s.AuswaertsMannschaftId == e.Mannschaft.Id));

        if (runde.Rundenart == Rundenart.SchweizerSystem)
        {
            if (RundenKonfiguration.AusJson(runde.KonfigurationJson).AnzahlSpieltage is not { } anzahlSpieltage) return null;
            int generiert = alleSpiele.Select(s => s.Spieltag ?? 0).DefaultIfEmpty().Max();
            int ausstehend = Math.Max(0, anzahlSpieltage - generiert);
            foreach (var id in ergebnis.Keys) ergebnis[id] += ausstehend;
        }
        return ergebnis;
    }

    // ── Gruppe: alle Ergebniskombinationen der offenen Spiele ─────────────

    /// <summary>Setzt je Team der Gruppe besten und schlechtesten Rang und daraus
    /// <see cref="TabellenEintrag.SicherPositioniert"/> (Issue #202 - ob der exakte Rang durch kein Ergebnis der
    /// verbleibenden Spiele mehr verändert werden kann). Ob ein Team weiterkommt, leiten die Zonen der
    /// Übergangsregeln aus diesen Rängen ab (<see cref="TabellenZonen"/>). Ohne offene Spiele ist jeder Rang final.
    /// Bleibt aus, solange ein Platz der Gruppe noch nicht aufgelöst ist.</summary>
    public static void GruppenRaengeSetzen(GruppenTabelle gruppe, IReadOnlyList<Spiel> gruppenSpiele, TabellenRegeln regeln)
    {
        // Issue #397: Solange ein Platz der Gruppe noch nicht aufgelöst ist, fehlen dessen Spiele in der
        // Status-Berechnung (Gegner unbekannt) — kein Rang darf dann als sicher gelten.
        if (gruppe.OffenePlaetze.Count > 0) return;

        var stand = new GruppenStand(
            TabellenRechner.MitErgebnis(gruppenSpiele),
            [.. gruppenSpiele.Where(s => s.Status != SpielStatus.Abgeschlossen
                                      && s.HeimMannschaftId.HasValue && s.AuswaertsMannschaftId.HasValue)],
            regeln);

        foreach (var eintrag in gruppe.Tabelle)
        {
            int teamId = eintrag.Mannschaft.Id;
            var (bestRang, worstRang) = stand.Offen.Count == 0
                ? (eintrag.Rang, eintrag.Rang)
                : (ExtremRangErmitteln(stand, teamId, bestenfalls: true), ExtremRangErmitteln(stand, teamId, bestenfalls: false));

            eintrag.SicherPositioniert = bestRang == worstRang;
            eintrag.BesterRang = bestRang;
            eintrag.SchlechtesterRang = worstRang;
        }
    }

    /// <summary>Was über eine Gruppe feststeht (Spiele mit Ergebnis) und was noch aussteht (offene Spiele mit
    /// zwei bekannten Teams), samt den Regeln, nach denen daraus eine Tabelle wird.</summary>
    private sealed record GruppenStand(
        IReadOnlyList<Spiel> Abgeschlossen,
        IReadOnlyList<Spiel> Offen,
        TabellenRegeln Regeln);

    /// <summary>Ermittelt den best- bzw. schlechtestmöglichen Rang von <paramref name="teamId"/>: die eigenen offenen
    /// Spiele sind fixiert - bestenfalls gewinnt das Team alle, schlechtestenfalls verliert es alle - und werden mit
    /// einem extremen statt einem knappen Ergebnis simuliert - für das eigene Spiel gibt es nur zwei Beteiligte (das
    /// Team und den Gegner), ein extremeres Ergebnis wirkt daher immer eindeutig in dieselbe Richtung (nie schwächer).
    ///
    /// Für alle fremden offenen Spiele (die <paramref name="teamId"/> nicht betreffen) wird per Brute-Force jede
    /// Kombination aus Ausgang (Heim-/Auswärtssieg/Unentschieden) UND Tordifferenz (knapp/extrem) durchgerechnet -
    /// macht pro fremdem Spiel 6 statt 2 Varianten, insgesamt 6^n statt 2^n Kombinationen. Notwendig, weil ein
    /// fremdes Spiel einen "Umweg" über einen dritten Verein nimmt: Team X gewinnt gegen Team Y - ein knapper Sieg
    /// schadet Y kaum, ein extremer Sieg zertrümmert Ys Tordifferenz. Ob das für <paramref name="teamId"/> gut oder
    /// schlecht ist, hängt davon ab, ob teamId gerade mit X oder mit Y um einen Platz konkurriert - das kann bei
    /// jedem Aufruf anders liegen, ein pauschal "immer knapp" oder "immer extrem" für fremde Spiele ist daher in
    /// keine Richtung sicher korrekt (beide wurden ausprobiert und haben je einen der beiden Tests dazu zum
    /// Scheitern gebracht). Unentschieden ist ein eigenständiger, nicht auf einen "Sieger" reduzierbarer
    /// Ausgang und fehlte ursprünglich komplett in der Brute-Force (nur 4 Sieg-Varianten) - dadurch konnte ein Team
    /// fälschlich als SicherPositioniert gelten, obwohl erst ein Unentschieden im fremden Spiel diesen Rang absichert
    /// oder gefährdet hätte (Fund aus manuellem #206-Test: Rang 2 einer Gruppe wurde vorschnell aufgelöst, obwohl
    /// nur ein Sieg in beide Richtungen, nicht aber ein Unentschieden im offenen Fremdspiel geprüft wurde).
    /// (AD: Issue #201/#202/#205/#206, Funde aus manuellem Test).</summary>
    private static int ExtremRangErmitteln(GruppenStand stand, int teamId, bool bestenfalls)
    {
        var eigeneErgebnisse = stand.Offen
            .Where(s => s.HeimMannschaftId == teamId || s.AuswaertsMannschaftId == teamId)
            .Select(s => EigenesSpielEntschieden(s, teamId, teamGewinnt: bestenfalls))
            .ToList();
        var fremde = stand.Offen.Where(s => s.HeimMannschaftId != teamId && s.AuswaertsMannschaftId != teamId).ToList();

        int extremRang = bestenfalls ? int.MaxValue : int.MinValue;
        // long: bei einer Fünfergruppe mit Rückspiel (12 fremde offene Spiele) passt 6^12 nicht mehr in int.
        long kombinationen = (long)Math.Pow(_fremdSpielVarianten.Length, fremde.Count);

        for (long kombi = 0; kombi < kombinationen; kombi++)
        {
            var tabelle = TabellenRechner.TabelleBerechnen(
                [..stand.Abgeschlossen, ..eigeneErgebnisse, ..FremdeErgebnisse(fremde, kombi)],
                stand.Regeln);
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

    private static Spiel EigenesSpielEntschieden(Spiel spiel, int teamId, bool teamGewinnt)
    {
        bool heimSiegt = spiel.HeimMannschaftId == teamId ? teamGewinnt : !teamGewinnt;
        return heimSiegt
            ? SpielMitErgebnis(spiel, EXTREMES_TORERGEBNIS, 0)
            : SpielMitErgebnis(spiel, 0, EXTREMES_TORERGEBNIS);
    }

    /// <summary>Die Fremdspiele mit den Ergebnissen der Kombination Nummer <paramref name="kombi"/>: deren
    /// Stellen zur Basis der Varianten-Anzahl wählen je Spiel eine Variante.</summary>
    private static List<Spiel> FremdeErgebnisse(List<Spiel> fremde, long kombi)
    {
        var ergebnisse = new List<Spiel>(fremde.Count);
        long rest = kombi;
        foreach (var s in fremde)
        {
            var (heimTore, auswaertsTore) = _fremdSpielVarianten[rest % _fremdSpielVarianten.Length];
            rest /= _fremdSpielVarianten.Length;
            ergebnisse.Add(SpielMitErgebnis(s, heimTore, auswaertsTore));
        }
        return ergebnisse;
    }

    private const int KNAPPES_TORERGEBNIS  = 1;
    private const int EXTREMES_TORERGEBNIS = 1000;
    private const int UNENTSCHIEDEN_EXTREM = 500;

    // 6 Varianten pro fremdem Spiel: Heim-/Auswärtssieg × knapp/extrem, plus Unentschieden × knapp/extrem.
    private static readonly (int HeimTore, int AuswaertsTore)[] _fremdSpielVarianten =
    [
        (KNAPPES_TORERGEBNIS, 0),
        (EXTREMES_TORERGEBNIS, 0),
        (0, KNAPPES_TORERGEBNIS),
        (0, EXTREMES_TORERGEBNIS),
        (0, 0),
        (UNENTSCHIEDEN_EXTREM, UNENTSCHIEDEN_EXTREM)
    ];

    private static Spiel SpielMitErgebnis(Spiel original, int heimTore, int auswaertsTore) => new()
    {
        Spielnummer           = original.Spielnummer,
        HeimMannschaftId      = original.HeimMannschaftId,
        HeimMannschaft        = original.HeimMannschaft,
        AuswaertsMannschaftId = original.AuswaertsMannschaftId,
        AuswaertsMannschaft   = original.AuswaertsMannschaft,
        Status                = SpielStatus.Abgeschlossen,
        Ergebnis = new SpielErgebnis
        {
            HeimToreRegulaer      = heimTore,
            AuswaertsToreRegulaer = auswaertsTore,
            EntscheidungArt       = EntscheidungArt.RegulaereSpielzeit
        }
    };
}
