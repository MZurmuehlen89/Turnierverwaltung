using Fussballturnierverwaltung.Application.Turniere.Eingaben;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;

namespace Fussballturnierverwaltung.Application.Turniere;

/// <summary>Was eine Landes-Quoten-Zeile im Turnierkopf anbieten darf (#572), gemessen an den übrigen Zeilen – damit
/// die Oberfläche gar nicht erst widersprüchliche Kontingente zulässt: Eine Zeile für das ganze Land steht allein, ein
/// Land ohne Ligapyramide hat höchstens eine Zeile, ein Land höchstens einen Pokalsieger (#583). Zustandslos.</summary>
public static class LandesQuotenAuswahl
{
    public static bool HatPyramide(Land land) => land.Rangliste is { Abschnitte.Count: > 0 };

    /// <summary>Ob <paramref name="land"/> neben den <paramref name="andereZeilen"/> gewählt werden kann: das Land
    /// hat noch keine Zeile, oder es hat eine Pyramide und keine seiner Zeilen gilt für das ganze Land.</summary>
    public static bool LandWaehlbar(Land land, IEnumerable<LandesQuoteZeileEingabe> andereZeilen)
    {
        var gleiche = andereZeilen.Where(z => z.LandId == land.Id).ToList();
        return gleiche.Count == 0 || (HatPyramide(land) && gleiche.All(z => !z.GanzesLand));
    }

    /// <summary>„Ganzes Land“ (keine Spielklasse) geht nur, solange das Land keine weitere Zeile hat.</summary>
    public static bool GanzesLandWaehlbar(int landId, IEnumerable<LandesQuoteZeileEingabe> andereZeilen) =>
        andereZeilen.All(z => z.LandId != landId);

    /// <summary>„Pokalsieger“ geht nur, solange das Land noch kein Pokalsieger-Kontingent hat (#583).</summary>
    public static bool PokalsiegerWaehlbar(int landId, IEnumerable<LandesQuoteZeileEingabe> andereZeilen) =>
        andereZeilen.All(z => z.LandId != landId || !z.Pokalsieger);

    /// <summary>Der erste Platz hinter den Kontingenten der übrigen Zeilen in derselben Spielklasse, sonst Platz 1 –
    /// Vorschlag, wenn eine Zeile ihre Spielklasse bekommt (CL ab Platz 1, EL dahinter).</summary>
    public static int NaechsterFreierPlatz(int landId, int spielklasse, IEnumerable<LandesQuoteZeileEingabe> andereZeilen) =>
        andereZeilen
            .Where(z => z.LandId == landId && z.Spielklasse == spielklasse)
            .Select(z => z.GespeicherterAbPlatz!.Value + z.Anzahl)
            .DefaultIfEmpty(1)
            .Max();
}
