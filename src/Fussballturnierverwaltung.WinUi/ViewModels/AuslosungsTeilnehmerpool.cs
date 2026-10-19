using Fussballturnierverwaltung.Application.Auslosungen;
using Fussballturnierverwaltung.Application.Spielplan;
using Fussballturnierverwaltung.Application.Stammdaten;
using Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>Die Teilnehmer einer Auslosung samt den Mannschaften, die hinter ihren Platzhaltern schon feststehen.</summary>
public sealed record AuslosungsTeilnehmer(IReadOnlyList<TeilnehmerAnzeige> Teilnehmer, Qualifikanten Qualifikanten);

/// <summary>Wie viele Qualifikanten einer Runde noch nicht feststehen (#483).</summary>
public sealed record QualifikantenStand(int Offen, int Gesamt)
{
    public static QualifikantenStand Fest { get; } = new(0, 0);
    public bool AlleFest => Offen == 0;
}

/// <summary>Woher die Teilnehmer einer Auslosung in der Turnierdurchführung kommen: Platzhalter aus den Vorrunden
/// oder, für eine Runde ohne Vorgänger, die Mannschaften nach dem Turnierfilter. Gegenstück zum
/// <see cref="VorbelegungsTeilnehmerpool"/>; eine Instanz je <see cref="AuslosungViewModel"/>.
/// Seit #483 löst er die Platzhalter auf: angezeigt wird die Mannschaft, die einen Platzhalter sicher besetzt, und
/// eine Folgerunde gilt erst als auslosbar, wenn alle ihre Qualifikanten feststehen.</summary>
public class AuslosungsTeilnehmerpool(
    MannschaftService mannschaftService,
    VerbandService verbandService,
    UebergangsregelService uebergangsregelService,
    QualifikantenAufloesung qualifikantenAufloesung)
{
    private IReadOnlyDictionary<int, string>? _verbandKurznamen;

    /// <summary>Turnierwechsel: die je Turnier gemerkten Verbandsnamen verwerfen.</summary>
    public void Zuruecksetzen() => _verbandKurznamen = null;

    /// <summary>Alle Teilnehmer, die für die Runde der Auslosung in Frage kommen — Platzhalter aus den
    /// eingehenden Übergangsregeln, sonst aus der Vorgängerrunde, in der ersten Runde die Mannschaften nach
    /// dem Turnierfilter. Bereits einem Lostopf zugeordnete sind noch enthalten. Aufgelöst werden dabei auch die
    /// Platzhalter in Lostöpfen und Ergebnis der Auslosung — sie können aus einem früheren Stand der Runde stammen.</summary>
    public async Task<AuslosungsTeilnehmer> ErmittelnAsync(
        Auslosung detail, TurnierInstanz? instanz, IReadOnlyList<TurnierRunde> instanzRunden)
    {
        var runde = instanzRunden.FirstOrDefault(r => r.Id == detail.TurnierRundeId);
        var platzhalter = runde is null ? null : await PlatzhalterAsync(runde, instanzRunden);

        var qualifikanten = await qualifikantenAufloesung.AufloesenAsync(
            detail.TurnierRundeId, PlatzhalterNamen(platzhalter, detail));
        if (platzhalter is null)
            return new(await MannschaftenNachTurnierfilterAsync(instanz), qualifikanten);

        var verbandKurznamen = qualifikanten.JePlatzhalter.Count > 0 ? await VerbandKurznamenAsync() : null;
        var teilnehmer = platzhalter
            .Select(t => qualifikanten.Fuer(t.Name) is { } m
                ? t.MitQualifikant(m, verbandKurznamen?.GetValueOrDefault(m.Land?.VerbandId ?? 0))
                : t)
            .ToList();
        return new(teilnehmer, qualifikanten);
    }

    /// <summary>Ob die Qualifikanten einer Runde feststehen. Eine Runde ohne Vorgänger hat keine Platzhalter und gilt
    /// immer als fest.</summary>
    public async Task<QualifikantenStand> QualifikantenStandAsync(TurnierRunde runde, IReadOnlyList<TurnierRunde> instanzRunden)
    {
        var platzhalter = await PlatzhalterAsync(runde, instanzRunden);
        if (platzhalter is null || platzhalter.Count == 0) return QualifikantenStand.Fest;

        var qualifikanten = await qualifikantenAufloesung.AufloesenAsync(runde.Id, platzhalter.Select(t => t.Name));
        return new(qualifikanten.AnzahlOffen, qualifikanten.AnzahlOffen + qualifikanten.JePlatzhalter.Count);
    }

    /// <summary><c>null</c> für eine Runde ohne Vorgänger — dort stehen die Mannschaften nach Turnierfilter zur Wahl.</summary>
    private async Task<IReadOnlyList<TeilnehmerAnzeige>?> PlatzhalterAsync(
        TurnierRunde runde, IReadOnlyList<TurnierRunde> instanzRunden)
    {
        var eingehendeRegeln = await uebergangsregelService.AlleNachZielRundeIdAsync(runde.Id);
        // Ohne Kombinationstabelle: in einer gelosten Runde stehen die besten Dritten als Rangfolge zur Wahl, ihre
        // festen Baumplätze gelten nur bei fest vorgegebener Paarung in der Vorbelegung (#444).
        return TeilnehmerPoolHilfsmittel.PlatzhalterAusVorrunden(instanzRunden, runde, eingehendeRegeln);
    }

    private static IEnumerable<string> PlatzhalterNamen(IReadOnlyList<TeilnehmerAnzeige>? platzhalter, Auslosung detail) =>
        (platzhalter ?? []).Select(t => t.Name)
            .Concat(detail.Lostoepfe.SelectMany(l => l.Eintraege).Select(e => e.PlatzhalterName).OfType<string>())
            .Concat(detail.Ergebnisse
                .SelectMany(e => new[] { e.HeimPlatzhalterName, e.AuswaertsPlatzhalterName })
                .OfType<string>());

    private async Task<IReadOnlyList<TeilnehmerAnzeige>> MannschaftenNachTurnierfilterAsync(TurnierInstanz? instanz)
    {
        var teams = await mannschaftService.AlleNachTurnierfilterAsync(
            instanz?.VerbandId,
            instanz?.LandId,
            instanz?.MannschaftsartFilter ?? MannschaftsartFilter.Alle);
        var verbandKurznamen = await VerbandKurznamenAsync();
        return teams
            .Select(TeilnehmerAnzeige.AusMannschaft)
            .Select(t => t with { VerbandName = verbandKurznamen.GetValueOrDefault(t.VerbandId ?? 0) })
            .ToList();
    }

    /// <summary>Kurznamen der Verbände für den Verbandsfilter der verfügbaren Teams (#421). Je Turnier einmal
    /// geladen: der Teilnehmer-Pool wird nach jeder Topf-Änderung neu ermittelt, und die Mannschaftsabfrage lädt
    /// den Verband bewusst nicht mit (sein Logo käme sonst in jeder Mannschaftszeile mit).</summary>
    private async Task<IReadOnlyDictionary<int, string>> VerbandKurznamenAsync() =>
        _verbandKurznamen ??= (await verbandService.AlleAbrufenAsync()).ToDictionary(v => v.Id, v => v.Kurzname);
}
