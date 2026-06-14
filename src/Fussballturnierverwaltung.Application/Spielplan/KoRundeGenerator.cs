using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Spielplan;

/// <summary>
/// Direktes K.-o.-System. Unterstützt optionale Hin- und Rückspiele via MitRueckspiel auf der TurnierRunde.
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

        bool mitRueckspiel = kontext.Runde.MitRueckspiel;
        var spiele = new List<Spiel>();
        var paare  = new List<(int HinspielIndex, int RueckspielIndex)>();
        int spielNr = kontext.StartSpielNummer;

        for (int i = 0; i < n; i += 2)
        {
            if (mitRueckspiel)
            {
                int hinIdx   = spiele.Count;
                int rueckIdx = hinIdx + 1;

                spiele.Add(new Spiel
                {
                    TurnierInstanzId      = kontext.Runde.TurnierInstanzId,
                    TurnierRundeId        = kontext.Runde.Id,
                    Spielnummer           = spielNr++,
                    Spieltag              = 1,
                    HeimMannschaftId      = teams[i].Id,
                    AuswaertsMannschaftId = teams[i + 1].Id,
                    Status                = SpielStatus.Geplant,
                    SimulationsModus      = SimulationsModus.SchnellSimulation,
                    IstRueckspiel         = false
                });

                spiele.Add(new Spiel
                {
                    TurnierInstanzId      = kontext.Runde.TurnierInstanzId,
                    TurnierRundeId        = kontext.Runde.Id,
                    Spielnummer           = spielNr++,
                    Spieltag              = 2,
                    HeimMannschaftId      = teams[i + 1].Id,
                    AuswaertsMannschaftId = teams[i].Id,
                    Status                = SpielStatus.Geplant,
                    SimulationsModus      = SimulationsModus.SchnellSimulation,
                    IstRueckspiel         = true
                });

                paare.Add((hinIdx, rueckIdx));
            }
            else
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
        }

        return new SpielplanErgebnis
        {
            Runde              = kontext.Runde,
            Spiele             = spiele,
            HinRueckspielPaare = mitRueckspiel ? paare : []
        };
    }
}
