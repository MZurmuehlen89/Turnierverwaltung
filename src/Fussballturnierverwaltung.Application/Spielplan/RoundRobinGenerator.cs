using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Spielplan;

/// <summary>
/// Berger-Tabellen-Algorithmus (Kreismethode) für Liga und Gruppenphase.
/// Jede Mannschaft spielt genau einmal gegen jede andere.
/// Bei ungerader Anzahl erhält immer eine Mannschaft ein Freilos.
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

        // Bei ungerader Anzahl: Dummy (null) hinzufügen → Freilos-Runden
        bool hatDummy = n % 2 == 1;
        if (hatDummy) teams.Add(null!);

        int gesamt    = teams.Count;           // gerade
        int spieltage = gesamt - 1;
        var spiele    = new List<Spiel>();
        int spielNr   = kontext.StartSpielNummer;

        // Kreismethode: teams[0] ist fest, der Rest rotiert
        var kreis = Enumerable.Range(1, gesamt - 1).ToArray(); // Indizes in teams[]

        for (int spieltag = 0; spieltag < spieltage; spieltag++)
        {
            // Paarung: teams[0] vs teams[kreis[letzter]]
            var heim0 = spieltag % 2 == 0 ? teams[0] : teams[kreis[^1]];
            var ausw0 = spieltag % 2 == 0 ? teams[kreis[^1]] : teams[0];

            if (heim0 is not null && ausw0 is not null)
                spiele.Add(Erstellen(kontext, heim0.Id, ausw0.Id, spielNr++, spieltag + 1));

            // Restliche Paarungen: kreis[i] vs kreis[n-3-i] (letzter kreis-Eintrag ist schon gepaart)
            for (int i = 0; i < (gesamt - 2) / 2; i++)
            {
                var heim = teams[kreis[i]];
                var ausw = teams[kreis[gesamt - 3 - i]];

                if (heim is not null && ausw is not null)
                    spiele.Add(Erstellen(kontext, heim.Id, ausw.Id, spielNr++, spieltag + 1));
            }

            // Rotation: letztes Element des Kreises an den Anfang
            var letzter = kreis[^1];
            for (int k = gesamt - 2; k > 0; k--) kreis[k] = kreis[k - 1];
            kreis[0] = letzter;
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
