using Fussballturnierverwaltung.Application.Spielplan;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Entitaeten.Turnierbaum;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Turnierbaum;

/// <summary>Liest den gespeicherten Turnierbaum für die Anzeige (#506: aus <see cref="TurnierBaumService"/>
/// herausgelöst, der den Baum erzeugt).</summary>
public class BracketLeser(
    ITurnierBaumRepository baumRepository,
    ITurnierInstanzRepository instanzRepository,
    ISpielRepository spielRepository)
{
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
        var rundeJeId = instanz?.Runden.ToDictionary(r => r.Id) ?? [];
        var koRunden = instanz is null ? [] : TurnierBaumPlan.KoRunden(instanz.Runden);

        var rundenGruppen = baum.Knoten
            .GroupBy(k => k.Runde)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var geordnet = g.ToList();
                var rueckspiele = geordnet
                    .Where(k => k.SpielId.HasValue && rueckspielJeHinspielId.ContainsKey(k.SpielId.Value))
                    .ToDictionary(k => k.SpielId!.Value, k => rueckspielJeHinspielId[k.SpielId!.Value]);
                var runde = TurnierRundeDerEbene(geordnet, g.Key, rundeJeId, koRunden);
                string? rundenName = string.IsNullOrWhiteSpace(runde?.Name) ? null : runde.Name;
                bool istSpielUmPlatzDrei = runde?.Rundenart == Rundenart.SpielUmPlatzDrei;
                return new BracketRunde(g.Key, geordnet, rueckspiele, rundenName, istSpielUmPlatzDrei);
            })
            .ToList();

        return PlatzDreiVorDasFinale(rundenGruppen);
    }

    /// <summary>Die Turnierrunde einer Baum-Ebene: aus den Spielen ihrer Knoten, bei einer Ebene nur aus Platzhaltern
    /// (Runde noch nicht ausgelost) über die Ebenennummer — Ebene n ist die n-te K.-o.-Runde, wie beim Erzeugen
    /// (#567: sonst hieß das noch offene Spiel um Platz 3 nach seiner Spielzahl „Finale“ und galt nicht als
    /// Spiel um Platz 3).</summary>
    private static TurnierRunde? TurnierRundeDerEbene(List<TurnierBaumKnoten> knoten, int ebene,
        Dictionary<int, TurnierRunde> rundeJeId, IReadOnlyList<TurnierRunde> koRunden)
    {
        int? rundeId = knoten.Select(k => k.Spiel?.TurnierRundeId).FirstOrDefault(id => id.HasValue);
        return rundeId is int id ? rundeJeId.GetValueOrDefault(id) : koRunden.ElementAtOrDefault(ebene - 1);
    }

    /// <summary>#567 (Vorbild kicker.de): Halbfinale → Spiel um Platz 3 → Finale, auch wenn das Spiel um Platz 3 per
    /// Reihenfolge erst nach dem Finale kommt. Die Linien verbinden die drei trotzdem durchgehend — anders als mit
    /// #385, wo Platz 3 ohne Linien hinter dem Finale stand. Ohne echtes Finale (letzte Runde der Kette mit genau
    /// einer Karte, wie in <see cref="RundenbaumLayout"/>) bleibt Platz 3 am Ende.</summary>
    private static List<BracketRunde> PlatzDreiVorDasFinale(List<BracketRunde> runden)
    {
        var kette = runden.Where(r => !r.IstSpielUmPlatzDrei).ToList();
        var platzDrei = runden.Where(r => r.IstSpielUmPlatzDrei);
        if (kette.Count == 0 || kette[^1].Knoten.Count != 1) return [.. kette, .. platzDrei];
        return [.. kette.Take(kette.Count - 1), .. platzDrei, kette[^1]];
    }
}

/// <summary>Eine Runde im Turnierbaum mit ihren Knoten. <paramref name="RueckspielJeHinspielId"/> enthält bei
/// Hin-/Rückspiel-Runden das Rückspiel je Hinspiel-Knoten (Issue #340), sonst ist es leer.
/// <paramref name="RundenName"/> ist der Name der zugehörigen <see cref="Domain.Entitaeten.Turniere.TurnierRunde"/>
/// (Issue #365), <c>null</c> wenn keiner ermittelbar ist. <paramref name="IstSpielUmPlatzDrei"/> markiert die Runde,
/// die nicht zur Baumkette gehört (Issue #385) — sie steht direkt vor der letzten Runde der Kette (#567).</summary>
public record BracketRunde(
    int RundeNummer,
    IReadOnlyList<TurnierBaumKnoten> Knoten,
    IReadOnlyDictionary<int, Spiel> RueckspielJeHinspielId,
    string? RundenName = null,
    bool IstSpielUmPlatzDrei = false);
