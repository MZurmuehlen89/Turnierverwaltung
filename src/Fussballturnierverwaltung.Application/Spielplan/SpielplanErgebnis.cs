using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;

namespace Fussballturnierverwaltung.Application.Spielplan;

/// <summary>Ergebnis einer Spielplan-Generierung. Spiele sind noch nicht persistiert.</summary>
public class SpielplanErgebnis
{
    public TurnierRunde Runde { get; init; } = null!;

    /// <summary>Alle generierten Spiele in der Reihenfolge der Erstellung.</summary>
    public List<Spiel> Spiele { get; init; } = [];

    /// <summary>
    /// Gesetzt bei MitRueckspiel=true: Paare von (HinspielIndex, RueckspielIndex) in Spiele.
    /// Der Service setzt nach dem ersten SaveChanges die HinspielId auf den Rückspielen.
    /// </summary>
    public List<(int HinspielIndex, int RueckspielIndex)> HinRueckspielPaare { get; init; } = [];

    public bool HatHinUndRueckspiele => HinRueckspielPaare.Count > 0;
}
