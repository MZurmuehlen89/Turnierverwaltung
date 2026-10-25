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
        int n = kontext.Mannschaften.Count;

        if (n < 2) throw new InvalidOperationException(
            $"Für K.-o.-Runden werden mindestens 2 Mannschaften benötigt (waren: {n}).");

        if (n % 2 != 0) throw new InvalidOperationException(
            $"K.-o.-Runden benötigen eine gerade Mannschaftsanzahl (waren: {n}).");

        // Hinspiele (bzw. Einzelspiele) zuerst, danach — bei Hin-/Rückspiel — alle Rückspiele in derselben
        // Paarungsreihenfolge: nummeriert so, wie die Spiele tatsächlich ausgetragen werden (Issue #346).
        var hinspiele = Hinspiele(kontext);
        if (!kontext.Runde.MitRueckspiel)
            return new SpielplanErgebnis { Runde = kontext.Runde, Spiele = hinspiele, HinRueckspielPaare = [] };

        return new SpielplanErgebnis
        {
            Runde              = kontext.Runde,
            Spiele             = [.. hinspiele, .. Rueckspiele(kontext)],
            HinRueckspielPaare = [.. Enumerable.Range(0, hinspiele.Count).Select(p => (p, hinspiele.Count + p))]
        };
    }

    // Paarung p: Team 2p empfängt Team 2p+1
    private static List<Spiel> Hinspiele(SpielplanKontext kontext)
    {
        var teams = kontext.Mannschaften;
        return [.. Enumerable.Range(0, teams.Count / 2).Select(p =>
            Erstellen(kontext, teams[2 * p].Id, teams[2 * p + 1].Id, kontext.StartSpielNummer + p, istRueckspiel: false))];
    }

    private static List<Spiel> Rueckspiele(SpielplanKontext kontext)
    {
        var teams     = kontext.Mannschaften;
        int paarungen = teams.Count / 2;
        return [.. Enumerable.Range(0, paarungen).Select(p =>
            Erstellen(kontext, teams[2 * p + 1].Id, teams[2 * p].Id, kontext.StartSpielNummer + paarungen + p, istRueckspiel: true))];
    }

    // Mit Rückspiel: Hinspiele an Spieltag 1, Rückspiele an Spieltag 2; ein Einzelspiel hat keinen Spieltag
    private static Spiel Erstellen(SpielplanKontext kontext, int heimId, int auswaertsId, int spielNr, bool istRueckspiel) =>
        new()
        {
            TurnierInstanzId      = kontext.Runde.TurnierInstanzId,
            TurnierRundeId        = kontext.Runde.Id,
            Spielnummer           = spielNr,
            Spieltag              = istRueckspiel ? 2 : kontext.Runde.MitRueckspiel ? 1 : null,
            HeimMannschaftId      = heimId,
            AuswaertsMannschaftId = auswaertsId,
            Status                = SpielStatus.Geplant,
            SimulationsModus      = SimulationsModus.SchnellSimulation,
            IstRueckspiel         = istRueckspiel
        };
}
