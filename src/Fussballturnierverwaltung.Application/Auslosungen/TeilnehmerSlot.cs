using Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;

namespace Fussballturnierverwaltung.Application.Auslosungen;

// TopfRang: 0 = erster Topf. Gruppe/Gruppenplatz: Herkunft aus einer vorangegangenen Gruppenphase — aus dem
// Platzhalternamen („1. Gruppe A“) oder bei echten Mannschaften und Qualifikanten aus deren Gruppen-Auslosung (#331, #489).
// VerbandId: Kontinentalverband des Landes — nur bei echten Mannschaften bekannt (#356).
// QualifikantId: die Mannschaft, die schon sicher hinter einem Platzhalter steht (#483).
internal record TeilnehmerSlot(
    int? MannschaftId, int? LandId, string? PlatzhalterName,
    int TopfRang = 0, int? SetzPosition = null, string? Gruppe = null, int? Gruppenplatz = null,
    int? VerbandId = null, int? QualifikantId = null)
{
    /// <summary>Die Mannschaft, die tatsächlich spielt — selbst gelost oder hinter dem Platzhalter.</summary>
    public int? SpielendeMannschaftId => MannschaftId ?? QualifikantId;

    public static TeilnehmerSlot AusEintrag(
        LostopfEintrag e, int topfRang, IReadOnlyDictionary<int, string> gruppeJeMannschaft, Qualifikanten qualifikanten)
    {
        if (e.MannschaftId is { } id)
            return new(id, e.Mannschaft?.LandId, null, topfRang, e.SetzPosition,
                gruppeJeMannschaft.GetValueOrDefault(id), VerbandId: e.Mannschaft?.Land?.VerbandId);

        // Ein Platzhalter bleibt Platzhalter (Kaskade, Gruppen-Herkunft) — Land und Verband kommen von der
        // Mannschaft, die schon sicher dahinter steht (#483). Ohne Gruppe im Namen („1. bester 3. Platz“) auch die
        // Gruppe (#489).
        var herkunft = GruppenHerkunft.AusPlatzhalter(e.PlatzhalterName);
        var qualifikant = qualifikanten.Fuer(e.PlatzhalterName);
        var gruppe = herkunft?.Gruppe ?? (qualifikant is null ? null : gruppeJeMannschaft.GetValueOrDefault(qualifikant.Id));
        return new(null, qualifikant?.LandId, e.PlatzhalterName, topfRang, e.SetzPosition, gruppe, herkunft?.Platz,
            qualifikant?.Land?.VerbandId, qualifikant?.Id);
    }
}
