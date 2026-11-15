using Microsoft.Windows.AppLifecycle;

namespace Fussballturnierverwaltung.WinUi.Helpers;

/// <summary>Startet die App neu, etwa nach dem Speichern einer neuen Datenbankverbindung (#49) oder dem Wiederherstellen
/// einer Sicherung (#43): Ein Wechsel im laufenden Betrieb ließe offene Seiten mit Daten der alten Datenbank stehen.</summary>
public static class AppNeustart
{
    /// <returns>Den Grund, wenn der Neustart nicht ausgelöst werden konnte; bei Erfolg kehrt der Aufruf nicht zurück.</returns>
    /// <param name="erledigt">Was vor dem Neustart schon geschehen ist, für die Meldung bei einem Fehlschlag.</param>
    public static string Ausfuehren(string erledigt)
    {
        var grund = AppInstance.Restart("");
        return $"Das Programm konnte nicht neu gestartet werden ({grund}). Bitte von Hand neu starten – {erledigt}";
    }
}
