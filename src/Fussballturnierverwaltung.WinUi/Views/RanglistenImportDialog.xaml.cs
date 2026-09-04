using Fussballturnierverwaltung.Application.Ranglisten;
using Fussballturnierverwaltung.Application.Ranglisten.Import;
using Fussballturnierverwaltung.Application.Stammdaten;
using Fussballturnierverwaltung.WinUi.Dialoge;
using Fussballturnierverwaltung.WinUi.ViewModels;
using Microsoft.UI.Xaml.Controls;
using System.ComponentModel;

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
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        Loaded += async (_, _) => await ViewModel.InitialisierenAsync();
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ViewModel.ImportAbgeschlossen) && ViewModel.ImportAbgeschlossen)
            Hide();
    }
}
