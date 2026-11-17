namespace Fussballturnierverwaltung.Domain.Enums;

/// <summary>Wie weit und wie schnell eine Mannschaft von ihrer Grundausrichtung abweicht, wenn die Lage es verlangt
/// (#667). „Normal“ ist der Standardwert (0).</summary>
public enum Flexibilitaet
{
    Starr    = -1,
    Normal   = 0,
    Flexibel = 1
}
