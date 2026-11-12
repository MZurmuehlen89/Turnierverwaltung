using Fussballturnierverwaltung.Application.Spielplan;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Entitaeten.Turnierbaum;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Fussballturnierverwaltung.Application.Turnierbaum;

public class TurnierBaumService(
    ITurnierBaumRepository baumRepository,
    ITurnierInstanzRepository instanzRepository,
    ISpielRepository spielRepository,
    KoQuellverknuepfung quellverknuepfung,
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
        await quellverknuepfung.AlleRundenVerknuepfenAsync(turnierInstanzId, ct);

        var koRunden = TurnierBaumPlan.KoRunden(instanz.Runden);

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

    /// <summary>Löscht den Turnierbaum einer Turnierinstanz (inkl. aller Knoten).</summary>
    public async Task LoeschenAsync(int turnierInstanzId, CancellationToken ct = default)
    {
        var baum = await baumRepository.FindenNachInstanzAsync(turnierInstanzId, ct);
        if (baum is not null)
            await baumRepository.LoeschenMitKnotenAsync(baum.Id, ct);
    }
}
