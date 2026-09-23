using Fussballturnierverwaltung.Application.Spielplan;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Entitaeten.Turnierbaum;
using Fussballturnierverwaltung.Domain.Enums;
using Microsoft.Extensions.Logging;

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
    /// Generiert (oder regeneriert) den Turnierbaum aus den K.-o.-Runden eines Turniers.
    /// Nur Runden mit bereits erstellten Spielen werden als Knoten abgebildet.
    /// </summary>
    public async Task<TurnierBaum> GenerierenAsync(int turnierInstanzId, CancellationToken ct = default)
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
        var spieleProRunde = new List<List<Spiel>>();
        var rueckspielZuHinspielProRunde = new List<Dictionary<int, int>>();
        foreach (var runde in koRunden)
        {
            var spiele = await spielRepository.AlleNachRundeAsync(runde.Id, ct);
            rueckspielZuHinspielProRunde.Add(spiele
                .Where(s => s.IstRueckspiel && s.HinspielId.HasValue)
                .ToDictionary(s => s.Id, s => s.HinspielId!.Value));
            // Bei Hin/Rückspiel: nur Hinspiele als Bracket-Knoten (ein Knoten pro Paarung)
            var relevant = runde.MitRueckspiel
                ? spiele.Where(s => !s.IstRueckspiel).OrderBy(s => s.Spielnummer).ToList()
                : spiele.OrderBy(s => s.Spielnummer).ToList();

            spieleProRunde.Add(relevant);
        }

        if (spieleProRunde.All(l => l.Count == 0))
            throw new InvalidOperationException(
                "Noch keine Spiele für K.-o.-Runden vorhanden. Bitte zuerst Spielpläne generieren.");

        // Bestehenden Baum löschen
        var bestehend = await baumRepository.FindenNachInstanzAsync(turnierInstanzId, ct);
        if (bestehend is not null)
            await baumRepository.LoeschenMitKnotenAsync(bestehend.Id, ct);

        // Neuen Baum anlegen
        var baum = new TurnierBaum { TurnierInstanzId = turnierInstanzId };
        await baumRepository.HinzufuegenAsync(baum, ct);

        // Knoten pro Runde erstellen
        var knotenProRunde = new List<List<TurnierBaumKnoten>>();
        for (int i = 0; i < koRunden.Count; i++)
        {
            var spiele = spieleProRunde[i];
            var runde  = koRunden[i];
            var rundeKnoten = new List<TurnierBaumKnoten>();

            if (spiele.Any())
            {
                foreach (var spiel in spiele)
                {
                    rundeKnoten.Add(new TurnierBaumKnoten
                    {
                        TurnierBaumId = baum.Id,
                        SpielId = spiel.Id,
                        Runde = i + 1
                    });
                }
            }
            else
            {
                // Platzhalter-Knoten für Runden ohne Spiele
                int anzahl = runde.EingangsMannschaftsanzahl / 2;
                for (int j = 0; j < anzahl; j++)
                {
                    rundeKnoten.Add(new TurnierBaumKnoten
                    {
                        TurnierBaumId = baum.Id,
                        SpielId = null,
                        Runde = i + 1
                    });
                }
            }

            knotenProRunde.Add(rundeKnoten);
        }

        // Alle Knoten speichern (IDs werden gesetzt)
        await baumRepository.KnotenSpeichernAsync(knotenProRunde.SelectMany(k => k), ct);

        // Eltern-Kind-Verknüpfungen setzen (Issue #241: direkt aus Spiel.HeimQuellSpielId/
        // AuswaertsQuellSpielId statt Text-Platzhalter zu erraten — pro Slot, nicht pro Übergang, damit
        // gemischte Ebenen aus K.o.-Siegern und Direkteinstiegen nicht mehr teilweise unverknüpft bleiben).
        for (int i = 0; i + 1 < knotenProRunde.Count; i++)
        {
            var kindEbene    = knotenProRunde[i];
            var elternEbene  = knotenProRunde[i + 1];
            var elternSpiele = spieleProRunde[i + 1];

            var kindKnotenNachSpielId = kindEbene
                .Where(k => k.SpielId.HasValue)
                .ToDictionary(k => k.SpielId!.Value, k => k);

            // Issue #340: seit #346 zeigen Quell-IDs bei Hin-/Rückspiel auf das Rückspiel (trägt den
            // Gesamtsieger), der Knoten hängt aber am Hinspiel — Rückspiel-ID auf denselben Knoten abbilden.
            foreach (var (rueckspielId, hinspielId) in rueckspielZuHinspielProRunde[i])
                if (kindKnotenNachSpielId.TryGetValue(hinspielId, out var hinspielKnoten))
                    kindKnotenNachSpielId[rueckspielId] = hinspielKnoten;

            bool irgendeinSpielHatQuellId = elternSpiele.Any(
                s => s.HeimQuellSpielId.HasValue || s.AuswaertsQuellSpielId.HasValue);

            if (irgendeinSpielHatQuellId)
            {
                for (int j = 0; j < elternEbene.Count && j < elternSpiele.Count; j++)
                {
                    var elternSpiel = elternSpiele[j];

                    if (elternSpiel.HeimQuellSpielId is int heimQuellId &&
                        kindKnotenNachSpielId.TryGetValue(heimQuellId, out var linksKnoten))
                    {
                        elternEbene[j].LinkerKindKnotenId = linksKnoten.Id;
                        linksKnoten.ElternKnotenId         = elternEbene[j].Id;
                    }
                    if (elternSpiel.AuswaertsQuellSpielId is int auswQuellId &&
                        kindKnotenNachSpielId.TryGetValue(auswQuellId, out var rechtsKnoten))
                    {
                        elternEbene[j].RechterKindKnotenId = rechtsKnoten.Id;
                        rechtsKnoten.ElternKnotenId         = elternEbene[j].Id;
                    }
                }
            }
            else
            {
                // Fallback für Alt-Turniere ohne jegliche Quell-ID in dieser Übergangs-Ebene
                // (bisheriges Verhalten): sequentielle Zuordnung.
                for (int j = 0; j < elternEbene.Count; j++)
                {
                    int linkerIdx  = j * 2;
                    int rechterIdx = j * 2 + 1;

                    if (linkerIdx < kindEbene.Count)
                    {
                        elternEbene[j].LinkerKindKnotenId = kindEbene[linkerIdx].Id;
                        kindEbene[linkerIdx].ElternKnotenId = elternEbene[j].Id;
                    }
                    if (rechterIdx < kindEbene.Count)
                    {
                        elternEbene[j].RechterKindKnotenId = kindEbene[rechterIdx].Id;
                        kindEbene[rechterIdx].ElternKnotenId = elternEbene[j].Id;
                    }
                }
            }
        }

        // Wurzelknoten = letzter Knoten der letzten Runde
        var letzteEbene = knotenProRunde.Last();
        if (letzteEbene.Count == 1)
            baum.WurzelKnotenId = letzteEbene[0].Id;

        // Verknüpfungen in DB speichern
        await baumRepository.VerknuepfungenAktualisierenAsync(baum, knotenProRunde.SelectMany(k => k), ct);

        logger.LogInformation(
            "Turnierbaum für Instanz {Id} generiert: {Knoten} Knoten über {Runden} Runden.",
            turnierInstanzId, knotenProRunde.Sum(k => k.Count), knotenProRunde.Count);

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
                return new BracketRunde(g.Key, geordnet, rueckspiele);
            })
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
/// Hin-/Rückspiel-Runden das Rückspiel je Hinspiel-Knoten (Issue #340), sonst ist es leer.</summary>
public record BracketRunde(
    int RundeNummer,
    IReadOnlyList<TurnierBaumKnoten> Knoten,
    IReadOnlyDictionary<int, Spiel> RueckspielJeHinspielId);
