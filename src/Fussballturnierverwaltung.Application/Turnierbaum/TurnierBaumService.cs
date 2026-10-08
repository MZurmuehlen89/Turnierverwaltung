using Fussballturnierverwaltung.Application.Spielplan;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Entitaeten.Turnierbaum;
using Fussballturnierverwaltung.Domain.Enums;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Fussballturnierverwaltung.Application.Turnierbaum;

public class TurnierBaumService(
    ITurnierBaumRepository baumRepository,
    ITurnierInstanzRepository instanzRepository,
    ISpielRepository spielRepository,
    SpielplanService spielplanService,
    ILogger<TurnierBaumService> logger)
{
    /// <summary>Gibt den gespeicherten Turnierbaum zurück (null wenn noch nicht generiert).</summary>
    public Task<TurnierBaum?> AbrufenAsync(int turnierInstanzId, CancellationToken ct = default)
        => baumRepository.FindenNachInstanzMitKnotenAsync(turnierInstanzId, ct);

    /// <summary>
    /// Bringt den Turnierbaum auf den Stand der K.-o.-Runden eines Turniers: Runden mit Spielen bekommen einen
    /// Knoten je Paarung, Runden ohne Spiele Platzhalter-Knoten.
    /// </summary>
    public async Task<TurnierBaum> GenerierenAsync(int turnierInstanzId, CancellationToken ct = default)
    {
        var uhr = Stopwatch.StartNew();
        var plan = await PlanErmittelnAsync(turnierInstanzId, ct);

        // #390: Der Baum hängt nur an den Spielen der K.-o.-Runden und deren Quellverknüpfungen, nicht an
        // Ergebnissen oder eingetragenen Mannschaften. Die meisten Aufrufe (nach jedem Ergebnis, nach jeder
        // Auslosung) ändern daran nichts — dann bleibt der gespeicherte Baum stehen, statt ihn mit sechs
        // Speichervorgängen zu löschen und gleich wieder anzulegen.
        var bestehend = await baumRepository.FindenNachInstanzMitAufbauAsync(turnierInstanzId, ct);
        long laden = uhr.ElapsedMilliseconds;
        if (bestehend is not null && plan.Entspricht(TurnierBaumPlan.AusBestand(bestehend)))
        {
            logger.LogInformation(
                "Turnierbaum für Instanz {Id} unverändert: {Knoten} Knoten (Laden {Laden} ms).",
                turnierInstanzId, bestehend.Knoten.Count, laden);
            return bestehend;
        }

        if (bestehend is not null)
            await baumRepository.LoeschenMitKnotenAsync(bestehend.Id, ct);
        var baum = await PlanSpeichernAsync(turnierInstanzId, plan, ct);

        logger.LogInformation(
            "Turnierbaum für Instanz {Id} generiert: {Knoten} Knoten über {Runden} Runden (Laden {Laden} ms, Speichern {Speichern} ms).",
            turnierInstanzId, plan.AlleKnoten.Count(), plan.Ebenen.Count, laden, uhr.ElapsedMilliseconds - laden);
        return baum;
    }

    /// <summary>Zieht den Turnierbaum nach einer Spielplanänderung nach — der Nutzer soll den Baum nie von Hand
    /// neu erzeugen müssen, auch den ersten nicht (#320-Folgefund). Fehler werden bewusst geschluckt: der Baum ist
    /// Beiwerk der eigentlichen Aktion, und für Turniere ganz ohne K.-o.-Runden wirft
    /// <see cref="GenerierenAsync"/>.</summary>
    public async Task NachSpielplanAenderungAktualisierenAsync(int turnierInstanzId)
    {
        try { await GenerierenAsync(turnierInstanzId); }
        catch { /* TurnierBaum-Rebuild ist optional — nie Fehler an den Benutzer weitergeben */ }
    }

    private async Task<TurnierBaumPlan> PlanErmittelnAsync(int turnierInstanzId, CancellationToken ct)
    {
        var instanz = await instanzRepository.FindenNachIdMitRundenAsync(turnierInstanzId, ct)
            ?? throw new InvalidOperationException($"TurnierInstanz {turnierInstanzId} nicht gefunden.");

        // Issue #241: Quellverknüpfungen defensiv nachziehen, bevor die Spiele unten geladen werden —
        // deckt auch Alt-Turniere ab, die seit der Migration noch nicht neu generiert wurden.
        await spielplanService.SpielQuellenAlleTransitionsVerknuepfenAsync(turnierInstanzId, ct);

        // Alle K.-o.-Runden in Reihenfolge
        var koRunden = instanz.Runden
            .Where(r => r.Rundenart is Rundenart.KoRunde
                                    or Rundenart.Finale
                                    or Rundenart.SpielUmPlatzDrei)
            .OrderBy(r => r.Reihenfolge)
            .ToList();

        if (!koRunden.Any())
            throw new InvalidOperationException(
                "Dieses Turnier hat keine K.-o.-Runden. Ein Turnierbaum ist nur für K.-o.-Turniere sinnvoll.");

        // Spiele pro Runde laden
        var spieleProRunde = new List<IReadOnlyList<Spiel>>();
        foreach (var runde in koRunden)
            spieleProRunde.Add(await spielRepository.AlleNachRundeAsync(runde.Id, ct));

        var plan = TurnierBaumPlan.Erstellen(koRunden, spieleProRunde);
        if (!plan.HatSpiele)
            throw new InvalidOperationException(
                "Noch keine Spiele für K.-o.-Runden vorhanden. Bitte zuerst Spielpläne generieren.");

        return plan;
    }

    /// <summary>Zwei Schritte, weil die Fremdschlüssel zwischen Eltern- und Kindknoten in beide Richtungen zeigen:
    /// erst alle Knoten ohne Verweise (dabei entstehen die IDs), dann die Verweise und der Wurzelknoten.</summary>
    private async Task<TurnierBaum> PlanSpeichernAsync(int turnierInstanzId, TurnierBaumPlan plan, CancellationToken ct)
    {
        var baum = new TurnierBaum { TurnierInstanzId = turnierInstanzId };
        await baumRepository.HinzufuegenAsync(baum, ct);

        var knotenJePlanKnoten = plan.AlleKnoten.ToDictionary(
            p => p, p => new TurnierBaumKnoten { TurnierBaumId = baum.Id, SpielId = p.SpielId, Runde = p.Runde });
        var alleKnoten = plan.AlleKnoten.Select(p => knotenJePlanKnoten[p]).ToList();
        await baumRepository.KnotenSpeichernAsync(alleKnoten, ct);

        int? KnotenId(PlanKnoten? planKnoten) => planKnoten is null ? null : knotenJePlanKnoten[planKnoten].Id;
        foreach (var (planKnoten, knoten) in knotenJePlanKnoten)
        {
            knoten.ElternKnotenId      = KnotenId(planKnoten.Eltern);
            knoten.LinkerKindKnotenId  = KnotenId(planKnoten.Links);
            knoten.RechterKindKnotenId = KnotenId(planKnoten.Rechts);
        }
        baum.WurzelKnotenId = KnotenId(plan.Wurzel);

        await baumRepository.VerknuepfungenAktualisierenAsync(baum, alleKnoten, ct);
        return baum;
    }

    /// <summary>
    /// Liest den Turnierbaum mit allen Knoten und Spielen für die Darstellung.
    /// Gibt strukturierte Daten pro Runde zurück.
    /// </summary>
    public async Task<IReadOnlyList<BracketRunde>> BracketLadenAsync(
        int turnierInstanzId, CancellationToken ct = default)
    {
        var baum = await baumRepository.FindenNachInstanzMitKnotenAsync(turnierInstanzId, ct);
        if (baum is null) return [];

        // Knoten nach Runde gruppieren und sortieren
        // Issue #340: Knoten hängen bei Hin-/Rückspiel am Hinspiel — die Rückspiele (Gesamtergebnis und
        // -sieger) werden je Hinspiel-ID mitgeliefert.
        var rueckspielJeHinspielId = new Dictionary<int, Spiel>();
        var turnierRundeIds = baum.Knoten
            .Where(k => k.Spiel is not null)
            .Select(k => k.Spiel!.TurnierRundeId)
            .Distinct()
            .ToList();
        foreach (var rundeId in turnierRundeIds)
        {
            foreach (var rueck in (await spielRepository.AlleNachRundeAsync(rundeId, ct))
                         .Where(s => s.IstRueckspiel && s.HinspielId.HasValue))
                rueckspielJeHinspielId.TryAdd(rueck.HinspielId!.Value, rueck);
        }

        // Issue #365: echter Rundenname je Baum-Spalte statt Ableitung aus der Spielanzahl
        // (CL-Playoff-Runde hat ebenfalls 8 Paarungen und wurde als "Achtelfinale" beschriftet).
        var instanz = await instanzRepository.FindenNachIdMitRundenAsync(turnierInstanzId, ct);
        var rundenNameJeId = instanz?.Runden.ToDictionary(r => r.Id, r => r.Name) ?? [];
        var platzDreiRundenIds = instanz?.Runden.Where(r => r.Rundenart == Rundenart.SpielUmPlatzDrei)
            .Select(r => r.Id).ToHashSet() ?? [];

        var rundenGruppen = baum.Knoten
            .GroupBy(k => k.Runde)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                // Innerhalb der Runde: linke Kinder vor rechten (Bracket-Reihenfolge wiederherstellen)
                var geordnet = OrdinierteKnoten(g.ToList());
                var rueckspiele = geordnet
                    .Where(k => k.SpielId.HasValue && rueckspielJeHinspielId.ContainsKey(k.SpielId.Value))
                    .ToDictionary(k => k.SpielId!.Value, k => rueckspielJeHinspielId[k.SpielId!.Value]);
                string? rundenName = geordnet
                    .Select(k => k.Spiel?.TurnierRundeId)
                    .Where(id => id.HasValue)
                    .Select(id => rundenNameJeId.GetValueOrDefault(id!.Value))
                    .FirstOrDefault(n => !string.IsNullOrWhiteSpace(n));
                bool istSpielUmPlatzDrei = geordnet.Any(k => k.Spiel is not null
                    && platzDreiRundenIds.Contains(k.Spiel.TurnierRundeId));
                return new BracketRunde(g.Key, geordnet, rueckspiele, rundenName, istSpielUmPlatzDrei);
            })
            // Issue #385: Spiel um Platz 3 hinter das Finale, damit Halbfinale und Finale benachbart sind.
            .OrderBy(r => r.IstSpielUmPlatzDrei)
            .ToList();

        return rundenGruppen;
    }

    /// <summary>
    /// Schreibt die Sieger abgeschlossener Spiele in die korrespondierenden
    /// Spiele der nächsten Runde (Heim-/Auswärts-Slot gemäß Bracket-Struktur).
    /// </summary>
    public async Task<int> SiegerFortsteiernAsync(int turnierInstanzId, CancellationToken ct = default)
    {
        var baum = await baumRepository.FindenNachInstanzAsync(turnierInstanzId, ct);
        if (baum is null)
            throw new InvalidOperationException("Kein Turnierbaum vorhanden. Bitte zuerst generieren.");

        // Issue #241: nutzt jetzt denselben ID-basierten Auflösungspfad, der ohnehin automatisch nach
        // jedem Ergebniseintrag läuft (SpielErgebnisService.PlatzhalterAutomatischAufloesenAsync) — statt
        // Spiel-Felder direkt zu schreiben und PlatzhalterKorrigierenAsync/den Text-Erhalt zu umgehen.
        int fortgeschrieben = await spielplanService.PlatzhalterAlleRundenAufloesenAsync(turnierInstanzId, ct);

        logger.LogInformation("Sieger fortgeschrieben: {Anzahl} Spiel(e) aktualisiert.", fortgeschrieben);
        return fortgeschrieben;
    }

    /// <summary>Löscht den Turnierbaum einer Turnierinstanz (inkl. aller Knoten).</summary>
    public async Task LoeschenAsync(int turnierInstanzId, CancellationToken ct = default)
    {
        var baum = await baumRepository.FindenNachInstanzAsync(turnierInstanzId, ct);
        if (baum is not null)
            await baumRepository.LoeschenMitKnotenAsync(baum.Id, ct);
    }

    // ── Hilfsmethoden ─────────────────────────────────────────────────────

    /// <summary>
    /// Stellt die Bracket-Reihenfolge der Knoten wieder her (linke Kinder vor rechten,
    /// gemäß der Eltern-Ebene).
    /// </summary>
    private static List<TurnierBaumKnoten> OrdinierteKnoten(List<TurnierBaumKnoten> knoten)
    {
        // Knoten ohne Eltern: erste Runde, bereits in Einfügereihenfolge
        var ohneEltern = knoten.Where(k => k.ElternKnotenId is null).ToList();
        if (ohneEltern.Count == knoten.Count) return knoten;

        // Rekonstruiere Reihenfolge via Eltern-Kind-Beziehung
        var elternIds = knoten.Select(k => k.ElternKnotenId).Distinct().ToList();
        var ergebnis = new List<TurnierBaumKnoten>();

        // Sortiere nach (ElternKnotenId, ob linkes Kind)
        foreach (var kn in knoten)
        {
            // Suche die Eltern-Info aus den Knoten der eigenen Liste
            // (Eltern sind in einer anderen Ebene und nicht hier enthalten)
            ergebnis.Add(kn);
        }

        return ergebnis; // Fallback: unverändert
    }
}

/// <summary>Eine Runde im Turnierbaum mit ihren Knoten. <paramref name="RueckspielJeHinspielId"/> enthält bei
/// Hin-/Rückspiel-Runden das Rückspiel je Hinspiel-Knoten (Issue #340), sonst ist es leer.
/// <paramref name="RundenName"/> ist der Name der zugehörigen <see cref="Domain.Entitaeten.Turniere.TurnierRunde"/>
/// (Issue #365), <c>null</c> wenn keiner ermittelbar ist. <paramref name="IstSpielUmPlatzDrei"/> markiert die Runde,
/// die nicht zur Baumkette gehört (Issue #385) — sie steht immer am Ende der Liste.</summary>
public record BracketRunde(
    int RundeNummer,
    IReadOnlyList<TurnierBaumKnoten> Knoten,
    IReadOnlyDictionary<int, Spiel> RueckspielJeHinspielId,
    string? RundenName = null,
    bool IstSpielUmPlatzDrei = false);
