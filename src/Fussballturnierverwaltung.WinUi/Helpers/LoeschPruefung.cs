using Fussballturnierverwaltung.Application.Diagnose;
using Microsoft.Extensions.Logging;

namespace Fussballturnierverwaltung.WinUi.Helpers;

internal static class LoeschPruefung
{
    /// <summary>Ermittelt, warum sich ein Datensatz nicht löschen lässt; null = löschbar. Ein Fehler der Abfrage
    /// kommt als Meldung zurück und sperrt ebenfalls (#559).</summary>
    public static async Task<string?> SperreErmittelnAsync(ILogger logger, Func<Task<string?>> sperreErmitteln)
    {
        // Als Vorgang, damit GUI-Tests erst nach der Abfrage weitermachen (Muster #597)
        using var vorgang = logger.Vorgang("Länder & Verbände: Löschen prüfen", "Löschen wird geprüft …");
        try
        {
            return await sperreErmitteln();
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }
}
