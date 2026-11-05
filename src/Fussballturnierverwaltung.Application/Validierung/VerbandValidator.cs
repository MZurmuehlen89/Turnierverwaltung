using Fussballturnierverwaltung.Application.Abstraktionen;
using Fussballturnierverwaltung.Application.Stammdaten.Eingaben;

namespace Fussballturnierverwaltung.Application.Validierung;

/// <summary>Feldbezug der Meldungen (#602) = Name der Eigenschaft in der Eingabe.</summary>
public class VerbandValidator
{
    public ValidationErgebnis Validieren(VerbandErstellenEingabe eingabe) =>
        Pruefen(eingabe.Name, eingabe.Kurzname, eingabe.Logo);

    // Bearbeiten prüft seit #602 auch die Höchstlänge – vorher fing erst die Datenbank einen zu langen Namen ab.
    public ValidationErgebnis Validieren(VerbandAktualisierenEingabe eingabe) =>
        Pruefen(eingabe.Name, eingabe.Kurzname, eingabe.Logo);

    private static ValidationErgebnis Pruefen(string name, string kurzname, byte[]? logo)
    {
        var fehler = new List<Feldfehler>();
        if (string.IsNullOrWhiteSpace(name))
            fehler.Add(new("Verbandsname darf nicht leer sein.", nameof(VerbandErstellenEingabe.Name)));
        else if (name.Trim().Length > 200)
            fehler.Add(new("Verbandsname darf maximal 200 Zeichen haben.", nameof(VerbandErstellenEingabe.Name)));
        if (string.IsNullOrWhiteSpace(kurzname))
            fehler.Add(new("Kurzname darf nicht leer sein.", nameof(VerbandErstellenEingabe.Kurzname)));
        BildValidierung.GroessePruefen(logo, nameof(VerbandErstellenEingabe.Logo), fehler);
        return ValidationErgebnis.Aus(fehler);
    }
}
