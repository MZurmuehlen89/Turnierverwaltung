using Fussballturnierverwaltung.WinUi.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace Fussballturnierverwaltung.WinUi.Views;

/// <summary>"Spielplan"-Teil des #301-Composite. ListView.SelectedItems ist nicht direkt bindbar —
/// Mehrfachauswahl (Issue #124) wird hier abgegriffen und ins ViewModel gespiegelt (Precedent:
/// Wpf/Views/SpielplanView.xaml.cs). Alle drei Listen (Gesamt/Fokus/Mannschaft) teilen sich denselben Handler.</summary>
public sealed partial class SpielplanView : UserControl
{
    public SpielplanView()
    {
        InitializeComponent();

        // handledEventsToo: ListView/ListViewItem bzw. TextBox können Escape/Enter bereits selbst als
        // behandelt markieren — ein XAML-KeyDown-Handler käme dann gar nicht mehr an (Issue #345).
        SpielListView.AddHandler(KeyDownEvent, new KeyEventHandler(SpielListView_KeyDown), true);
        FokusSpielListView.AddHandler(KeyDownEvent, new KeyEventHandler(SpielListView_KeyDown), true);
        MannschaftsSpielListView.AddHandler(KeyDownEvent, new KeyEventHandler(SpielListView_KeyDown), true);
        ErgebnisFormular.AddHandler(KeyDownEvent, new KeyEventHandler(ErgebnisFormular_KeyDown), true);

        // Strg+S speichert das Ergebnis-Formular (WPF-KeyBinding auf der SpielplanView, Issue #345).
        var speichern = new KeyboardAccelerator { Key = VirtualKey.S, Modifiers = VirtualKeyModifiers.Control };
        speichern.Invoked += Speichern_Invoked;
        KeyboardAccelerators.Add(speichern);

        DataContextChanged += (_, e) =>
        {
            if (_vm is not null) _vm.ErgebnisEingabeAngefordert -= ErgebnisEingabeAngefordert;
            _vm = e.NewValue as SpielplanViewModel;
            if (_vm is not null) _vm.ErgebnisEingabeAngefordert += ErgebnisEingabeAngefordert;
        };
    }

    private SpielplanViewModel? _vm;

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

        var zielListView = SichtbareListe(vm);
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

        SichtbareListe(vm).Focus(FocusState.Programmatic);
        vm.SpeichernBefehl.Execute(null);
    }

    // ── Kontextmenü (#62) ────────────────────────────────────────────────────

    // Rechtsklick (oder Menütaste) wählt die Zeile aus wie ein Linksklick — liegt sie schon in einer Mehrfachauswahl,
    // gilt das Menü der ganzen Auswahl. Wechselt die Auswahl bei ungespeicherter Eingabe, kommt erst die Rückfrage;
    // das Menü öffnet dann der nächste Rechtsklick (zwei Dialoge zugleich verträgt WinUI nicht).
    private void SpielListView_ContextRequested(UIElement sender, ContextRequestedEventArgs e)
    {
        if (sender is not ListView { DataContext: SpielplanViewModel vm } listView ||
            ZeileFinden(e.OriginalSource as DependencyObject, listView) is not { } zeile) return;
        e.Handled = true;

        if (!listView.SelectedItems.Contains(zeile))
        {
            bool rueckfrage = vm.HatUngespeicherteAenderungen;
            listView.SelectedItem = zeile;
            if (rueckfrage) return;
        }

        var menue = SpielKontextmenue.ZeilenMenue(vm, MannschaftsKontextmenue.ZielFinden<IMannschaftsAnzeige>(listView),
            zeile, listView.SelectedItems.OfType<SpielZeile>().ToList());
        if (e.TryGetPosition(listView, out var punkt))
            menue.ShowAt(listView, new Microsoft.UI.Xaml.Controls.Primitives.FlyoutShowOptions { Position = punkt });
        else if (listView.ContainerFromItem(zeile) is FrameworkElement container)
            menue.ShowAt(container);
    }

    private static SpielZeile? ZeileFinden(DependencyObject? element, ListView listView)
    {
        for (; element is not null && !ReferenceEquals(element, listView);
             element = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(element))
        {
            if (element is FrameworkElement { DataContext: SpielZeile zeile }) return zeile;
        }
        return null;
    }

    // „Ergebnis eingeben“ (Spielliste) bzw. „Im Spielplan zeigen“ (Turnierbaum): das Spiel in der Liste zeigen und den
    // Cursor ins erste Torfeld. Erst nach dem Schließen des Menüs — es gibt den Fokus beim Schließen an die Liste zurück.
    private void ErgebnisEingabeAngefordert(object? sender, EventArgs e)
    {
        if (_vm is not { AusgewaehlteSpiel: { } spiel } vm) return;
        SichtbareListe(vm).ScrollIntoView(spiel);
        DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
        {
            if (vm.FormAktiv) ErstesFormularFeld.Focus(FocusState.Programmatic);
        });
    }

    private ListView SichtbareListe(SpielplanViewModel vm) =>
        vm.IstMannschaftsansicht ? MannschaftsSpielListView
        : vm.ZeigeFokusListe ? FokusSpielListView
        : SpielListView;

    // ── Mannschaftsauswahl (#479) ────────────────────────────────────────────
    // Die Box fragt nur an; ob die Wahl gilt (Ungespeichert-Abfrage), entscheidet der Spielplan.

    // Fokus in die Liste: sonst klappt die Vorschlagsliste beim nächsten Tastendruck wieder auf.
    private void MannschaftsAuswahl_Bestaetigt(object? sender, EventArgs e)
    {
        if (DataContext is SpielplanViewModel vm) SichtbareListe(vm).Focus(FocusState.Programmatic);
    }
}
