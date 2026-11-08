using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Tabelle;

/// <summary>Rechnet Tabellen aus Spielen, ohne Datenbankzugriff: sammelt die Teams, holt ihre Statistik aus
/// <see cref="TeamStatistik"/> und lässt sie von <see cref="TabellenSortierung"/> nach den Ranking-Regeln ordnen. Die Gruppen einer Gruppenphase bildet <see cref="GruppenRechner"/>, das Laden von Runde,
/// Regelwerk und Spielen übernimmt <see cref="TabellenService"/> (#439).</summary>
public static class TabellenRechner
{
    // ── Tabelle ───────────────────────────────────────────────────────────

    /// <summary>Tabelle aus den Spielen mit Ergebnis. Mit <paramref name="nurHeimspiele"/> zählt je Team nur die
    /// Heim- (<c>true</c>) bzw. nur die Auswärtsseite (<c>false</c>). <paramref name="teilnehmer"/> nennt Teams,
    /// die auch ohne Spiel gegen einen bereits bekannten Gegner in der Tabelle stehen sollen.
    /// <paramref name="spielplan"/> sind alle angesetzten Spiele, wenn <paramref name="spiele"/> nur einen Teil davon
    /// wertet (Stand nach einem Spieltag): Der direkte Vergleich zählt erst, wenn alle Duelle der gleichstehenden Teams
    /// gespielt sind (#628), und ein Duell nach dem Spieltag steht zu diesem Stand noch aus.</summary>
    public static IReadOnlyList<TabellenEintrag> TabelleBerechnen(
        IReadOnlyList<Spiel> spiele, TabellenRegeln regeln,
        bool? nurHeimspiele = null, IReadOnlyList<Mannschaft>? teilnehmer = null, IReadOnlyList<Spiel>? spielplan = null) =>
        Berechnen(new Eingabe(spiele, spielplan ?? spiele, nurHeimspiele, teilnehmer, NurMannschaften: null), regeln);

    /// <summary>Rangfolge nur der <paramref name="mannschaftIds"/>, ihre Statistik aber aus allen
    /// <paramref name="spiele"/> – etwa die Gruppendritten einer Gruppenphase (#184). Sie werden nur untereinander
    /// verglichen: die Mini-Tabelle des direkten Vergleichs kennt keine Spiele gegen Teams außerhalb (#628), zwischen
    /// Teams verschiedener Gruppen entscheidet sie also nichts.</summary>
    public static IReadOnlyList<TabellenEintrag> RangfolgeBerechnen(
        IReadOnlyList<Spiel> spiele, IReadOnlySet<int> mannschaftIds, TabellenRegeln regeln,
        IReadOnlyList<Mannschaft>? teilnehmer = null) =>
        Berechnen(new Eingabe(spiele, spiele, NurHeimspiele: null, teilnehmer, mannschaftIds), regeln);

    /// <summary>Was in eine Tabelle eingeht: die gewerteten Spiele, der Spielplan, gegen den der direkte Vergleich
    /// prüft, ob alle Duelle gespielt sind, die gezählte Seite, Teams ohne Spiel und – für eine Rangfolge – die
    /// Auswahl der Teams.</summary>
    private sealed record Eingabe(
        IReadOnlyList<Spiel> Spiele, IReadOnlyList<Spiel> Spielplan, bool? NurHeimspiele,
        IReadOnlyList<Mannschaft>? Teilnehmer, IReadOnlySet<int>? NurMannschaften);

    private static IReadOnlyList<TabellenEintrag> Berechnen(Eingabe eingabe, TabellenRegeln regeln)
    {
        var teams = TeamsSammeln(eingabe.Spiele, eingabe.Teilnehmer);
        if (teams.Count == 0) return [];

        // Statistik für alle Teams: die Gegner-Summen brauchen auch die Teams, die nicht in der Rangfolge stehen.
        var abgeschlossene = MitErgebnis(eingabe.Spiele);
        var stats = teams.Keys.ToDictionary(
            id => id,
            id => TeamStatistik.Berechnen(id, abgeschlossene, regeln.PunkteRegel, eingabe.NurHeimspiele));
        TeamStatistik.GegnerSummenErgaenzen(stats, abgeschlossene);

        var eintraege = teams
            .Where(kv => eingabe.NurMannschaften?.Contains(kv.Key) ?? true)
            .Select(kv => EintragAus(kv.Value, stats[kv.Key]))
            .ToList();
        TabellenSortierung.SortierenUndRangZuweisen(
            eintraege, new TabellenSortierung.Wertung(stats, abgeschlossene, eingabe.Spielplan, regeln));
        return eintraege;
    }

