using System.Globalization;
using Fussballturnierverwaltung.Application.Abstraktionen;
using Fussballturnierverwaltung.Application.Einstellungen;

namespace Fussballturnierverwaltung.Application.Validierung;

/// <summary>Prüft die Datenbankverbindung vor dem Verbindungsaufbau (#49). Feldbezug = Name der Eigenschaft.</summary>
public class DatenbankVerbindungValidator
{
    public ValidationErgebnis Validieren(DatenbankVerbindungEingabe eingabe)
    {
        var fehler = new List<Feldfehler>();
        if (string.IsNullOrWhiteSpace(eingabe.Server))
            fehler.Add(new("Server darf nicht leer sein.", nameof(DatenbankVerbindungEingabe.Server)));
        if (!int.TryParse(eingabe.Port.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var port)
            || port is < 1 or > 65535)
            fehler.Add(new("Port muss eine Zahl von 1 bis 65535 sein (Standard 5432).", nameof(DatenbankVerbindungEingabe.Port)));
        if (string.IsNullOrWhiteSpace(eingabe.Datenbank))
            fehler.Add(new("Datenbank darf nicht leer sein.", nameof(DatenbankVerbindungEingabe.Datenbank)));
        if (string.IsNullOrWhiteSpace(eingabe.Benutzer))
            fehler.Add(new("Benutzer darf nicht leer sein.", nameof(DatenbankVerbindungEingabe.Benutzer)));
        return ValidationErgebnis.Aus(fehler);
    }
}
