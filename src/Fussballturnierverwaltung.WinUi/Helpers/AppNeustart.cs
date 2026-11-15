using Microsoft.Windows.AppLifecycle;

namespace Fussballturnierverwaltung.WinUi.Helpers;

/// <summary>Startet die App neu, etwa nach dem Speichern einer neuen Datenbankverbindung (#49): ein Wechsel im
/// laufenden Betrieb ließe offene Seiten mit Daten der alten Datenbank stehen.</summary>
public static class AppNeustart
{
    /// <returns>Den Grund, wenn der Neustart nicht ausgelöst werden konnte; bei Erfolg kehrt der Aufruf nicht zurück.</returns>
    public static string Ausfuehren()
    {
        var grund = AppInstance.Restart("");
        return $"Das Programm konnte nicht neu gestartet werden ({grund}). Bitte von Hand neu starten – die Verbindung ist gespeichert.";
    }
}
