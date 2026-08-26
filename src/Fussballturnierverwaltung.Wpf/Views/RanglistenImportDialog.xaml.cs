using Fussballturnierverwaltung.Application.Ranglisten;
using Fussballturnierverwaltung.Application.Stammdaten;
using Fussballturnierverwaltung.Wpf.ViewModels;
using System.ComponentModel;
using System.Windows;

namespace Fussballturnierverwaltung.Wpf.Views;

public partial class RanglistenImportDialog : Window
{
    private readonly RanglistenQuelleService _quelleService;
    private readonly MannschaftService _mannschaftService;
    private readonly LandService _landService;

    public RanglistenImportDialogViewModel ViewModel { get; }

    public RanglistenImportDialog(
        RanglistenImportDialogViewModel viewModel,
        RanglistenQuelleService quelleService,
        MannschaftService mannschaftService,
        LandService landService)
    {
        InitializeComponent();
        ViewModel = viewModel;
        _quelleService = quelleService;
        _mannschaftService = mannschaftService;
        _landService = landService;
        DataContext = viewModel;
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        ViewModel.MannschaftNeuAnlegenAngefordert += ViewModel_MannschaftNeuAnlegenAngefordert;
        Loaded += async (_, _) => await ViewModel.InitialisierenAsync();
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ViewModel.ImportAbgeschlossen) && ViewModel.ImportAbgeschlossen)
            DialogResult = true;
    }

    private void ViewModel_MannschaftNeuAnlegenAngefordert(UnaufgeloesteZeileZuordnung zeile)
    {
        var dialog = new MannschaftSchnellAnlegenDialog(_mannschaftService, _landService, zeile.Quelle.RohName) { Owner = this };
        if (dialog.ShowDialog() == true && dialog.Erstellt is not null)
            ViewModel.MannschaftUebernehmen(zeile, dialog.Erstellt);
        else
            zeile.Ausgewaehlt = zeile.Optionen[0]; // zurück auf "— Überspringen —"
    }

    private async void QuellenVerwaltenButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new RanglistenQuellenVerwaltenDialog(_quelleService) { Owner = this };
        dialog.ShowDialog();
        if (dialog.Geaendert)
            await ViewModel.QuellenNeuLadenAsync();
    }

    private void SchliessenButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
