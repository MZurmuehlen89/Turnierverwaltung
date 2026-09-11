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
            Title = "Ungespeicherte Änderungen",
            Content = "Es gibt ungespeicherte Änderungen. Speichern, Verwerfen oder Abbrechen?",
            PrimaryButtonText = "Speichern",
            SecondaryButtonText = "Verwerfen",
            CloseButtonText = "Abbrechen",
            DefaultButton = ContentDialogButton.Primary,
        };

        var ergebnis = await AnzeigenAsync(dialog);
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
            Title = titel,
            Content = nachricht,
            PrimaryButtonText = "Ja",
            CloseButtonText = "Abbrechen",
            DefaultButton = ContentDialogButton.Close,
        };

        return await AnzeigenAsync(dialog) == ContentDialogResult.Primary;
    }

    public async Task<ContentDialogResult> AnzeigenAsync(ContentDialog dialog)
    {
        dialog.XamlRoot = _xamlRoot;
        // Popups (ContentDialog eingeschlossen) hängen nicht im Visual Tree unterhalb von RootGrid und
        // erben dessen RequestedTheme deshalb nicht automatisch — ohne diese explizite Übernahme bleiben
        // ContentDialogs immer im (System-)Standard-Theme, unabhängig vom App-internen Light/Dark-Toggle
        // (Fund aus #285-Test).
        if (_xamlRoot?.Content is FrameworkElement root)
            dialog.RequestedTheme = root.ActualTheme;
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

    public async Task<string?> CsvDateiOeffnenAsync()
    {
        var picker = new FileOpenPicker();
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(_fenster));
        picker.FileTypeFilter.Add(".csv");

        var datei = await picker.PickSingleFileAsync();
        return datei?.Path;
    }

    public async Task<string?> DateiSpeichernUnterAsync(string vorschlagsname, string erweiterungOhnePunkt, string dateiTypAnzeige)
    {
        var picker = new FileSavePicker();
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(_fenster));
        picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
        picker.SuggestedFileName = Path.GetFileNameWithoutExtension(vorschlagsname);
        picker.FileTypeChoices.Add(dateiTypAnzeige, [$".{erweiterungOhnePunkt}"]);

        var datei = await picker.PickSaveFileAsync();
        return datei?.Path;
    }
}
