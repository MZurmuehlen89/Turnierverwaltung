using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;

namespace Fussballturnierverwaltung.WinUi.Dialoge;

public class DialogService : IDialogService
{
    private XamlRoot? _xamlRoot;
    private Window? _fenster;

    public void XamlRootSetzen(XamlRoot root) => _xamlRoot = root;
    public void FensterSetzen(Window fenster) => _fenster = fenster;

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

    public async Task<bool> BestaetigenAsync(string titel, string nachricht)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = _xamlRoot,
            Title = titel,
            Content = nachricht,
            PrimaryButtonText = "Ja",
            CloseButtonText = "Abbrechen",
            DefaultButton = ContentDialogButton.Close,
        };

        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    public async Task<ContentDialogResult> AnzeigenAsync(ContentDialog dialog)
    {
        dialog.XamlRoot = _xamlRoot;
        return await dialog.ShowAsync();
    }

    public async Task<byte[]?> BilddateiWaehlenAsync()
    {
        var picker = new FileOpenPicker();
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(_fenster));
        picker.FileTypeFilter.Add(".png");
        picker.FileTypeFilter.Add(".jpg");
        picker.FileTypeFilter.Add(".jpeg");

        var datei = await picker.PickSingleFileAsync();
        if (datei is null) return null;

        using var stream = await datei.OpenStreamForReadAsync();
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);
        return ms.ToArray();
    }
}
