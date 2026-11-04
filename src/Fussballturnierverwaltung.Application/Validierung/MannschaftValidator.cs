using Fussballturnierverwaltung.Application.Abstraktionen;
using Fussballturnierverwaltung.Application.Stammdaten.Eingaben;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;

namespace Fussballturnierverwaltung.Application.Validierung;

/// <summary>Feldbezug der Meldungen (#595) = Name der Eigenschaft in der Eingabe.</summary>
public class MannschaftValidator
{
    public ValidationErgebnis Validieren(MannschaftErstellenEingabe eingabe)
    {
        var fehler = new List<Feldfehler>();

        if (string.IsNullOrWhiteSpace(eingabe.Name))
            fehler.Add(new("Mannschaftsname darf nicht leer sein.", nameof(eingabe.Name)));
        else if (eingabe.Name.Trim().Length > 200)
            fehler.Add(new("Mannschaftsname darf maximal 200 Zeichen haben.", nameof(eingabe.Name)));

        KurznamePruefen(eingabe.Kurzname, fehler);
        RatingsPruefen(eingabe.OffensivRating, eingabe.MittelfeldRating, eingabe.DefensivRating,
            eingabe.Heimstaerke, eingabe.Auswaertsstaerke, fehler);

        if (eingabe.LandId <= 0)
            fehler.Add(new("Es muss ein Land ausgewählt werden.", nameof(eingabe.LandId)));

        BildValidierung.GroessePruefen(eingabe.Wappen, nameof(eingabe.Wappen), fehler);

        return ValidationErgebnis.Aus(fehler);
    }

    public ValidationErgebnis Validieren(MannschaftAktualisierenEingabe eingabe)
    {
        var fehler = new List<Feldfehler>();

        if (string.IsNullOrWhiteSpace(eingabe.Name))
            fehler.Add(new("Mannschaftsname darf nicht leer sein.", nameof(eingabe.Name)));

        KurznamePruefen(eingabe.Kurzname, fehler);
        RatingsPruefen(eingabe.OffensivRating, eingabe.MittelfeldRating, eingabe.DefensivRating,
            eingabe.Heimstaerke, eingabe.Auswaertsstaerke, fehler);

        BildValidierung.GroessePruefen(eingabe.Wappen, nameof(eingabe.Wappen), fehler);

        return ValidationErgebnis.Aus(fehler);
    }

    /// <summary>Validiert eine bereits persistierte Mannschaft auf Konsistenz.</summary>
    public ValidationErgebnis Validieren(Mannschaft mannschaft)
    {
        var fehler = new List<Feldfehler>();
        RatingsPruefen(mannschaft.OffensivRating, mannschaft.MittelfeldRating, mannschaft.DefensivRating,
            mannschaft.Heimstaerke, mannschaft.Auswaertsstaerke, fehler);
        return ValidationErgebnis.Aus(fehler);
    }

    private static void KurznamePruefen(string kurzname, List<Feldfehler> fehler)
    {
        if (string.IsNullOrWhiteSpace(kurzname))
            fehler.Add(new("Kürzel darf nicht leer sein.", nameof(MannschaftErstellenEingabe.Kurzname)));
    }

#pragma warning disable S107 // die fünf Ratings einer Mannschaft, Eingaben und Entität haben keinen gemeinsamen Typ
    private static void RatingsPruefen(
        int offensiv, int mittelfeld, int defensiv, int heim, int auswaerts, List<Feldfehler> fehler)
#pragma warning restore S107
    {
        RatingPruefen("Offensiv-Rating",   nameof(Mannschaft.OffensivRating),   offensiv,   fehler);
        RatingPruefen("Mittelfeld-Rating", nameof(Mannschaft.MittelfeldRating), mittelfeld, fehler);
        RatingPruefen("Defensiv-Rating",   nameof(Mannschaft.DefensivRating),   defensiv,   fehler);
        RatingPruefen("Heimstärke",        nameof(Mannschaft.Heimstaerke),      heim,       fehler);
        RatingPruefen("Auswärtsstärke",    nameof(Mannschaft.Auswaertsstaerke), auswaerts,  fehler);
    }

    private static void RatingPruefen(string bezeichnung, string feld, int wert, List<Feldfehler> fehler)
    {
        if (wert < 1 || wert > 100)
            fehler.Add(new($"{bezeichnung} muss zwischen 1 und 100 liegen (war: {wert}).", feld));
    }
}
