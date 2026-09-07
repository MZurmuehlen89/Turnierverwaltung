using Fussballturnierverwaltung.WinUi.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace Fussballturnierverwaltung.WinUi.Views;

/// <summary>"Spielplan"-Teil des #301-Composite. ListView.SelectedItems ist nicht direkt bindbar —
/// Mehrfachauswahl (Issue #124) wird hier abgegriffen und ins ViewModel gespiegelt (Precedent:
/// Wpf/Views/SpielplanView.xaml.cs). Beide Listen (Gesamt/Fokus) teilen sich denselben Handler.</summary>
public sealed partial class SpielplanView : UserControl
{
    public SpielplanView()
    {
        InitializeComponent();
    }

    private void SpielListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ListView { DataContext: SpielplanViewModel vm } listView)
            vm.AktualisiereMehrfachAuswahl(listView.SelectedItems.OfType<SpielZeile>());
    }
}