    /// <summary>Tabelle einer <see cref="TabellenAnsicht"/>: Hin- und Rückrunde werten nur die Spiele ihrer
    /// Hälfte, Heim- und Auswärtstabelle je Team nur die eine Seite. <paramref name="teilnehmer"/> und
    /// <paramref name="spielplan"/> wie bei <see cref="TabelleBerechnen"/>; der Spielplan wird auf dieselbe Hälfte
    /// beschränkt.</summary>
    public static IReadOnlyList<TabellenEintrag> TabelleDerAnsichtBerechnen(
        IReadOnlyList<Spiel> spiele, TabellenAnsicht ansicht, TabellenRegeln regeln,
        IReadOnlyList<Mannschaft>? teilnehmer = null, IReadOnlyList<Spiel>? spielplan = null)
    {
        bool? nurHeimspiele = ansicht switch
        {
            TabellenAnsicht.Heimtabelle      => true,
            TabellenAnsicht.Auswaertstabelle => false,
            _                                => null
        };

        return TabelleBerechnen(
            HaelfteDerAnsicht(spiele, ansicht), regeln, nurHeimspiele, teilnehmer,
            spielplan is null ? null : HaelfteDerAnsicht(spielplan, ansicht));
    }

    private static IReadOnlyList<Spiel> HaelfteDerAnsicht(IReadOnlyList<Spiel> spiele, TabellenAnsicht ansicht) =>
        ansicht switch
        {
            TabellenAnsicht.Hinrunde   => spiele.Where(s => !s.IstRueckspiel).ToList(),
            TabellenAnsicht.Rueckrunde => spiele.Where(s => s.IstRueckspiel).ToList(),
            _                          => spiele
        };

    /// <summary>Die Spiele bis einschließlich <paramref name="bisSpieltag"/> - der Stand nach diesem Spieltag;
    /// ohne Angabe alle Spiele.</summary>
    public static IReadOnlyList<Spiel> BisSpieltag(IReadOnlyList<Spiel> spiele, int? bisSpieltag) =>
        bisSpieltag is null ? spiele : spiele.Where(s => s.Spieltag is { } sp && sp <= bisSpieltag).ToList();

    /// <summary>Die Spieltage, für die ein historischer Tabellenstand sinnvoll ist: 1 bis zum
    /// letzten Spieltag mit mindestens einem eingetragenen Ergebnis (auch bei nur teilweise
    /// simuliertem Spieltag). Leer, wenn noch kein Ergebnis vorliegt — verhindert, dass der
    /// Tabellenstand-Snapshot auf komplett unsimulierte, zukünftige Spieltage zeigen kann.</summary>
    public static IReadOnlyList<int> SpieltageMitErgebnis(IEnumerable<Spiel> spiele)
    {
        int letzterMitErgebnis = spiele
            .Where(s => HatErgebnis(s) && s.Spieltag.HasValue)
            .Select(s => s.Spieltag!.Value)
            .DefaultIfEmpty()
            .Max();

        return letzterMitErgebnis > 0
            ? Enumerable.Range(1, letzterMitErgebnis).ToList()
            : [];
    }

    /// <summary>Die Spiele, die in eine Tabelle eingehen: abgeschlossen und mit Ergebnis.</summary>
    public static IReadOnlyList<Spiel> MitErgebnis(IEnumerable<Spiel> spiele) => [.. spiele.Where(HatErgebnis)];

    public static bool HatErgebnis(Spiel spiel) => spiel.Status == SpielStatus.Abgeschlossen && spiel.Ergebnis is not null;

    private static Dictionary<int, Mannschaft> TeamsSammeln(IReadOnlyList<Spiel> spiele, IReadOnlyList<Mannschaft>? teilnehmer)
    {
        var teams = spiele
            .Where(s => s.HeimMannschaftId.HasValue && s.AuswaertsMannschaftId.HasValue)
            .SelectMany(s => new[]
            {
                (s.HeimMannschaftId!.Value,      s.HeimMannschaft!),
                (s.AuswaertsMannschaftId!.Value, s.AuswaertsMannschaft!)
            })
            .DistinctBy(t => t.Item1)
            .ToDictionary(t => t.Item1, t => t.Item2);

        // Issue #397: Teams ohne Spiel gegen einen bereits bekannten Gegner trotzdem mit 0 Punkten führen.
        foreach (var m in teilnehmer ?? [])
            teams.TryAdd(m.Id, m);

        return teams;
    }

    private static TabellenEintrag EintragAus(Mannschaft mannschaft, RohStats stats) => new()
    {
        Mannschaft    = mannschaft,
        Spiele        = stats.Spiele,
        Siege         = stats.Siege,
        Unentschieden = stats.Unentschieden,
        Niederlagen   = stats.Niederlagen,
        Tore          = stats.Tore,
        Gegentore     = stats.Gegentore,
        Punkte        = stats.Punkte,
        Buchholz      = stats.GegnerPunkte
    };
}
