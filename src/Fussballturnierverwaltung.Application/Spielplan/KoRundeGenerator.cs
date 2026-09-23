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
        int paarungen = n / 2;
        var spiele = new List<Spiel>();
        var paare  = new List<(int HinspielIndex, int RueckspielIndex)>();

        // Hinspiele (bzw. Einzelspiele) zuerst, danach — bei Hin-/Rückspiel — alle Rückspiele in derselben
        // Paarungsreihenfolge: nummeriert so, wie die Spiele tatsächlich ausgetragen werden (Issue #346).
        for (int p = 0; p < paarungen; p++)
        {
            spiele.Add(new Spiel
            {
                TurnierInstanzId      = kontext.Runde.TurnierInstanzId,
                TurnierRundeId        = kontext.Runde.Id,
                Spielnummer           = kontext.StartSpielNummer + p,
                Spieltag              = mitRueckspiel ? 1 : null,
                HeimMannschaftId      = teams[2 * p].Id,
                AuswaertsMannschaftId = teams[2 * p + 1].Id,
                Status                = SpielStatus.Geplant,
                SimulationsModus      = SimulationsModus.SchnellSimulation,
                IstRueckspiel         = false
            });
        }

        if (mitRueckspiel)
        {
            for (int p = 0; p < paarungen; p++)
            {
                spiele.Add(new Spiel
                {
                    TurnierInstanzId      = kontext.Runde.TurnierInstanzId,
                    TurnierRundeId        = kontext.Runde.Id,
                    Spielnummer           = kontext.StartSpielNummer + paarungen + p,
                    Spieltag              = 2,
                    HeimMannschaftId      = teams[2 * p + 1].Id,
                    AuswaertsMannschaftId = teams[2 * p].Id,
                    Status                = SpielStatus.Geplant,
                    SimulationsModus      = SimulationsModus.SchnellSimulation,
                    IstRueckspiel         = true
                });
                paare.Add((p, paarungen + p));
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
