using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.WinUi.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace Fussballturnierverwaltung.WinUi.Views;

public sealed partial class AusVorlageErstellenDialog : ContentDialog
{
    public AusVorlageErstellenViewModel ViewModel { get; }

    public AusVorlageErstellenDialog(TurnierInstanzService instanzService, TurnierKlonService klonService)
    {
        InitializeComponent();
        ViewModel = new AusVorlageErstellenViewModel(instanzService, klonService);
        DataContext = ViewModel;
        Loaded += async (_, _) => await ViewModel.LadenAsync();
    }

    // Analog RanglistenImportDialog: Dialog bleibt bei Fehlern offen (z.B. Validierungsfehler aus
    // TurnierKlonService), daher Deferral statt einfachem Command-Binding auf den Primary-Button.
    private async void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        var deferral = args.GetDeferral();
        try
        {
            await ViewModel.ErstellenAsync();
            if (ViewModel.ErstellteInstanz is null)
                args.Cancel = true;
        }
        finally
        {
            deferral.Complete();
        }
    }
}
