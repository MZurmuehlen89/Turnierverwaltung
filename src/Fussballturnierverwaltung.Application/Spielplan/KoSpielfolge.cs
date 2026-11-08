using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Spielplan;

/// <summary>Spielfolge einer K.-o.-Runde, mit festen Mannschaften (<see cref="KoRundeGenerator"/>) wie mit Platzhaltern
/// (<see cref="PlatzhalterKoSpiele"/>): erst alle Hinspiele (bzw. Einzelspiele) in Paarungsreihenfolge, bei Hin- und
/// Rückspiel danach alle gespiegelten Rückspiele — nummeriert so, wie sie ausgetragen werden (Issue #346). Rückspiel =
/// Hinspielnummer + Anzahl Paarungen; darauf verlassen sich <see cref="KoPaarungen"/>, Quellverknüpfung und Kaskade.</summary>
internal static class KoSpielfolge
{
    /// <summary>Die Hinspiele (ohne Rückspiel: alle Spiele) und je Hinspiel sein Rückspiel. <paramref name="spielErstellen"/>
    /// besetzt Heim und Auswärts (und den Simulationsmodus); Runde, Nummer, Spieltag und Status setzt die Spielfolge.</summary>
    public static (List<Spiel> Hinspiele, List<(Spiel Hinspiel, Spiel Rueckspiel)> Rueckspiele) Erzeugen<T>(
        TurnierRunde runde, IReadOnlyList<(T Heim, T Auswaerts)> paarungen, int startNr, Func<T, T, Spiel> spielErstellen)
    {
        var hinspiele = paarungen
            .Select((p, i) => Einordnen(spielErstellen(p.Heim, p.Auswaerts), runde, startNr + i, istRueckspiel: false))
            .ToList();
        if (!runde.MitRueckspiel) return (hinspiele, []);

        var rueckspiele = paarungen
            .Select((p, i) => (
                Hinspiel:   hinspiele[i],
                Rueckspiel: Einordnen(spielErstellen(p.Auswaerts, p.Heim), runde, startNr + paarungen.Count + i, istRueckspiel: true)))
            .ToList();
        return (hinspiele, rueckspiele);
    }

    // Mit Rückspiel: Hinspiele an Spieltag 1, Rückspiele an Spieltag 2; ein Einzelspiel hat keinen Spieltag
    private static Spiel Einordnen(Spiel spiel, TurnierRunde runde, int spielNr, bool istRueckspiel)
    {
        spiel.TurnierInstanzId = runde.TurnierInstanzId;
        spiel.TurnierRundeId   = runde.Id;
        spiel.Spielnummer      = spielNr;
        spiel.Spieltag         = istRueckspiel ? 2 : runde.MitRueckspiel ? 1 : null;
        spiel.Status           = SpielStatus.Geplant;
        spiel.IstRueckspiel    = istRueckspiel;
        return spiel;
    }
}
