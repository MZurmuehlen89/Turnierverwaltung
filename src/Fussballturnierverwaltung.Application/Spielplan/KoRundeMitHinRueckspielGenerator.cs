using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Spielplan;

/// <summary>
/// K.-o.-Runden mit Hin- und Rückspiel. Generiert 2 Spiele pro Paarung.
/// HinspielId der Rückspiele wird vom SpielplanService nach dem ersten SaveChanges gesetzt.
/// Auswärtstorregel wird via AuswaertstorRegelAktiv auf der TurnierRunde gesteuert.
/// </summary>
public class KoRundeMitHinRueckspielGenerator : ISpielplanGenerator
{
    public IReadOnlyList<Rundenart> UnterstützteRundenarten =>
        [Rundenart.KoRundeMitHinUndRueckspiel];

    public SpielplanErgebnis Generieren(SpielplanKontext kontext)
    {
        var teams = kontext.Mannschaften;
        int n = teams.Count;

        if (n < 2) throw new InvalidOperationException(
            $"Für Hin-/Rückspiel-Runden werden mindestens 2 Mannschaften benötigt.");

        if (n % 2 != 0) throw new InvalidOperationException(
            $"Hin-/Rückspiel-Runden benötigen eine gerade Mannschaftsanzahl (waren: {n}).");

        var spiele = new List<Spiel>();
        var paare  = new List<(int HinspielIndex, int RueckspielIndex)>();
        int spielNr = kontext.StartSpielNummer;

        for (int i = 0; i < n; i += 2)
        {
            int hinIdx  = spiele.Count;
            int rueckIdx = hinIdx + 1;

            // Hinspiel: Team i ist Heim
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

            // Rückspiel: Teams tauschen Heim/Auswärts — HinspielId wird nach dem ersten Save gesetzt
            spiele.Add(new Spiel
            {
                TurnierInstanzId      = kontext.Runde.TurnierInstanzId,
                TurnierRundeId        = kontext.Runde.Id,
                Spielnummer           = spielNr++,
                Spieltag              = 2,
                HeimMannschaftId      = teams[i + 1].Id,   // Heim und Auswärts getauscht
                AuswaertsMannschaftId = teams[i].Id,
                Status                = SpielStatus.Geplant,
                SimulationsModus      = SimulationsModus.SchnellSimulation,
                IstRueckspiel         = true
                // HinspielId: noch null, wird vom SpielplanService gesetzt
            });

            paare.Add((hinIdx, rueckIdx));
        }

        return new SpielplanErgebnis
        {
            Runde               = kontext.Runde,
            Spiele              = spiele,
            HinRueckspielPaare  = paare
        };
    }
}
