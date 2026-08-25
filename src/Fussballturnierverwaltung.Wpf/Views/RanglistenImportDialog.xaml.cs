using Fussballturnierverwaltung.Application.Ranglisten;
using Fussballturnierverwaltung.Wpf.ViewModels;
using System.ComponentModel;
using System.Windows;

namespace Fussballturnierverwaltung.Wpf.Views;

public partial class RanglistenImportDialog : Window
{
    private readonly RanglistenQuelleService _quelleService;

    public RanglistenImportDialogViewModel ViewModel { get; }

    public RanglistenImportDialog(RanglistenImportDialogViewModel viewModel, RanglistenQuelleService quelleService)
    {
        InitializeComponent();
        ViewModel = viewModel;
        _quelleService = quelleService;
        DataContext = viewModel;
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        Loaded += async (_, _) => await ViewModel.InitialisierenAsync();
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ViewModel.ImportAbgeschlossen) && ViewModel.ImportAbgeschlossen)
            DialogResult = true;
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
