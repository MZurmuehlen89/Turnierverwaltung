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

        // Paarung p: Team 2p empfängt Team 2p+1; Reihenfolge, Nummern und Spieltage legt die KoSpielfolge fest
        var teams     = kontext.Mannschaften;
        var paarungen = Enumerable.Range(0, n / 2).Select(p => (Heim: teams[2 * p], Auswaerts: teams[2 * p + 1])).ToList();
        var (hinspiele, rueckspiele) = KoSpielfolge.Erzeugen(kontext.Runde, paarungen, kontext.StartSpielNummer,
            (heim, auswaerts) => new Spiel
            {
                HeimMannschaftId      = heim.Id,
                AuswaertsMannschaftId = auswaerts.Id,
            });

        return new SpielplanErgebnis
        {
            Runde              = kontext.Runde,
            Spiele             = [.. hinspiele, .. rueckspiele.Select(r => r.Rueckspiel)],
            HinRueckspielPaare = [.. Enumerable.Range(0, rueckspiele.Count).Select(p => (p, hinspiele.Count + p))]
        };
    }
}
