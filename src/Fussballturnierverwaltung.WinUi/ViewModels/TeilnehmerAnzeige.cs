using Fussballturnierverwaltung.Application.Auslosungen;
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
/// Aufrufer, weil die Mannschaftsabfragen den Verband (samt Logo) nicht mitladen.
/// <see cref="QualifikantId"/>/<see cref="QualifikantName"/>: die Mannschaft, die hinter einem Platzhalter schon sicher
/// feststeht (#483). Der Teilnehmer bleibt dabei Platzhalter — <see cref="Name"/> ist weiter der Platzhaltername, unter
/// dem er in Lostöpfe gelegt wird —, angezeigt, gefiltert und geprüft wird aber die Mannschaft.</summary>
public record TeilnehmerAnzeige(int? MannschaftId, string Name, string? Kuerzel, int EintragId = 0, int? LandId = null, byte[]? Wappen = null,
                                string? LandName = null, int? VerbandId = null, string? VerbandName = null,
                                int? QualifikantId = null, string? QualifikantName = null)
{
    public bool IstPlatzhalter => !MannschaftId.HasValue;

    /// <summary>Die Mannschaft — eigene oder die hinter dem Platzhalter —, sonst der Platzhaltername.</summary>
    public string AnzeigeName => QualifikantName ?? Name;

    /// <summary>Der Platzhalter als Zusatz hinter der Mannschaft, die ihn besetzt („ · 1. Gruppe A“) — leer, solange
    /// er offen ist oder es keinen gibt.</summary>
    public string HerkunftZusatz => QualifikantName is null ? "" : $"  ·  {Name}";

    /// <summary>Die Mannschaft, die tatsächlich spielt: die eigene oder die hinter dem Platzhalter.</summary>
    public int? SpielendeMannschaftId => MannschaftId ?? QualifikantId;

    /// <summary>Ergänzt einen Platzhalter um die Mannschaft, die ihn sicher besetzt; sonst unverändert.</summary>
    public TeilnehmerAnzeige MitQualifikant(Mannschaft? qualifikant, string? verbandName = null) =>
        qualifikant is null || MannschaftId.HasValue
            ? this
            : this with
            {
                QualifikantId = qualifikant.Id, QualifikantName = qualifikant.Name, Kuerzel = qualifikant.Land?.Kurzname,
                LandId = qualifikant.LandId, Wappen = MannschaftsIcon.Fuer(qualifikant), LandName = qualifikant.Land?.Name,
                VerbandId = qualifikant.Land?.VerbandId, VerbandName = verbandName,
            };

    public static TeilnehmerAnzeige AusMannschaft(Mannschaft m) =>
        new(m.Id, m.Name, m.Land?.Kurzname, LandId: m.LandId, Wappen: MannschaftsIcon.Fuer(m), LandName: m.Land?.Name,
            VerbandId: m.Land?.VerbandId);

    public static TeilnehmerAnzeige AusPlatzhalter(string name) =>
        new(null, name, null);

    public static TeilnehmerAnzeige AusEintrag(LostopfEintrag e) =>
        e.MannschaftId.HasValue
            ? new(e.MannschaftId, e.Mannschaft?.Name ?? "?", e.Mannschaft?.Land?.Kurzname, e.Id, e.Mannschaft?.LandId, MannschaftsIcon.Fuer(e.Mannschaft))
            : new(null, e.PlatzhalterName ?? "?", null, e.Id);

    public static TeilnehmerAnzeige AusEintrag(LostopfEintrag e, Qualifikanten qualifikanten) =>
        AusEintrag(e).MitQualifikant(qualifikanten.Fuer(e.PlatzhalterName));
}
