using Fussballturnierverwaltung.WinUi.ViewModels;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;

namespace Fussballturnierverwaltung.WinUi.Views;

/// <summary>Drag&amp;Drop-Mechanik für den "Auslosung"-Tab (#300) — analog <see cref="VorbelegungPage"/>
/// (#297), adaptiert auf Lostöpfe statt fester Slots: Ziel ist die gesamte Lostopf-<see cref="ListView"/>
/// (DataContext = <see cref="LostopfViewModel"/>) statt eines einzelnen Slot-Objekts.</summary>
public sealed partial class AuslosungPage : UserControl
{
    public AuslosungPage()
    {
        InitializeComponent();
    }

    private AuslosungViewModel? ViewModel => DataContext as AuslosungViewModel;

    // ── Drag starten (aus "Verfügbar") ───────────────────────────────────────

    private void VerfuegbarListView_DragItemsStarting(object sender, DragItemsStartingEventArgs e)
    {
        if (e.Items.Count == 0 || e.Items[0] is not TeilnehmerAnzeige team) return;
        e.Data.SetText(team.Name);
        e.Data.RequestedOperation = DataPackageOperation.Move;
    }

    // ── Doppeltipp auf Verfügbar-Item: in den ersten Lostopf ─────────────────

    private async void VerfuegbarListView_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        var viewModel = ViewModel;
        if (viewModel is null || sender is not ListView lv || lv.SelectedItem is not TeilnehmerAnzeige team) return;

        var ersterTopf = viewModel.Lostoepfe.OrderBy(l => l.Reihenfolge).FirstOrDefault();
        if (ersterTopf is null) return;

        await viewModel.TeilnehmerZuTopfHinzufuegenAsync(ersterTopf.Id, team);
        e.Handled = true;
    }

    // ── Lostopf-Drop ──────────────────────────────────────────────────────────

    private void TopfListView_DragOver(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.Text))
            e.AcceptedOperation = DataPackageOperation.Move;
    }

    private async void TopfListView_Drop(object sender, DragEventArgs e)
    {
        ResetDragVisual(sender);
        if (!e.DataView.Contains(StandardDataFormats.Text)) return;

        // Ziel-Topf VOR dem await lesen (siehe VorbelegungPage-Precedent): GetTextAsync gibt die Kontrolle
        // an die Message-Loop zurück, in der Lücke könnte ein Rundenwechsel die Collections neu aufbauen.
        if (sender is not ListView { DataContext: LostopfViewModel topf }) return;
        var viewModel = ViewModel;
        if (viewModel is null) return;

        var name = await e.DataView.GetTextAsync();
        var team = viewModel.VerfuegbareTeilnehmer.FirstOrDefault(t => t.Name == name);
        if (team is null) return;

        await viewModel.TeilnehmerZuTopfHinzufuegenAsync(topf.Id, team);
    }

    private void TopfListView_DragEnter(object sender, DragEventArgs e)
    {
        if (!e.DataView.Contains(StandardDataFormats.Text)) return;
        if (sender is ListView lv)
        {
            lv.BorderBrush = new SolidColorBrush(Colors.CornflowerBlue);
            lv.BorderThickness = new Thickness(2);
        }
    }

    private void TopfListView_DragLeave(object sender, DragEventArgs e) => ResetDragVisual(sender);

    private static void ResetDragVisual(object sender)
    {
        if (sender is ListView lv)
        {
            lv.ClearValue(Control.BorderBrushProperty);
            lv.ClearValue(Control.BorderThicknessProperty);
        }
    }

    // ── Rechtsklick auf ein Team in einem Lostopf: entfernen ─────────────────

    private void TopfListView_RightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        var viewModel = ViewModel;
        if (viewModel is null || sender is not ListView { DataContext: LostopfViewModel topf }) return;
        if (e.OriginalSource is not FrameworkElement { DataContext: TeilnehmerAnzeige team }) return;

        var flyout = new MenuFlyout();
        var item = new MenuFlyoutItem { Text = $"'{team.Name}' entfernen" };
        item.Click += (_, _) => _ = viewModel.TeilnehmerAusTopfEntfernenAsync(topf.Id, team.EintragId);
        flyout.Items.Add(item);
        flyout.ShowAt((FrameworkElement)e.OriginalSource);
    }
}
