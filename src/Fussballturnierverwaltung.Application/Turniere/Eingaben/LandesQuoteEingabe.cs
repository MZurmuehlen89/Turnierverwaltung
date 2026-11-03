namespace Fussballturnierverwaltung.Application.Turniere.Eingaben;

/// <summary>Eine Landes-Quoten-Zeile des Turnierkopfs. Mit <paramref name="Spielklasse"/> ein Kontingent (#572):
/// <paramref name="Anzahl"/> Plätze ab <paramref name="AbPlatz"/> (fehlt er, ab Platz 1) der Spielklasse. Mit
/// <paramref name="Pokalsieger"/> der Platz des Pokalsiegers (#583), ohne Spielklasse; <paramref name="AbPlatz"/> ist
/// dann „Nachrücken ab“ in der obersten Spielklasse.</summary>
public record LandesQuoteZeileEingabe(int LandId, int Anzahl, int? Spielklasse = null, int? AbPlatz = null, bool Pokalsieger = false)
{
    /// <summary>Für das ganze Land: weder Spielklasse noch Pokalsieger.</summary>
    public bool GanzesLand => Spielklasse is null && !Pokalsieger;

    /// <summary>Startplatz wie gespeichert: für das ganze Land keiner, sonst mindestens Platz 1.</summary>
    public int? GespeicherterAbPlatz => GanzesLand ? null : AbPlatz ?? 1;
}
