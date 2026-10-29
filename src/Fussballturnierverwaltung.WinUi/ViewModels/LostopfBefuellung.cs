using Fussballturnierverwaltung.Application.Auslosungen;
using Fussballturnierverwaltung.Application.Ranglisten;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>„Töpfe automatisch befüllen“ (#119): legt die noch verfügbaren Mannschaften nach einer Rangliste in die
/// Lostöpfe einer Auslosung. Aus <see cref="LostopfArbeitsbereichViewModel"/> herausgezogen (#573); läuft unter dem
/// DbContext-Schutz des Aufrufers und lädt das Detail nicht selbst neu.</summary>
public class LostopfBefuellung(
    AuslosungService auslosungService,
    RanglisteService ranglisteService,
    TurnierInstanzService instanzService)
{
    /// <summary>
    /// Verteilt die aktuell noch unzugeordneten Mannschaften (<paramref name="verfuegbar"/>, noch offene Platzhalter
    /// ausgenommen) anhand der Rangliste auf die Lostöpfe: die bestplatzierten Teams zuerst in Topf 1, die nächsten
    /// in Topf 2 usw. Bereits manuell zugeordnete Teams bleiben unangetastet — es wird nur ergänzt, was noch in
    /// "Verfügbar" steht. Teams ohne Ranglisten-Eintrag werden ans Ende einsortiert (landen tendenziell im letzten
    /// Topf). Den Titelverteidiger des Turniers setzt sie in Topf 1, wenn die Runde das vorgibt
    /// (UEFA-Champions-League-Ligaphase).
    /// </summary>
    /// <returns>Eine Fehlermeldung für den Reiter, sonst <c>null</c>.</returns>
    public async Task<string?> NachRanglisteBefuellenAsync(Auslosung detail, int ranglisteId, IReadOnlyList<TeilnehmerAnzeige> verfuegbar)
    {
        if (detail.Lostoepfe.Count == 0)
            return "Diese Auslosung hat keine Lostöpfe.";

        var rangliste = await ranglisteService.FindenNachIdAsync(ranglisteId);
        if (rangliste is null) return null;

        var rangProMannschaft = rangliste.Eintraege.ToDictionary(e => e.MannschaftId, e => e.Rang);
        var vorabInTopf1 = await VorabInTopf1Async(detail);
        var teamsZuVerteilen = TeamsZuVerteilen(detail, verfuegbar, rangProMannschaft, vorabInTopf1);
        if (teamsZuVerteilen.Count == 0)
            return "Keine verfügbaren Mannschaften zum Zuordnen.";

        // Gruppenköpfe, die das Programm schon in Topf 1 gelegt hat (#487), zählen bei der Aufteilung mit — sonst
        // bekäme Topf 1 ein Team zu viel. Sie selbst werden nicht noch einmal eingelegt.
        var toepfe = detail.Lostoepfe.OrderBy(l => l.Reihenfolge).ToList();
        var schonInTopf1 = toepfe[0].Eintraege
            .Where(e => e.MannschaftId is { } id && vorabInTopf1.Contains(id))
            .Select(e => e.MannschaftId!.Value);
        var teilnehmerJeMannschaft = teamsZuVerteilen.ToDictionary(t => t.SpielendeMannschaftId!.Value);
        var gruppenplatzProMannschaft = teamsZuVerteilen
            .Where(t => Gruppenplatz(t).HasValue)
            .ToDictionary(t => t.SpielendeMannschaftId!.Value, t => Gruppenplatz(t)!.Value);
        var verteilung = LostopfVorbelegung.Verteilen(
            [.. schonInTopf1, .. teilnehmerJeMannschaft.Keys], rangProMannschaft, toepfe.Count, vorabInTopf1,
            gruppenplatzProMannschaft);
        for (int topfIndex = 0; topfIndex < toepfe.Count; topfIndex++)
        {
            foreach (var mannschaftId in verteilung[topfIndex])
                if (teilnehmerJeMannschaft.TryGetValue(mannschaftId, out var teilnehmer))
                    await auslosungService.InTopfLegenAsync(detail.Id, toepfe[topfIndex].Id, teilnehmer);
        }
        return null;
    }

    // Titelverteidiger (#349) und feste Gruppenköpfe (#487) kommen vorab in Topf 1.
    private async Task<IReadOnlyList<int>> VorabInTopf1Async(Auslosung detail)
    {
        if (detail.TurnierRunde is not { } runde) return [];
        var instanz = await instanzService.FindenNachIdMitRundenAsync(detail.TurnierInstanzId);
        return instanz is null
            ? []
            : Gruppenkoepfe.VorabInTopf1(
                runde, [.. instanz.Gastgeber.OrderBy(g => g.Id).Select(g => g.MannschaftId)], instanz.TitelverteidigerId);
    }

    /// <summary>Noch verfügbare Mannschaften in Setzreihenfolge (vorab für Topf 1, dann Gruppenplatz, dann Rang, dann Name) — höchstens
    /// so viele, wie der Runde noch fehlen. Ein Platzhalter zählt mit der Mannschaft, die ihn sicher besetzt (#483),
    /// und kommt als Platzhalter in den Topf.</summary>
    private static List<TeilnehmerAnzeige> TeamsZuVerteilen(
        Auslosung detail, IReadOnlyList<TeilnehmerAnzeige> verfuegbar, Dictionary<int, int> rangProMannschaft, IReadOnlyList<int> vorabInTopf1)
    {
        var teams = verfuegbar
            .Where(t => t.SpielendeMannschaftId.HasValue)
            .OrderBy(t => vorabInTopf1.Contains(t.SpielendeMannschaftId!.Value) ? 0 : 1)
            .ThenBy(t => Gruppenplatz(t) ?? int.MaxValue)
            .ThenBy(t => rangProMannschaft.TryGetValue(t.SpielendeMannschaftId!.Value, out var rang) ? rang : int.MaxValue)
            .ThenBy(t => t.AnzeigeName);

        int teamsBenoetigt = detail.TurnierRunde?.EingangsMannschaftsanzahl ?? 0;
        int teamsZugewiesen = detail.Lostoepfe.SelectMany(l => l.Eintraege).Count();
        return teamsBenoetigt > 0
            ? teams.Take(Math.Max(0, teamsBenoetigt - teamsZugewiesen)).ToList()
            : teams.ToList();
    }

    /// <summary>Platz in der Gruppe, aus der ein Platzhalter stammt („2. Gruppe B“ → 2), sonst null (#565).</summary>
    private static int? Gruppenplatz(TeilnehmerAnzeige teilnehmer) =>
        teilnehmer.IstPlatzhalter ? GruppenHerkunft.AusPlatzhalter(teilnehmer.Name)?.Platz : null;
}

/// <summary>Einen Teilnehmer der Verfügbar-Liste in einen Lostopf legen — als Mannschaft oder als Platzhalter.</summary>
public static class LostopfEinlage
{
    public static Task InTopfLegenAsync(this AuslosungService auslosungService, int auslosungId, int topfId, TeilnehmerAnzeige teilnehmer) =>
        teilnehmer.MannschaftId.HasValue
            ? auslosungService.MannschaftZuLostopfHinzufuegenAsync(auslosungId, topfId, teilnehmer.MannschaftId.Value)
            : auslosungService.PlatzhalterZuLostopfHinzufuegenAsync(auslosungId, topfId, teilnehmer.Name);
}
