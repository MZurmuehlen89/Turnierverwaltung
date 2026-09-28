using Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>Darstellungstyp für einen Auslosungs-/Vorbelegungs-Teilnehmer — echte Mannschaft oder
/// Platzhalter. 1:1 aus Wpf/ViewModels/AuslosungViewModel.cs portiert (reine Domain-Konsumenten-Logik ohne
/// WPF-Bezug, eigene Kopie statt Projektreferenz, da WinUi und Wpf unabhängige Startprojekte bleiben
/// müssen, siehe WinUi-CLAUDE.md). <see cref="EintragId"/> (Default 0, ungenutzt von Vorbelegung) identifiziert
/// den zugehörigen <see cref="LostopfEintrag"/> — nötig, damit Auslosung (#300) gezielt einen Eintrag aus
/// einem Lostopf entfernen kann, ohne nach Name/MannschaftId zu suchen (mehrere Platzhalter könnten gleich
/// heißen). <see cref="LandName"/> speist den Länderfilter der verfügbaren Teams in der Auslosung (#344).</summary>
public record TeilnehmerAnzeige(int? MannschaftId, string Name, string? Kuerzel, int EintragId = 0, int? LandId = null, byte[]? Wappen = null,
                                string? LandName = null)
{
    public bool IstPlatzhalter => !MannschaftId.HasValue;

    public static TeilnehmerAnzeige AusMannschaft(Mannschaft m) =>
        new(m.Id, m.Name, m.Land?.Kurzname, LandId: m.LandId, Wappen: MannschaftsIcon(m), LandName: m.Land?.Name);

    public static TeilnehmerAnzeige AusPlatzhalter(string name) =>
        new(null, name, null);

    public static TeilnehmerAnzeige AusEintrag(LostopfEintrag e) =>
        e.MannschaftId.HasValue
            ? new(e.MannschaftId, e.Mannschaft?.Name ?? "?", e.Mannschaft?.Land?.Kurzname, e.Id, e.Mannschaft?.LandId, MannschaftsIcon(e.Mannschaft))
            : new(null, e.PlatzhalterName ?? "?", null, e.Id);

    // Issue #230, analog #229: Nationalmannschaften zeigen die Landesflagge statt des
    // Mannschafts-Wappens, das eigene Wappen bleibt in der DB erhalten.
    public static byte[]? MannschaftsIcon(Mannschaft? mannschaft)
        => mannschaft?.Mannschaftsart == Mannschaftsart.Nationalmannschaft
            ? mannschaft.Land?.Flagge
            : mannschaft?.Wappen;
}
