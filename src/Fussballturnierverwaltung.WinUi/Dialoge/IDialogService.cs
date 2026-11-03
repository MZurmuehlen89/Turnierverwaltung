using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Fussballturnierverwaltung.WinUi.Dialoge;

public enum UngespeichertAntwort
{
    Speichern,
    Verwerfen,
    Abbrechen,
}

/// <summary>
/// Ersatz für WPFs <c>MessageBox.Show</c> — WinUI 3 kennt keine synchrone MessageBox mehr,
/// nur den asynchronen <see cref="Microsoft.UI.Xaml.Controls.ContentDialog"/>, der einen
/// <see cref="XamlRoot"/> benötigt. Der NavigationService kennt kein Fenster, daher hält
/// dieser Singleton den zuletzt aktiven XamlRoot (vom MainWindow beim Start gesetzt).
/// </summary>
public interface IDialogService
{
    void XamlRootSetzen(XamlRoot root);

    /// <summary>Vom MainWindow beim Start gesetzt — nötig für <see cref="BilddateiWaehlenAsync"/>,
    /// da FileOpenPicker unter WinUI 3 Desktop ein Fenster-Handle (HWND) braucht.</summary>
    void FensterSetzen(Window fenster);

    Task<UngespeichertAntwort> UngespeicherteAenderungenAbfragenAsync();

    Task<bool> BestaetigenAsync(string titel, string nachricht);

    /// <summary>Öffnet den Datei-Auswahldialog für ein Bild (png/jpg) und liefert dessen Rohdaten,
    /// oder null bei Abbruch.</summary>
    Task<byte[]?> BilddateiWaehlenAsync();

    /// <summary>Öffnet den Datei-Auswahldialog für eine CSV-Datei und liefert deren vollen Pfad,
    /// oder null bei Abbruch (#290, Import/Export).</summary>
    Task<string?> CsvDateiOeffnenAsync();

    /// <summary>Öffnet den Speicherort-Dialog für eine Datei mit Vorschlagsnamen und liefert den
    /// vollen Pfad, oder null bei Abbruch (#290, Import/Export). WinUI 3 kennt anders als WPFs
    /// SaveFileDialog kein InitialDirectory für einen beliebigen Pfad — der Nutzer landet im
    /// Standardordner (Dokumente) und muss ggf. selbst navigieren.</summary>
    Task<string?> DateiSpeichernUnterAsync(string vorschlagsname, string erweiterungOhnePunkt, string dateiTypAnzeige);

    /// <summary>Setzt den aktuellen XamlRoot auf den übergebenen, selbst erstellten ContentDialog
    /// (#285) und zeigt ihn an — ViewModels bauen ihre eigenen, fachspezifischen ContentDialogs, kennen aber keinen
    /// XamlRoot; das übernimmt zentral dieser Service (siehe Klassenkommentar oben).</summary>
    Task<ContentDialogResult> AnzeigenAsync(ContentDialog dialog);
}
