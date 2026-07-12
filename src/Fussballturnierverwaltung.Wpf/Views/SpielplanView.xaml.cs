using Fussballturnierverwaltung.Wpf.ViewModels;
using System.Linq;
using System.Windows.Controls;

namespace Fussballturnierverwaltung.Wpf.Views;

public partial class SpielplanView : UserControl
{
    public SpielplanView()
    {
        InitializeComponent();
    }

    // ListView.SelectedItems ist nicht direkt bindbar — Mehrfachauswahl (Issue #124) wird
    // hier abgegriffen und ins ViewModel gespiegelt, analog zu den Drag&Drop-Handlern in
    // AuslosungView.xaml.cs.
    private void SpielListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ListView { DataContext: SpielplanViewModel vm } listView)
            vm.AktualisiereMehrfachAuswahl(listView.SelectedItems.Cast<SpielZeile>());
    }
}
