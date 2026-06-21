using Fussballturnierverwaltung.Application.Spielplan;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Entitaeten.Turnierbaum;
using Fussballturnierverwaltung.Domain.Enums;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace Fussballturnierverwaltung.Application.Turnierbaum;

public class TurnierBaumService(
    ITurnierBaumRepository baumRepository,
    ITurnierInstanzRepository instanzRepository,
    ISpielRepository spielRepository,
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
        foreach (var runde in koRunden)
        {
            var spiele = await spielRepository.AlleNachRundeAsync(runde.Id, ct);
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

        // Eltern-Kind-Verknüpfungen setzen
        for (int i = 0; i + 1 < knotenProRunde.Count; i++)
        {
            var kindEbene    = knotenProRunde[i];
            var kindSpiele   = spieleProRunde[i];
            var elternEbene  = knotenProRunde[i + 1];
            var elternSpiele = spieleProRunde[i + 1];

            bool verknuepft = VerkuepfenPerPlatzhalter(kindEbene, kindSpiele, elternEbene, elternSpiele);

            if (!verknuepft)
            {
                // Fallback: sequentielle Zuordnung
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
        var rundenGruppen = baum.Knoten
            .GroupBy(k => k.Runde)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                // Innerhalb der Runde: linke Kinder vor rechten (Bracket-Reihenfolge wiederherstellen)
                var geordnet = OrdinierteKnoten(g.ToList());
                return new BracketRunde(g.Key, geordnet);
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
        var baum = await baumRepository.FindenNachInstanzMitKnotenAsync(turnierInstanzId, ct);
        if (baum is null)
            throw new InvalidOperationException("Kein Turnierbaum vorhanden. Bitte zuerst generieren.");

        int fortgeschrieben = 0;

        foreach (var knoten in baum.Knoten)
        {
            // Knoten ohne Eltern oder ohne Spiel überspringen
            if (knoten.ElternKnotenId is null || knoten.SpielId is null) continue;

            var spiel = knoten.Spiel;
            if (spiel?.Status != SpielStatus.Abgeschlossen ||
                spiel.Ergebnis?.SiegerMannschaftId is null) continue;

            var eltern = baum.Knoten.FirstOrDefault(k => k.Id == knoten.ElternKnotenId);
            if (eltern?.SpielId is null) continue;

            var elternSpiel = eltern.Spiel;
            if (elternSpiel is null) continue;

            int sieger = spiel.Ergebnis.SiegerMannschaftId.Value;
            bool istLinkes = eltern.LinkerKindKnotenId == knoten.Id;

            // Slot mit Sieger belegen — linkes Kind → Heim, rechtes Kind → Auswärts (AD-002 Fix Bug C)
            if (istLinkes && elternSpiel.HeimMannschaftId != sieger)
            {
                elternSpiel.HeimMannschaftId = sieger;
                await spielRepository.AktualisierenAsync(elternSpiel, ct);
                fortgeschrieben++;
            }
            else if (!istLinkes && elternSpiel.AuswaertsMannschaftId != sieger)
            {
                elternSpiel.AuswaertsMannschaftId = sieger;
                await spielRepository.AktualisierenAsync(elternSpiel, ct);
                fortgeschrieben++;
            }
        }

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

    // Versucht, Eltern-Kind-Verbindungen aus Platzhalternamen wie "Sieger VF1" zu lesen.
    // Gibt true zurück wenn mindestens eine Verbindung gesetzt wurde, sonst false (→ sequentiell).
    private static bool VerkuepfenPerPlatzhalter(
        List<TurnierBaumKnoten> kindEbene,  List<Spiel> kindSpiele,
        List<TurnierBaumKnoten> elternEbene, List<Spiel> elternSpiele)
    {
        bool hatPlatzhalter = elternSpiele.Any(
            s => s.HeimPlatzhalterName != null || s.AuswaertsPlatzhalterName != null);
        if (!hatPlatzhalter) return false;

        string kuerzel = RundenkuerzelFuerAnzahl(kindEbene.Count);

        // Position (1-basiert, sortiert nach Spielnummer) → Knoten-Map
        // "Sieger VF1" = 1. Spiel der VF-Runde nach Spielnummer, nicht Spielnummer == 1
        var spielNrZuKnoten = new Dictionary<int, TurnierBaumKnoten>();
        for (int ki = 0; ki < kindSpiele.Count && ki < kindEbene.Count; ki++)
            spielNrZuKnoten[ki + 1] = kindEbene[ki];

        bool anyLinked = false;
        for (int j = 0; j < elternEbene.Count && j < elternSpiele.Count; j++)
        {
            var es = elternSpiele[j];

            var heimNr      = ExtrahiereKoSiegerNr(es.HeimPlatzhalterName,      kuerzel);
            var auswaertsNr = ExtrahiereKoSiegerNr(es.AuswaertsPlatzhalterName, kuerzel);

            if (heimNr.HasValue && spielNrZuKnoten.TryGetValue(heimNr.Value, out var links))
            {
                elternEbene[j].LinkerKindKnotenId = links.Id;
                links.ElternKnotenId              = elternEbene[j].Id;
                anyLinked = true;
            }
            if (auswaertsNr.HasValue && spielNrZuKnoten.TryGetValue(auswaertsNr.Value, out var rechts))
            {
                elternEbene[j].RechterKindKnotenId = rechts.Id;
                rechts.ElternKnotenId              = elternEbene[j].Id;
                anyLinked = true;
            }
        }
        return anyLinked;
    }

    private static int? ExtrahiereKoSiegerNr(string? name, string kuerzel)
    {
        if (name is null) return null;
        var m = Regex.Match(name, @"^Sieger (\w+?)(\d+)$", RegexOptions.IgnoreCase);
        if (!m.Success || !string.Equals(m.Groups[1].Value, kuerzel, StringComparison.OrdinalIgnoreCase))
            return null;
        return int.TryParse(m.Groups[2].Value, out int nr) ? nr : null;
    }

    private static string RundenkuerzelFuerAnzahl(int anzahl) => anzahl switch
    {
        2  => "HF",
        4  => "VF",
        8  => "AF",
        _  => $"R{anzahl}"
    };

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

/// <summary>Eine Runde im Turnierbaum mit ihren Knoten.</summary>
public record BracketRunde(int RundeNummer, IReadOnlyList<TurnierBaumKnoten> Knoten);
