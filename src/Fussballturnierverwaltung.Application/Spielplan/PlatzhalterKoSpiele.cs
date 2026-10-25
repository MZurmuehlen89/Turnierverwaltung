using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Spielplan;

/// <summary>Die Spiele einer K.-o.-Runde aus Platzhalter-Paarungen (z.B. "Sieger Gruppe A") — ohne Datenbank. Wie im
/// <see cref="KoRundeGenerator"/>: mit Rückspiel erst alle Hinspiele (Spieltag 1), dann alle gespiegelten Rückspiele
/// (Spieltag 2), nummeriert so, wie sie ausgetragen werden (Issue #346).</summary>
public static class PlatzhalterKoSpiele
{
    /// <summary>Die Hinspiele (ohne Rückspiel: alle Spiele) und je Hinspiel sein Rückspiel.</summary>
    public static (List<Spiel> Hinspiele, List<(Spiel Hinspiel, Spiel Rueckspiel)> Rueckspiele) Erzeugen(
        TurnierRunde runde, IReadOnlyList<(string Heim, string Auswaerts)> paarungen, int startNr)
    {
        var hinspiele = paarungen
            .Select((p, i) => Erstellen(runde, p.Heim, p.Auswaerts, startNr + i, istRueckspiel: false))
            .ToList();
        if (!runde.MitRueckspiel) return (hinspiele, []);

        var rueckspiele = hinspiele
            .Select(h => (Hinspiel: h, Rueckspiel: Erstellen(runde, h.AuswaertsPlatzhalterName!, h.HeimPlatzhalterName!,
                h.Spielnummer + hinspiele.Count, istRueckspiel: true)))
            .ToList();
        return (hinspiele, rueckspiele);
    }

    private static Spiel Erstellen(TurnierRunde runde, string heim, string auswaerts, int spielNr, bool istRueckspiel) =>
        new()
        {
            TurnierInstanzId         = runde.TurnierInstanzId,
            TurnierRundeId           = runde.Id,
            Spielnummer              = spielNr,
            Spieltag                 = istRueckspiel ? 2 : runde.MitRueckspiel ? 1 : null,
            HeimPlatzhalterName      = heim,
            AuswaertsPlatzhalterName = auswaerts,
            Status                   = SpielStatus.Geplant,
            SimulationsModus         = SimulationsModus.ManuellesErgebnis,
            IstRueckspiel            = istRueckspiel,
        };
}
