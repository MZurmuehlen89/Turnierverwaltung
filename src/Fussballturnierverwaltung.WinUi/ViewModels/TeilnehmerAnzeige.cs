using Fussballturnierverwaltung.Application.Stammdaten;
using Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>Darstellungstyp für einen Auslosungs-/Vorbelegungs-Teilnehmer — echte Mannschaft oder
/// Platzhalter. 1:1 aus Wpf/ViewModels/AuslosungViewModel.cs portiert (reine Domain-Konsumenten-Logik ohne
/// WPF-Bezug, eigene Kopie statt Projektreferenz, da WinUi und Wpf unabhängige Startprojekte bleiben
/// müssen, siehe WinUi-CLAUDE.md). <see cref="EintragId"/> (Default 0, ungenutzt von Vorbelegung) identifiziert
/// den zugehörigen <see cref="LostopfEintrag"/> — nötig, damit Auslosung (#300) gezielt einen Eintrag aus
/// einem Lostopf entfernen kann, ohne nach Name/MannschaftId zu suchen (mehrere Platzhalter könnten gleich
/// heißen). <see cref="LandName"/> speist den Länderfilter der verfügbaren Teams in der Auslosung (#344),
/// <see cref="VerbandId"/>/<see cref="VerbandName"/> den Filter nach Kontinentalverband (#421) — der Name kommt vom
/// Aufrufer, weil die Mannschaftsabfragen den Verband (samt Logo) nicht mitladen.</summary>
public record TeilnehmerAnzeige(int? MannschaftId, string Name, string? Kuerzel, int EintragId = 0, int? LandId = null, byte[]? Wappen = null,
                                string? LandName = null, int? VerbandId = null, string? VerbandName = null)
{
    public bool IstPlatzhalter => !MannschaftId.HasValue;

    public static TeilnehmerAnzeige AusMannschaft(Mannschaft m) =>
        new(m.Id, m.Name, m.Land?.Kurzname, LandId: m.LandId, Wappen: MannschaftsIcon.Fuer(m), LandName: m.Land?.Name,
            VerbandId: m.Land?.VerbandId);

    public static TeilnehmerAnzeige AusPlatzhalter(string name) =>
        new(null, name, null);

    public static TeilnehmerAnzeige AusEintrag(LostopfEintrag e) =>
        e.MannschaftId.HasValue
            ? new(e.MannschaftId, e.Mannschaft?.Name ?? "?", e.Mannschaft?.Land?.Kurzname, e.Id, e.Mannschaft?.LandId, MannschaftsIcon.Fuer(e.Mannschaft))
            : new(null, e.PlatzhalterName ?? "?", null, e.Id);
}
