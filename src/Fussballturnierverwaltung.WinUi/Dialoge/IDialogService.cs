using Microsoft.UI.Xaml;

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
}
