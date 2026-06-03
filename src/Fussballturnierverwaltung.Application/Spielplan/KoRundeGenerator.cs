using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Spielplan;

/// <summary>
/// Direktes K.-o.-System: Mannschaft i tritt gegen Mannschaft i+1 an.
/// Die Auslosung bestimmt die Reihenfolge. Geeignet für KoRunde, Finale, SpielUmPlatzDrei.
/// </summary>
public class KoRundeGenerator : ISpielplanGenerator
{
    public IReadOnlyList<Rundenart> UnterstützteRundenarten =>
        [Rundenart.KoRunde, Rundenart.Finale, Rundenart.SpielUmPlatzDrei];

    public SpielplanErgebnis Generieren(SpielplanKontext kontext)
    {
        var teams = kontext.Mannschaften;
        int n = teams.Count;

        if (n < 2) throw new InvalidOperationException(
            $"Für K.-o.-Runden werden mindestens 2 Mannschaften benötigt (waren: {n}).");

        if (n % 2 != 0) throw new InvalidOperationException(
            $"K.-o.-Runden benötigen eine gerade Mannschaftsanzahl (waren: {n}).");

        var spiele = new List<Spiel>();
        int spielNr = kontext.StartSpielNummer;

        for (int i = 0; i < n; i += 2)
        {
            spiele.Add(new Spiel
            {
                TurnierInstanzId      = kontext.Runde.TurnierInstanzId,
                TurnierRundeId        = kontext.Runde.Id,
                Spielnummer           = spielNr++,
                HeimMannschaftId      = teams[i].Id,
                AuswaertsMannschaftId = teams[i + 1].Id,
                Status                = SpielStatus.Geplant,
                SimulationsModus      = SimulationsModus.SchnellSimulation
            });
        }

        return new SpielplanErgebnis { Runde = kontext.Runde, Spiele = spiele };
    }
}
