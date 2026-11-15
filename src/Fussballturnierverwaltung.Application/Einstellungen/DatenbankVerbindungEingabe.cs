using System.Globalization;

namespace Fussballturnierverwaltung.Application.Einstellungen;

/// <summary>Eingabe der Datenbankverbindung im Formular (#49): der Port als Text, wie er im Feld steht.</summary>
public record DatenbankVerbindungEingabe(string Server, string Port, string Datenbank, string Benutzer)
{
    public static DatenbankVerbindungEingabe Aus(DatenbankVerbindung verbindung) => new(
        verbindung.Server,
        verbindung.Port.ToString(CultureInfo.InvariantCulture),
        verbindung.Datenbank,
        verbindung.Benutzer);

    /// <summary>Unterscheidet sich die Eingabe von der gespeicherten Verbindung (Leerzeichen am Rand zählen nicht)?</summary>
    public bool WeichtAbVon(DatenbankVerbindung verbindung) =>
        Bereinigt() != Aus(verbindung).Bereinigt();

    /// <summary>Nur nach erfolgreicher Prüfung durch den <c>DatenbankVerbindungValidator</c> aufrufen.</summary>
    public DatenbankVerbindung AlsVerbindung()
    {
        var bereinigt = Bereinigt();
        return new DatenbankVerbindung
        {
            Server = bereinigt.Server,
            Port = int.Parse(bereinigt.Port, CultureInfo.InvariantCulture),
            Datenbank = bereinigt.Datenbank,
            Benutzer = bereinigt.Benutzer,
            KennwortVerschluesselt = null
        };
    }

    private DatenbankVerbindungEingabe Bereinigt() => new(Server.Trim(), Port.Trim(), Datenbank.Trim(), Benutzer.Trim());
}
