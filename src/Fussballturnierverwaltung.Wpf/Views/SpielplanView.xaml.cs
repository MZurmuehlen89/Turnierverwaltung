using Fussballturnierverwaltung.Wpf.ViewModels;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

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

    // Öffnet das an den Button gehängte ContextMenu als Dropdown bei Linksklick (Issue #144) —
    // ContextMenu setzt PlacementTarget sonst nur bei Rechtsklick automatisch.
    private void AktionenDropdown_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { ContextMenu: { } menu } button) return;
        menu.PlacementTarget = button;
        menu.IsOpen = true;
    }

    // Escape hebt die Einzelspiel-Selektion auf (Issue #145) — löst über SelectionChanged
    // automatisch AktualisiereMehrfachAuswahl aus und setzt AusgewaehlteSpiel (TwoWay-Binding)
    // auf null, wodurch der rechte Gruppen-Fokus (sekundäre Automatik aus #138) verschwindet,
    // ohne den unabhängigen, bewussten Gruppenfilter (AusgewaehlteGruppe) anzufassen.
    private void SpielListView_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || sender is not ListView listView) return;
        listView.UnselectAll();
        e.Handled = true;
    }
}
