using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Fussballturnierverwaltung.WinUi.Dialoge;

public class DialogService : IDialogService
{
    private XamlRoot? _xamlRoot;

    public void XamlRootSetzen(XamlRoot root) => _xamlRoot = root;

    public async Task<UngespeichertAntwort> UngespeicherteAenderungenAbfragenAsync()
    {
        var dialog = new ContentDialog
        {
            XamlRoot = _xamlRoot,
            Title = "Ungespeicherte Änderungen",
            Content = "Es gibt ungespeicherte Änderungen. Speichern, Verwerfen oder Abbrechen?",
            PrimaryButtonText = "Speichern",
            SecondaryButtonText = "Verwerfen",
            CloseButtonText = "Abbrechen",
            DefaultButton = ContentDialogButton.Primary,
        };

        var ergebnis = await dialog.ShowAsync();
        return ergebnis switch
        {
            ContentDialogResult.Primary => UngespeichertAntwort.Speichern,
            ContentDialogResult.Secondary => UngespeichertAntwort.Verwerfen,
            _ => UngespeichertAntwort.Abbrechen,
        };
    }
}
