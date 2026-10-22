using Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Auslosungen;

/// <summary>Welche Auslosungen die Vorbelegung führt und welche die Durchführung (#482): die Vorbelegung führt die
/// Manuell-Auslosungen der Runden mit „Paarung fest vorgegeben“. Eine Manuell-Auslosung einer Runde ohne Haken ist
/// eine Ziehung von Hand der Durchführung, eine andere Art in einer Runde mit Haken (Haken nachträglich gesetzt)
/// bleibt ebenfalls in der Durchführung, wo sie sich zurücksetzen lässt.</summary>
public static class VorbelegungsZustaendigkeit
{
    public static bool GehoertZurVorbelegung(Auslosung auslosung) =>
        auslosung.Auslosungsart == Auslosungsart.Manuell && auslosung.TurnierRunde is { PaarungFestVorgegeben: true };
}
