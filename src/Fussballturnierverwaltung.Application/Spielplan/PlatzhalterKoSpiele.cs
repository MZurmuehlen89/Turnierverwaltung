using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;

namespace Fussballturnierverwaltung.Application.Spielplan;

/// <summary>Die Spiele einer K.-o.-Runde aus Platzhalter-Paarungen (z.B. "Sieger Gruppe A") — ohne Datenbank, in
/// derselben <see cref="KoSpielfolge"/> wie der <see cref="KoRundeGenerator"/> (#620).</summary>
public static class PlatzhalterKoSpiele
{
    /// <summary>Die Hinspiele (ohne Rückspiel: alle Spiele) und je Hinspiel sein Rückspiel.</summary>
    public static (List<Spiel> Hinspiele, List<(Spiel Hinspiel, Spiel Rueckspiel)> Rueckspiele) Erzeugen(
        TurnierRunde runde, IReadOnlyList<(string Heim, string Auswaerts)> paarungen, int startNr) =>
        KoSpielfolge.Erzeugen(runde, paarungen, startNr, (heim, auswaerts) => new Spiel
        {
            HeimPlatzhalterName      = heim,
            AuswaertsPlatzhalterName = auswaerts,
        });
}
