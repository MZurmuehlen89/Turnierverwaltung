namespace Fussballturnierverwaltung.Application.Einstellungen;

/// <summary>Verbindung zur PostgreSQL-Datenbank (#49), einstellbar auf der Seite „Datenbankwartung“ und im
/// Startfenster bei einem Verbindungsfehler. Die Vorgaben passen zu <c>build/PostgreSQL-Einrichten.ps1</c>.</summary>
public class DatenbankVerbindung
{
    public string Server { get; set; } = "localhost";
    public int Port { get; set; } = 5432;
    public string Datenbank { get; set; } = "turnierverwaltung";
    public string Benutzer { get; set; } = "turnierverwaltung";

    /// <summary>Kennwort, für den Windows-Benutzer verschlüsselt (Base64). <c>null</c>: keins gespeichert – dann sucht
    /// PostgreSQL es in seiner Kennwortdatei (pgpass.conf), wo das Einrichtungsskript es ablegt.</summary>
    public string? KennwortVerschluesselt { get; set; }
}
