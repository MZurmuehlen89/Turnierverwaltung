using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Spielplan;

/// <summary>
/// Berger-Tabellen-Algorithmus (Kreismethode) für Liga und Gruppenphase.
/// Mit MitRueckspiel=true wird ein doppelter Durchgang (Hin- und Rückrunde) generiert.
/// </summary>
public class RoundRobinGenerator : ISpielplanGenerator
{
    public IReadOnlyList<Rundenart> UnterstützteRundenarten =>
        [Rundenart.Liga, Rundenart.Gruppenphase];

    public SpielplanErgebnis Generieren(SpielplanKontext kontext)
    {
        var teams = kontext.Mannschaften.ToList();
        int n = teams.Count;

        if (n < 2) throw new InvalidOperationException(
            $"Für Round-Robin werden mindestens 2 Mannschaften benötigt (waren: {n}).");

        bool mitRueckspiel = kontext.Runde.MitRueckspiel;

        // Bei ungerader Anzahl: Dummy (null) hinzufügen → Freilos-Runden
        bool hatDummy = n % 2 == 1;
        if (hatDummy) teams.Add(null!);

        int gesamt    = teams.Count;
        int spieltage = gesamt - 1;
        var spiele    = new List<Spiel>();
        int spielNr   = kontext.StartSpielNummer;

        var kreis = Enumerable.Range(1, gesamt - 1).ToArray();

        for (int spieltag = 0; spieltag < spieltage; spieltag++)
        {
            var heim0 = spieltag % 2 == 0 ? teams[0] : teams[kreis[^1]];
            var ausw0 = spieltag % 2 == 0 ? teams[kreis[^1]] : teams[0];

            if (heim0 is not null && ausw0 is not null)
                spiele.Add(Erstellen(kontext, heim0.Id, ausw0.Id, spielNr++, spieltag + 1));

            for (int i = 0; i < (gesamt - 2) / 2; i++)
            {
                var heim = teams[kreis[i]];
                var ausw = teams[kreis[gesamt - 3 - i]];

                if (heim is not null && ausw is not null)
                    spiele.Add(Erstellen(kontext, heim.Id, ausw.Id, spielNr++, spieltag + 1));
            }

            var letzter = kreis[^1];
            for (int k = gesamt - 2; k > 0; k--) kreis[k] = kreis[k - 1];
            kreis[0] = letzter;
        }

        // Rückrunde: Heim/Auswärts tauschen, Spieltag-Offset += spieltage
        if (mitRueckspiel)
        {
            var hinrunde = spiele.ToList();
            foreach (var s in hinrunde)
            {
                spiele.Add(Erstellen(kontext, s.AuswaertsMannschaftId, s.HeimMannschaftId,
                    spielNr++, (s.Spieltag ?? 0) + spieltage));
            }
        }

        return new SpielplanErgebnis { Runde = kontext.Runde, Spiele = spiele };
    }

    private static Spiel Erstellen(SpielplanKontext k, int heimId, int auswId, int spielNr, int spieltag) =>
        new()
        {
            TurnierInstanzId      = k.Runde.TurnierInstanzId,
            TurnierRundeId        = k.Runde.Id,
            Spielnummer           = spielNr,
            Spieltag              = spieltag,
            HeimMannschaftId      = heimId,
            AuswaertsMannschaftId = auswId,
            Status                = SpielStatus.Geplant,
            SimulationsModus      = SimulationsModus.SchnellSimulation
        };
}
