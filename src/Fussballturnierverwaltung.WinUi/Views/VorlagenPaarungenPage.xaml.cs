using Fussballturnierverwaltung.WinUi.ViewModels;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using Windows.ApplicationModel.DataTransfer;

namespace Fussballturnierverwaltung.WinUi.Views;

/// <summary>Drag&amp;Drop-Mechanik für den "Paarungen"-Tab (Issue #277-Nachbesserung nach Nutzer-Rückmeldung:
/// Zuweisung sollte wie im WPF-Original per Ziehen aus "Verfügbar" möglich sein) — analog WPF-Original
/// <c>PaarungsfestlegungView.xaml.cs</c>, adaptiert auf die WinUI3-Drag&amp;Drop-APIs (<see cref="DataPackage"/>
/// statt <c>DataObject</c>, Text-Payload statt typisiertem Objekt, da hier nur Platzhalter-Strings statt echter
/// Mannschaften gezogen werden). Das bestehende Klick-Formular (Spalte 4 der Page) bleibt als zusätzlicher,
/// barrierefreier Weg erhalten — analog WPF, das ebenfalls mehrere parallele Bedienwege (Drag&amp;Drop,
/// Doppelklick, Rechtsklick) für dieselbe Seite anbietet.</summary>
public sealed partial class VorlagenPaarungenPage : UserControl
{
    public VorlagenPaarungenPage()
    {
        InitializeComponent();
    }

    private VorlagenPaarungenViewModel? ViewModel => DataContext as VorlagenPaarungenViewModel;

    // ── Drag starten (aus "Verfügbar") ───────────────────────────────────────

    private void VerfuegbarListView_DragItemsStarting(object sender, DragItemsStartingEventArgs e)
    {
        if (e.Items.Count == 0 || e.Items[0] is not string platzhalter) return;
        e.Data.SetText(platzhalter);
        e.Data.RequestedOperation = DataPackageOperation.Move;
    }

    // ── Doppeltipp auf Verfügbar-Item: in den nächsten freien Slot ───────────

    private void VerfuegbarListView_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        var vm = ViewModel;
        if (vm is null || sender is not ListView lv || lv.SelectedItem is not string platzhalter) return;

        var slot = vm.NaechsterFreierSlot();
        if (slot is null) return;
        vm.PlatzhalterZuweisen(slot, platzhalter);
        e.Handled = true;
    }

    // ── Slot-Drop ─────────────────────────────────────────────────────────────

    private void Slot_DragOver(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.Text))
            e.AcceptedOperation = DataPackageOperation.Move;
    }

    private async void Slot_Drop(object sender, DragEventArgs e)
    {
        ResetSlotDragVisual(sender);
        if (!e.DataView.Contains(StandardDataFormats.Text)) return;

        // Ziel-Slot VOR dem await lesen, nicht danach: GetTextAsync gibt die Kontrolle an die Message-Loop
        // zurück, und falls in dieser Lücke die Slot-Collections neu aufgebaut werden (AktualisiereSlots,
        // z.B. durch einen Rundenwechsel), zeigt fe.DataContext danach auf ein verwaistes Slot-Objekt — die
        // Zuweisung liefe dann ins Leere (Nutzer-Rückmeldung: Drag&Drop blieb gelegentlich wirkungslos hängen).
        if (sender is not FrameworkElement fe || fe.DataContext is not VorlagenPaarungSlot slot) return;
        var vm = ViewModel;
        if (vm is null) return;

        var platzhalter = await e.DataView.GetTextAsync();
        vm.PlatzhalterZuweisen(slot, platzhalter);
    }

    private void Slot_DragEnter(object sender, DragEventArgs e)
    {
        if (!e.DataView.Contains(StandardDataFormats.Text)) return;
        if (sender is Border border)
        {
            border.BorderBrush = new SolidColorBrush(Colors.CornflowerBlue);
            border.BorderThickness = new Thickness(2);
        }
    }

    private void Slot_DragLeave(object sender, DragEventArgs e) => ResetSlotDragVisual(sender);

    // ── Klick wählt einen Slot fürs Formular; Rechtsklick entfernt eine Zuweisung ──

    private void Slot_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (ViewModel is { } vm && sender is FrameworkElement fe && fe.DataContext is VorlagenPaarungSlot slot)
            vm.AusgewaehlterSlot = slot;
    }

    private void Slot_RightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        var vm = ViewModel;
        if (vm is null || sender is not FrameworkElement fe || fe.DataContext is not VorlagenPaarungSlot slot || !slot.IstBesetzt) return;

        var flyout = new MenuFlyout();
        var item = new MenuFlyoutItem { Text = $"'{slot.PlatzhalterBezeichnung}' entfernen" };
        item.Click += (_, _) => vm.PlatzhalterAusSlotEntfernen(slot);
        flyout.Items.Add(item);
        flyout.ShowAt(fe);
    }

    private static void ResetSlotDragVisual(object sender)
    {
        if (sender is Border border)
        {
            border.ClearValue(Border.BorderBrushProperty);
            border.ClearValue(Border.BorderThicknessProperty);
        }
    }
}
