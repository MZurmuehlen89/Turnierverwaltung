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

    Task<UngespeichertAntwort> UngespeicherteAenderungenAbfragenAsync();
}
