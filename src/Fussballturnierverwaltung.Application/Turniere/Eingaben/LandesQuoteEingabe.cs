namespace Fussballturnierverwaltung.Application.Turniere.Eingaben;

/// <summary>Eine Landes-Quoten-Zeile des Turnierkopfs. Mit <paramref name="Spielklasse"/> ein Kontingent (#572):
/// <paramref name="Anzahl"/> Plätze ab <paramref name="AbPlatz"/> (fehlt er, ab Platz 1) der Spielklasse.</summary>
public record LandesQuoteZeileEingabe(int LandId, int Anzahl, int? Spielklasse = null, int? AbPlatz = null)
{
    /// <summary>Startplatz wie gespeichert: ohne Spielklasse keiner, sonst mindestens Platz 1.</summary>
    public int? GespeicherterAbPlatz => Spielklasse is null ? null : AbPlatz ?? 1;
}
