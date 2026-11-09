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

    /// <summary>Warum eine Runde dieser Rundenart keine fest vorgegebenen Paarungen haben kann — oder <c>null</c>.
    /// Im Schweizer System folgen die Gegner je Spieltag aus der Tabelle; fest vorgeben ließe sich nur der erste
    /// Spieltag, und den legt die Auslosung fest (Nutzerentscheid #627).</summary>
    public static string? FesteVorgabeSperrGrund(Rundenart rundenart) => rundenart == Rundenart.SchweizerSystem
        ? "Beim Schweizer System ergeben sich die Paarungen je Spieltag aus der Tabelle – feste Paarungen gibt es nicht."
        : null;
}
