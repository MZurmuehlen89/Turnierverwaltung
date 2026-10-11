using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Stammdaten;

/// <summary>Das Bild, mit dem eine Mannschaft angezeigt wird (#229/#230/#232): Nationalmannschaften zeigen die
/// Landesflagge statt des Mannschaftswappens, das eigene Wappen bleibt in der Datenbank erhalten. Die Flagge kommt
/// aus <c>Mannschaft.Land</c> — ist das Land nicht mitgeladen, bleibt eine Nationalmannschaft ohne Bild.</summary>
public static class MannschaftsIcon
{
    public static byte[]? Fuer(Mannschaft? mannschaft)
        => mannschaft?.Mannschaftsart == Mannschaftsart.Nationalmannschaft
            ? mannschaft.Land?.Flagge
            : mannschaft?.Wappen;
}
