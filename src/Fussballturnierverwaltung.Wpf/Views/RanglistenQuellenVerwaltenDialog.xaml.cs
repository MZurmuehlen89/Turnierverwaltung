using Fussballturnierverwaltung.Application.Ranglisten;
using Fussballturnierverwaltung.Domain.Entitaeten.Ranglisten;
using System.Collections.ObjectModel;
using System.Windows;

namespace Fussballturnierverwaltung.Wpf.Views;

public partial class RanglistenQuellenVerwaltenDialog : Window
{
    private readonly RanglistenQuelleService _service;

    public ObservableCollection<RanglistenQuelle> Quellen { get; } = [];

    /// <summary>Wird true, sobald sich der Quellenbestand geändert hat (Neu/Bearbeitet/Gelöscht) —
    /// der Aufrufer (Import-Dialog) muss dann seine Quellen-Auswahl neu laden.</summary>
    public bool Geaendert { get; private set; }

    public RanglistenQuellenVerwaltenDialog(RanglistenQuelleService service)
    {
        InitializeComponent();
        _service = service;
        DataContext = this;
        Loaded += async (_, _) => await LadenAsync();
    }

    private async Task LadenAsync()
    {
        var quellen = await _service.AlleAbrufenAsync();
        Quellen.Clear();
        foreach (var q in quellen) Quellen.Add(q);
    }

    private async void NeuButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new RanglistenQuelleDialog(_service) { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            Geaendert = true;
            await LadenAsync();
        }
    }

    private async void BearbeitenButton_Click(object sender, RoutedEventArgs e)
    {
        if (QuellenGrid.SelectedItem is not RanglistenQuelle quelle) return;

        var dialog = new RanglistenQuelleDialog(_service, quelle) { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            Geaendert = true;
            await LadenAsync();
        }
    }

    private async void LoeschenButton_Click(object sender, RoutedEventArgs e)
    {
        if (QuellenGrid.SelectedItem is not RanglistenQuelle quelle) return;

        var ergebnis = MessageBox.Show(
            $"'{quelle.Name}' wirklich löschen?", "Quelle löschen", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (ergebnis != MessageBoxResult.Yes) return;

        await _service.LoeschenAsync(quelle.Id);
        Geaendert = true;
        await LadenAsync();
    }

    private void SchliessenButton_Click(object sender, RoutedEventArgs e) => Close();
}
