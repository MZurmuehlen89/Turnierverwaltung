using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Application.Zufall;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Spielplan;

/// <summary>
/// Spielplan-Generator für die UCL-artige TopfLiga: Jedes Team spielt K Spiele pro Topf
/// (ermittelt durch Auslosung). Die Paarungen werden aus dem SpielplanKontext gelesen und per
/// <see cref="SpieltagZerlegung"/> exakt auf so viele Spieltage verteilt, wie jedes Team Spiele hat
/// (UCL: 8 Spieltage à 18 Spiele), inkl. ausgeglichenem Heim-/Auswärtsrhythmus (Issue #347).
/// </summary>
public class TopfLigaGenerator(Zufallsquelle zufallsquelle) : ISpielplanGenerator
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

        var spieltagJePaarung = SpieltagZerlegung.Zerlegen(paarungen, zufallsquelle.Erzeugen())
            ?? throw new InvalidOperationException(
                "Die ausgelosten Paarungen lassen sich nicht konfliktfrei auf die Spieltage verteilen. " +
                "Bitte Auslosung wiederholen.");

        var spiele  = new List<Spiel>();
        int spielNr = kontext.StartSpielNummer;

        // Spielnummern in Spieltag-Reihenfolge vergeben, damit Spieltag 1 auch die ersten Nummern trägt
        foreach (int i in Enumerable.Range(0, paarungen.Count).OrderBy(i => spieltagJePaarung[i]))
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

        return new SpielplanErgebnis { Runde = kontext.Runde, Spiele = spiele };
    }
}
