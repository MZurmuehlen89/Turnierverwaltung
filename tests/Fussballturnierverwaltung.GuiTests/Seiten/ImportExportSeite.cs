using FlaUI.Core.AutomationElements;
using GuiTest.Framework;

namespace Fussballturnierverwaltung.GuiTests.Seiten;

/// <summary>Seitenobjekt „Import / Export“ (#537): Mannschaften als CSV exportieren und importieren, jeweils über
/// den Windows-Dateidialog.</summary>
public sealed class ImportExportSeite(Window fenster)
{
    // Import und Export laufen über alle Mannschaften des Seeds; der Import gleicht jede Zeile einzeln ab
    private static readonly TimeSpan _dateiVorgang = TimeSpan.FromSeconds(60);

    public string Status => fenster.TextOf("ImportExport.Status");
    public string Fehlermeldung => fenster.TextOf("ImportExport.Fehlermeldung");

    /// <summary>Exportiert alle Mannschaften nach <paramref name="pfad"/> und wartet auf die Bestätigung.</summary>
    public void MannschaftenExportieren(string pfad)
    {
        fenster.ActivateById("ImportExport.MannschaftenExportieren");
        FileDialog.WaitFor(fenster).Choose(pfad);
        WarteAufErgebnis("exportiert");
    }

    /// <summary>Importiert die Mannschaften aus <paramref name="pfad"/> und liefert die Statusmeldung.</summary>
    public string MannschaftenImportieren(string pfad)
    {
        fenster.ActivateById("ImportExport.MannschaftenImportieren");
        FileDialog.WaitFor(fenster).Choose(pfad);
        WarteAufErgebnis("importiert");
        return Status;
    }

    private void WarteAufErgebnis(string erfolgswort) =>
        Wait.Until(() => Status.Contains(erfolgswort, StringComparison.Ordinal) || Fehlermeldung.Length > 0,
            _dateiVorgang, $"Meldung „… {erfolgswort} …“ oder Fehler");
}
