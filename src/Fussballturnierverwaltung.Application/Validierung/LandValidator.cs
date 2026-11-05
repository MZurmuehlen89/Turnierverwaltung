using Fussballturnierverwaltung.Application.Abstraktionen;
using Fussballturnierverwaltung.Application.Stammdaten.Eingaben;

namespace Fussballturnierverwaltung.Application.Validierung;

/// <summary>Feldbezug der Meldungen (#602) = Name der Eigenschaft in der Eingabe.</summary>
public class LandValidator
{
    public ValidationErgebnis Validieren(LandErstellenEingabe eingabe) =>
        Pruefen(eingabe.Name, eingabe.Kurzname, eingabe.FifaCode, eingabe.VerbandId, eingabe.Flagge);

    public ValidationErgebnis Validieren(LandAktualisierenEingabe eingabe) =>
        Pruefen(eingabe.Name, eingabe.Kurzname, eingabe.FifaCode, eingabe.VerbandId, eingabe.Flagge);

    private static ValidationErgebnis Pruefen(string name, string kurzname, string fifaCode, int verbandId, byte[]? flagge)
    {
        var fehler = new List<Feldfehler>();

        if (string.IsNullOrWhiteSpace(name))
            fehler.Add(new("Ländername darf nicht leer sein.", nameof(LandErstellenEingabe.Name)));
        else if (name.Trim().Length > 200)
            fehler.Add(new("Ländername darf maximal 200 Zeichen haben.", nameof(LandErstellenEingabe.Name)));

        if (string.IsNullOrWhiteSpace(kurzname))
            fehler.Add(new("Kurzname darf nicht leer sein.", nameof(LandErstellenEingabe.Kurzname)));

        if (string.IsNullOrWhiteSpace(fifaCode))
            fehler.Add(new("FIFA-Code darf nicht leer sein.", nameof(LandErstellenEingabe.FifaCode)));
        else if (fifaCode.Trim().Length > 3)
            fehler.Add(new("FIFA-Code darf maximal 3 Zeichen haben.", nameof(LandErstellenEingabe.FifaCode)));

        if (verbandId <= 0)
            fehler.Add(new("Es muss ein Verband ausgewählt werden.", nameof(LandErstellenEingabe.VerbandId)));

        BildValidierung.GroessePruefen(flagge, nameof(LandErstellenEingabe.Flagge), fehler);
        return ValidationErgebnis.Aus(fehler);
    }
}
