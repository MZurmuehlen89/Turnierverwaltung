using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Stammdaten;

/// <summary>Auslosungsfilter eines Turniers (Verband/Land/Mannschaftsart) als Wert: entscheidet, welche
/// Mannschaften für das Turnier in Frage kommen — gleiche Semantik wie
/// <c>IMannschaftRepository.AlleNachTurnierfilterAsync</c>. Die FIFA als Weltebene ist hier bereits aufgelöst:
/// für sie ist <paramref name="VerbandId"/> <c>null</c> ("weltweit", Issue #211).</summary>
public record Auslosungsfilter(int? VerbandId, int? LandId, MannschaftsartFilter Art)
{
    public static Auslosungsfilter Keiner { get; } = new(null, null, MannschaftsartFilter.Alle);

    public bool Passt(MannschaftAuswahl mannschaft) =>
        (VerbandId is null || mannschaft.VerbandId == VerbandId)
        && (LandId is null || mannschaft.LandId == LandId)
        && Art switch
        {
            MannschaftsartFilter.Verein             => mannschaft.Mannschaftsart == Mannschaftsart.Verein,
            MannschaftsartFilter.Nationalmannschaft => mannschaft.Mannschaftsart == Mannschaftsart.Nationalmannschaft,
            _                                       => true
        };
}
