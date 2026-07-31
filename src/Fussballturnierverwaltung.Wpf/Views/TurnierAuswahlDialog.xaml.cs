using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace Fussballturnierverwaltung.Wpf.Views;

public partial class TurnierAuswahlDialog : Window
{
    private readonly ICollectionView _ansicht;

    public TurnierInstanz? Ausgewaehlt { get; private set; }

    public TurnierAuswahlDialog(IEnumerable<TurnierInstanz> instanzen)
    {
        InitializeComponent();

        var liste = instanzen.ToList();
        _ansicht = CollectionViewSource.GetDefaultView(liste);
        _ansicht.Filter = PasstZurSuche;
        AuswahlGrid.ItemsSource = _ansicht;
    }

    private bool PasstZurSuche(object obj)
    {
        var begriff = SuchBox.Text;
        if (string.IsNullOrWhiteSpace(begriff)) return true;

        var instanz = (TurnierInstanz)obj;
        return (instanz.Name?.Contains(begriff, StringComparison.OrdinalIgnoreCase) ?? false) ||
               (instanz.SaisonOderJahr?.Contains(begriff, StringComparison.OrdinalIgnoreCase) ?? false) ||
               (instanz.TurnierVorlage?.Name?.Contains(begriff, StringComparison.OrdinalIgnoreCase) ?? false);
    }

    private void SuchBox_TextChanged(object sender, TextChangedEventArgs e) => _ansicht.Refresh();

    private void AuswahlGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (AuswahlGrid.SelectedItem is TurnierInstanz instanz)
            Uebernehmen(instanz);
    }

    private void UebernehmenButton_Click(object sender, RoutedEventArgs e)
    {
        if (AuswahlGrid.SelectedItem is TurnierInstanz instanz)
            Uebernehmen(instanz);
    }

    private void Uebernehmen(TurnierInstanz instanz)
    {
        Ausgewaehlt = instanz;
        DialogResult = true;
    }

    private void AbbrechenButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
