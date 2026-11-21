namespace Fussballturnierverwaltung.Domain.Enums;

/// <summary>Wie sicher eine Mannschaft im Elfmeterschießen trifft, vor allem unter Druck (#669). „Normal“ ist der
/// Standardwert (0) und verhält sich exakt wie vor #669.</summary>
public enum Nervenstaerke
{
    Schwach = -1,
    Normal  = 0,
    Stark   = 1
}
