using Fussballturnierverwaltung.Application.Ranglisten;
using Fussballturnierverwaltung.Application.Ranglisten.Import;
using Fussballturnierverwaltung.Application.Stammdaten;
using Fussballturnierverwaltung.WinUi.Dialoge;
using Fussballturnierverwaltung.WinUi.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace Fussballturnierverwaltung.WinUi.Views;

public sealed partial class RanglistenImportDialog : ContentDialog
{
    public RanglistenImportViewModel ViewModel { get; }

    public bool ImportAbgeschlossen => ViewModel.ImportAbgeschlossen;

    public RanglistenImportDialog(
        RanglistenImportService importService,
        RanglistenQuelleService quelleService,
        RanglisteService ranglisteService,
        MannschaftService mannschaftService,
        LandService landService,
        IDialogService dialogService)
    {
        InitializeComponent();
        ViewModel = new RanglistenImportViewModel(importService, quelleService, ranglisteService, mannschaftService, landService, dialogService);
        DataContext = ViewModel;
        Loaded += async (_, _) => await ViewModel.InitialisierenAsync();
    }

    // Der native Primary-Button (statt einem eigenen Content-Button) übernimmt Klick UND Enter-Taste,
    // muss den Dialog aber offen halten, solange der Import nicht erfolgreich war (z.B. fehlende
    // Ziel-Rangliste) — daher Deferral statt einfachem Command-Binding.
    private async void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        var deferral = args.GetDeferral();
        try
        {
            await ViewModel.ImportierenCommand.ExecuteAsync(null);
            if (!ViewModel.ImportAbgeschlossen)
                args.Cancel = true;
        }
        finally
        {
            deferral.Complete();
        }
    }
}
