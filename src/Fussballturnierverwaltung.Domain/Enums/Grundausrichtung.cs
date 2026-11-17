namespace Fussballturnierverwaltung.Domain.Enums;

/// <summary>Taktische Grundausrichtung einer Mannschaft (#667): wo sie auf der Risikoachse ins Spiel geht. Die Werte
/// sind Stufen um „ausgeglichen“ herum – je Stufe ein halber Schritt auf der Achse und fünf Minuten Verschiebung der
/// Umstellzeitpunkte.</summary>
public enum Grundausrichtung
{
    SehrDefensiv = -2,
    Defensiv     = -1,
    Ausgeglichen = 0,
    Offensiv     = 1,
    SehrOffensiv = 2
}
