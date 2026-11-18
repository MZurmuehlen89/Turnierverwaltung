namespace Fussballturnierverwaltung.Domain.Enums;

/// <summary>Wie schnell eine Mannschaft ermüdet und wie stark sich das in der Schlussphase und in der Verlängerung
/// zeigt (#677). „Normal“ ist der Standardwert (0) und verhält sich exakt wie vor #677.</summary>
public enum Kondition
{
    Schwach = -1,
    Normal  = 0,
    Stark   = 1
}
