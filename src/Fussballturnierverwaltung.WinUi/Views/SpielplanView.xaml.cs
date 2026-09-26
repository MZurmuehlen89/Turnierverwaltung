using Fussballturnierverwaltung.WinUi.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace Fussballturnierverwaltung.WinUi.Views;

/// <summary>"Spielplan"-Teil des #301-Composite. ListView.SelectedItems ist nicht direkt bindbar —
/// Mehrfachauswahl (Issue #124) wird hier abgegriffen und ins ViewModel gespiegelt (Precedent:
/// Wpf/Views/SpielplanView.xaml.cs). Beide Listen (Gesamt/Fokus) teilen sich denselben Handler.</summary>
public sealed partial class SpielplanView : UserControl
{
    public SpielplanView()
    {
        InitializeComponent();

        // handledEventsToo: ListView/ListViewItem bzw. TextBox können Escape/Enter bereits selbst als
        // behandelt markieren — ein XAML-KeyDown-Handler käme dann gar nicht mehr an (Issue #345).
        SpielListView.AddHandler(KeyDownEvent, new KeyEventHandler(SpielListView_KeyDown), true);
        FokusSpielListView.AddHandler(KeyDownEvent, new KeyEventHandler(SpielListView_KeyDown), true);
        ErgebnisFormular.AddHandler(KeyDownEvent, new KeyEventHandler(ErgebnisFormular_KeyDown), true);

        // Strg+S speichert das Ergebnis-Formular (WPF-KeyBinding auf der SpielplanView, Issue #345).
        var speichern = new KeyboardAccelerator { Key = VirtualKey.S, Modifiers = VirtualKeyModifiers.Control };
        speichern.Invoked += Speichern_Invoked;
        KeyboardAccelerators.Add(speichern);
    }

    private void SpielListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ListView { DataContext: SpielplanViewModel vm } listView)
            vm.AktualisiereMehrfachAuswahl(listView.SelectedItems.OfType<SpielZeile>());
    }

    // Escape hebt die Einzelspiel-Selektion auf (Issue #145) — die TwoWay-Bindung setzt AusgewaehlteSpiel
    // auf null, wodurch Formular und automatischer Gruppen-Fokus verschwinden, ohne den bewusst gesetzten
    // Gruppenfilter (AusgewaehlteGruppe) anzufassen. Der Dirty-Check aus SpielWechselAsync greift dabei wie
    // bei jedem anderen Auswahlwechsel. Enter springt bei offenem Formular ins erste Formularfeld (Issue #63).
    private void SpielListView_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (sender is not ListView { DataContext: SpielplanViewModel vm } listView) return;

        switch (e.Key)
        {
            case VirtualKey.Escape:
                listView.SelectedItems.Clear();
                e.Handled = true;
                break;
            case VirtualKey.Enter when vm.FormAktiv:
                ErstesFormularFeld.Focus(FocusState.Keyboard);
                e.Handled = true;
                break;
        }
    }

    // Escape im Ergebnis-Formular schließt es über denselben Deselektions-Pfad wie in der Liste (Issue #63)
    // und gibt den Fokus an die gerade sichtbare Liste zurück (Gesamt- oder Fokus-Liste).
    private void ErgebnisFormular_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Escape || DataContext is not SpielplanViewModel vm) return;

        var zielListView = vm.ZeigeFokusListe ? FokusSpielListView : SpielListView;
        zielListView.SelectedItems.Clear();
        zielListView.Focus(FocusState.Keyboard);
        e.Handled = true;
    }

    // Fokus vor dem Speichern aus dem Formular in die sichtbare Liste holen (wie ein Klick auf den
    // Ribbon-Button): Speichern blendet das Formular kurz aus, und ein darin fokussiertes Textfeld lässt
    // WinUI den Fokus selbst weiterreichen — landet er dabei in der Rundenliste, löst das einen
    // Rundenwechsel samt Ungespeichert-Abfrage aus, obwohl gerade gespeichert wird.
    private void Speichern_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs e)
    {
        e.Handled = true;
        if (DataContext is not SpielplanViewModel vm || !vm.SpeichernBefehl.CanExecute(null)) return;

        (vm.ZeigeFokusListe ? FokusSpielListView : SpielListView).Focus(FocusState.Programmatic);
        vm.SpeichernBefehl.Execute(null);
    }
}
