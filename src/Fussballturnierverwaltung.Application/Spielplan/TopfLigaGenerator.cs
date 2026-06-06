using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Spielplan;

/// <summary>
/// Spielplan-Generator für die UCL-artige TopfLiga: Jedes Team spielt K Spiele pro Topf
/// (ermittelt durch Auslosung). Die Paarungen werden aus dem SpielplanKontext gelesen
/// und per greedy Kantenf ärbung auf Spieltage verteilt, sodass kein Team zweimal
/// am gleichen Spieltag antritt.
/// </summary>
public class TopfLigaGenerator : ISpielplanGenerator
{
    public IReadOnlyList<Rundenart> UnterstützteRundenarten => [Rundenart.TopfLiga];

    public SpielplanErgebnis Generieren(SpielplanKontext kontext)
    {
        var paarungen = kontext.Paarungen
            ?? throw new InvalidOperationException(
                "TopfLigaGenerator erwartet Paarungen im SpielplanKontext. " +
                "Bitte eine TopfZuordnung-Auslosung durchführen und deren Ergebnisse übergeben.");

        if (!paarungen.Any())
            throw new InvalidOperationException("Keine Paarungen vorhanden.");

        // Maximale Spielanzahl pro Team = Anzahl Spieltage
        var spieleProTeam = paarungen
            .SelectMany(p => new[] { p.HeimId, p.AuswaertsId })
            .GroupBy(id => id)
            .ToDictionary(g => g.Key, g => g.Count());

        int anzahlSpieltage = spieleProTeam.Values.Max();

        var spieltageProTeam = new Dictionary<int, HashSet<int>>();
        var spieltagJePaarung = new int[paarungen.Count];
        var reihenfolge       = Enumerable.Range(0, paarungen.Count).ToList();

        // Greedy Kantenfärbung mit Shuffle-Wiederholung bei Misserfolg
        for (int versuch = 0; versuch < 20; versuch++)
        {
            reihenfolge = reihenfolge.OrderBy(_ => Random.Shared.Next()).ToList();
            spieltageProTeam.Clear();
            bool erfolg = true;

            foreach (int idx in reihenfolge)
            {
                var (heimId, auswaertsId) = paarungen[idx];

                var belegtHeim      = spieltageProTeam.GetValueOrDefault(heimId)      ?? [];
                var belegtAuswaerts = spieltageProTeam.GetValueOrDefault(auswaertsId) ?? [];

                int? spieltag = null;
                for (int md = 1; md <= anzahlSpieltage + 1; md++)
                {
                    if (!belegtHeim.Contains(md) && !belegtAuswaerts.Contains(md))
                    {
                        spieltag = md;
                        break;
                    }
                }

                if (spieltag is null) { erfolg = false; break; }

                spieltagJePaarung[idx] = spieltag.Value;
                spieltageProTeam.TryAdd(heimId,      []);
                spieltageProTeam.TryAdd(auswaertsId, []);
                spieltageProTeam[heimId].Add(spieltag.Value);
                spieltageProTeam[auswaertsId].Add(spieltag.Value);
            }

            if (erfolg) break;

            if (versuch == 19)
                throw new InvalidOperationException(
                    "Spieltag-Verteilung konnte nicht konfliktfrei berechnet werden. " +
                    "Bitte Auslosung wiederholen.");
        }

        var spiele  = new List<Spiel>();
        int spielNr = kontext.StartSpielNummer;

        for (int i = 0; i < paarungen.Count; i++)
        {
            var (heimId, auswaertsId) = paarungen[i];
            spiele.Add(new Spiel
            {
                TurnierInstanzId      = kontext.Runde.TurnierInstanzId,
                TurnierRundeId        = kontext.Runde.Id,
                Spielnummer           = spielNr++,
                Spieltag              = spieltagJePaarung[i],
                HeimMannschaftId      = heimId,
                AuswaertsMannschaftId = auswaertsId,
                Status                = SpielStatus.Geplant,
                SimulationsModus      = SimulationsModus.SchnellSimulation
            });
        }

        // Spiele nach Spieltag sortieren für eine konsistente Ausgabe
        spiele = spiele.OrderBy(s => s.Spieltag).ThenBy(s => s.Spielnummer).ToList();

        return new SpielplanErgebnis { Runde = kontext.Runde, Spiele = spiele };
    }
}
