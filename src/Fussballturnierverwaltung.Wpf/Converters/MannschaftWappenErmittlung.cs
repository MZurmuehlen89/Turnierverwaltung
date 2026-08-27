using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Wpf.Converters;

/// <summary>Gemeinsame Wappen-Auflösung für die Mannschaft-Converter (Issue #232) — Nationalmannschaften
/// zeigen die Landesflagge statt des Mannschafts-Wappens, analog der Wappen-Logik in den ViewModels
/// (z. B. SpielplanViewModel.MannschaftsIcon).</summary>
internal static class MannschaftWappenErmittlung
{
    public static byte[]? Bytes(Mannschaft? mannschaft)
        => mannschaft?.Mannschaftsart == Mannschaftsart.Nationalmannschaft
            ? mannschaft.Land?.Flagge
            : mannschaft?.Wappen;
}
