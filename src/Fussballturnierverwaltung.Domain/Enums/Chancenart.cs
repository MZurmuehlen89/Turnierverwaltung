namespace Fussballturnierverwaltung.Domain.Enums;

/// <summary>Güte einer Torchance in der ausführlichen Simulation (#431). Standardsituationen (Ecke, Freistoß)
/// sind keine eigene Art, sondern nur die Herkunft im Tortext.</summary>
public enum Chancenart
{
    Normal,
    Grosschance,
    Elfmeter
}
