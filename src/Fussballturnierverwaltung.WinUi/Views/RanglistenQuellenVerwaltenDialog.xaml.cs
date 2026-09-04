using Fussballturnierverwaltung.Application.Ranglisten;
using Fussballturnierverwaltung.WinUi.Dialoge;
using Fussballturnierverwaltung.WinUi.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace Fussballturnierverwaltung.WinUi.Views;

public sealed partial class RanglistenQuellenVerwaltenDialog : ContentDialog
{
    public RanglistenQuellenVerwaltenViewModel ViewModel { get; }

    public RanglistenQuellenVerwaltenDialog(RanglistenQuelleService service, IDialogService dialogService)
    {
        InitializeComponent();
        ViewModel = new RanglistenQuellenVerwaltenViewModel(service, dialogService);
        DataContext = ViewModel;
        Loaded += async (_, _) => await ViewModel.LadenAsync();
    }
}
