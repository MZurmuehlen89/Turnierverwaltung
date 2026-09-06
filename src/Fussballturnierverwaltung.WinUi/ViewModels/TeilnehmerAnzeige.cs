using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>Darstellungstyp für einen Auslosungs-/Vorbelegungs-Teilnehmer — echte Mannschaft oder
/// Platzhalter. 1:1 aus Wpf/ViewModels/AuslosungViewModel.cs portiert (reine Domain-Konsumenten-Logik ohne
/// WPF-Bezug, eigene Kopie statt Projektreferenz, da WinUi und Wpf unabhängige Startprojekte bleiben
/// müssen, siehe WinUi-CLAUDE.md).</summary>
public record TeilnehmerAnzeige(int? MannschaftId, string Name, string? Kuerzel, int? LandId = null, byte[]? Wappen = null)
{
    public bool IstPlatzhalter => !MannschaftId.HasValue;

    public static TeilnehmerAnzeige AusMannschaft(Mannschaft m) =>
        new(m.Id, m.Name, m.Land?.Kurzname, LandId: m.LandId, Wappen: MannschaftsIcon(m));

    public static TeilnehmerAnzeige AusPlatzhalter(string name) =>
        new(null, name, null);

    // Issue #230, analog #229: Nationalmannschaften zeigen die Landesflagge statt des
    // Mannschafts-Wappens, das eigene Wappen bleibt in der DB erhalten.
    internal static byte[]? MannschaftsIcon(Mannschaft? mannschaft)
        => mannschaft?.Mannschaftsart == Mannschaftsart.Nationalmannschaft
            ? mannschaft.Land?.Flagge
            : mannschaft?.Wappen;
}
