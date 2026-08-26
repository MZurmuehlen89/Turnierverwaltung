using Fussballturnierverwaltung.Wpf.ViewModels;
using System.Linq;
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

    // Escape hebt die Einzelspiel-Selektion auf (Issue #145) — löst über SelectionChanged
    // automatisch AktualisiereMehrfachAuswahl aus und setzt AusgewaehlteSpiel (TwoWay-Binding)
    // auf null, wodurch der rechte Gruppen-Fokus (sekundäre Automatik aus #138) verschwindet,
    // ohne den unabhängigen, bewussten Gruppenfilter (AusgewaehlteGruppe) anzufassen.
    // Enter springt bei aktivem Formular direkt ins erste Formularfeld (Issue #63) — die Auswahl
    // selbst (und damit das Öffnen des Formulars) übernimmt bereits die bestehende
    // SelectedItem-Bindung bei Pfeiltasten-Navigation, Enter dient hier nur dem Fokuswechsel.
    private void SpielListView_KeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not ListView { DataContext: SpielplanViewModel vm } listView) return;

        switch (e.Key)
        {
            case Key.Escape:
                listView.UnselectAll();
                e.Handled = true;
                break;
            case Key.Enter when vm.FormAktiv:
                ErstesFormularFeld.Focus();
                e.Handled = true;
                break;
        }
    }

    // Escape schließt bei Fokus innerhalb des Ergebnis-Formulars ebenfalls das Formular (Issue #63)
    // — SpielListView_KeyDown greift nur, solange der Fokus noch in der Liste liegt. Spiegelt dieselbe
    // Deselektion+Fokusrückgabe wie dort, damit der Dirty-Check-Dialog aus RundeWechselAsync/
    // SpielWechselAsync konsistent bleibt (kein zweiter Schließ-Pfad mit abweichendem Verhalten).
    private void ErgebnisFormular_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;

        // SpielListView bzw. FokusSpielListView sind je nach IstGefiltertAufGruppe alternativ sichtbar.
        var zielListView = SpielListView.Visibility == System.Windows.Visibility.Visible ? SpielListView : FokusSpielListView;
        zielListView.UnselectAll();
        zielListView.Focus();
        e.Handled = true;
    }
}
