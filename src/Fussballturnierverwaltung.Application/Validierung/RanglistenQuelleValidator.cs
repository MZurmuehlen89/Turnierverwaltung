using Fussballturnierverwaltung.Application.Abstraktionen;
using Fussballturnierverwaltung.Application.Ranglisten.Eingaben;

namespace Fussballturnierverwaltung.Application.Validierung;

public class RanglistenQuelleValidator
{
    public ValidationErgebnis Validieren(RanglistenQuelleErstellenEingabe eingabe)
    {
        var fehler = new List<string>();

        if (string.IsNullOrWhiteSpace(eingabe.Name))
            fehler.Add("Name darf nicht leer sein.");

        if (string.IsNullOrWhiteSpace(eingabe.Url))
            fehler.Add("URL darf nicht leer sein.");
        else if (!Uri.TryCreate(eingabe.Url, UriKind.Absolute, out var uri)
                 || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            fehler.Add("URL muss eine gültige http(s)-Adresse sein.");

        if (eingabe.TabellenIndex < 0)
            fehler.Add("Tabellenindex darf nicht negativ sein.");

        if (eingabe.MannschaftSpalte < 0)
            fehler.Add("Mannschaftsspalte darf nicht negativ sein.");

        if (eingabe.PunkteSpalte is < 0)
            fehler.Add("Punktespalte darf nicht negativ sein.");

        if (eingabe.PunkteSpalte is not null && eingabe.PunkteSpalte == eingabe.MannschaftSpalte)
            fehler.Add("Mannschaftsspalte und Punktespalte dürfen nicht identisch sein.");

        return fehler.Count == 0 ? ValidationErgebnis.Ok() : ValidationErgebnis.MitFehlern([.. fehler]);
    }

    public ValidationErgebnis Validieren(RanglistenQuelleAktualisierenEingabe eingabe)
    {
        var erstellen = new RanglistenQuelleErstellenEingabe(
            eingabe.Name, eingabe.Url, eingabe.TabellenIndex, eingabe.TabellenSelektor,
            eingabe.MannschaftSpalte, eingabe.PunkteSpalte, eingabe.KopfzeileUeberspringen);
        return Validieren(erstellen);
    }
}
