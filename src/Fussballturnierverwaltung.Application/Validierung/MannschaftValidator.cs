using Fussballturnierverwaltung.Application.Abstraktionen;
using Fussballturnierverwaltung.Application.Stammdaten.Eingaben;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;

namespace Fussballturnierverwaltung.Application.Validierung;

public class MannschaftValidator
{
    public ValidationErgebnis Validieren(MannschaftErstellenEingabe eingabe)
    {
        var fehler = new List<string>();

        if (string.IsNullOrWhiteSpace(eingabe.Name))
            fehler.Add("Mannschaftsname darf nicht leer sein.");
        else if (eingabe.Name.Trim().Length > 200)
            fehler.Add("Mannschaftsname darf maximal 200 Zeichen haben.");

        if (string.IsNullOrWhiteSpace(eingabe.Kurzname))
            fehler.Add("Kürzel darf nicht leer sein.");

        RatingPruefen("Offensiv-Rating",    eingabe.OffensivRating,   fehler);
        RatingPruefen("Mittelfeld-Rating",  eingabe.MittelfeldRating, fehler);
        RatingPruefen("Defensiv-Rating",    eingabe.DefensivRating,   fehler);
        RatingPruefen("Heimstärke",         eingabe.Heimstaerke,      fehler);
        RatingPruefen("Auswärtsstärke",     eingabe.Auswaertsstaerke, fehler);

        if (eingabe.LandId <= 0)
            fehler.Add("Es muss ein Land ausgewählt werden.");

        return Ergebnis(fehler);
    }

    public ValidationErgebnis Validieren(MannschaftAktualisierenEingabe eingabe)
    {
        var fehler = new List<string>();

        if (string.IsNullOrWhiteSpace(eingabe.Name))
            fehler.Add("Mannschaftsname darf nicht leer sein.");

        if (string.IsNullOrWhiteSpace(eingabe.Kurzname))
            fehler.Add("Kürzel darf nicht leer sein.");

        RatingPruefen("Offensiv-Rating",   eingabe.OffensivRating,   fehler);
        RatingPruefen("Mittelfeld-Rating", eingabe.MittelfeldRating, fehler);
        RatingPruefen("Defensiv-Rating",   eingabe.DefensivRating,   fehler);
        RatingPruefen("Heimstärke",        eingabe.Heimstaerke,      fehler);
        RatingPruefen("Auswärtsstärke",    eingabe.Auswaertsstaerke, fehler);

        return Ergebnis(fehler);
    }

    /// <summary>Validiert eine bereits persistierte Mannschaft auf Konsistenz.</summary>
    public ValidationErgebnis Validieren(Mannschaft mannschaft)
    {
        var fehler = new List<string>();

        RatingPruefen("Offensiv-Rating",   mannschaft.OffensivRating,   fehler);
        RatingPruefen("Mittelfeld-Rating", mannschaft.MittelfeldRating, fehler);
        RatingPruefen("Defensiv-Rating",   mannschaft.DefensivRating,   fehler);
        RatingPruefen("Heimstärke",        mannschaft.Heimstaerke,      fehler);
        RatingPruefen("Auswärtsstärke",    mannschaft.Auswaertsstaerke, fehler);

        return Ergebnis(fehler);
    }

    internal static void RatingPruefen(string bezeichnung, int wert, List<string> fehler)
    {
        if (wert < 1 || wert > 100)
            fehler.Add($"{bezeichnung} muss zwischen 1 und 100 liegen (war: {wert}).");
    }

    private static ValidationErgebnis Ergebnis(List<string> fehler) =>
        fehler.Count == 0 ? ValidationErgebnis.Ok() : ValidationErgebnis.MitFehlern([.. fehler]);
}
