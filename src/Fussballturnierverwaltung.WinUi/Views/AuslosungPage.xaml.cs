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
/// (DataContext = <see cref="LostopfViewModel"/>) statt eines einzelnen Slot-Objekts. Beide ListViews nutzen
/// <c>SelectionMode="Extended"</c> — Mehrfachauswahl per Strg/Umschalt-Klick wird sowohl beim Drag (mehrere
/// Namen newline-getrennt im DataPackage) als auch beim Rechtsklick-Kontextmenü (wirkt auf die gesamte
/// aktuelle Auswahl, falls das rechtsgeklickte Element Teil davon ist, sonst nur auf dieses eine Element —
/// Windows-Explorer-Konvention) berücksichtigt (Nutzerfund: Mehrfachauswahl/Rechtsklick funktionierten
/// zunächst nicht, da nur <c>e.Items[0]</c> ausgewertet wurde).</summary>
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
        var teams = e.Items.OfType<TeilnehmerAnzeige>().ToList();
        if (teams.Count == 0) return;
        e.Data.SetText(string.Join("\n", teams.Select(t => t.Name)));
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

    // ── Rechtsklick auf "Verfügbar": Zu einem Lostopf hinzufügen ─────────────

    private void VerfuegbarListView_RightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        var viewModel = ViewModel;
        if (viewModel is null || sender is not ListView lv) return;
        if (e.OriginalSource is not FrameworkElement { DataContext: TeilnehmerAnzeige team }) return;

        var ausgewaehlt = ZielAuswahlErmitteln(lv, team);
        var toepfe = viewModel.Lostoepfe.OrderBy(l => l.Reihenfolge).ToList();
        if (toepfe.Count == 0) return;

        var flyout = new MenuFlyout();
        foreach (var topf in toepfe)
        {
            var item = new MenuFlyoutItem { Text = $"Zu '{topf.Name}' hinzufügen" };
            item.Click += (_, _) => _ = viewModel.TeilnehmerZuTopfHinzufuegenAsync(topf.Id, ausgewaehlt);
            flyout.Items.Add(item);
        }
        flyout.ShowAt((FrameworkElement)e.OriginalSource);
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

        var text = await e.DataView.GetTextAsync();
        var namen = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var teams = namen
            .Select(n => viewModel.VerfuegbareTeilnehmer.FirstOrDefault(t => t.Name == n))
            .Where(t => t is not null)
            .Select(t => t!)
            .ToList();
        if (teams.Count == 0) return;

        await viewModel.TeilnehmerZuTopfHinzufuegenAsync(topf.Id, teams);
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
        if (viewModel is null || sender is not ListView { DataContext: LostopfViewModel topf } lv) return;
        if (e.OriginalSource is not FrameworkElement { DataContext: TeilnehmerAnzeige team }) return;

        var ausgewaehlt = ZielAuswahlErmitteln(lv, team);
        var beschreibung = ausgewaehlt.Count == 1 ? $"'{ausgewaehlt[0].Name}'" : $"{ausgewaehlt.Count} Teams";

        var flyout = new MenuFlyout();
        var item = new MenuFlyoutItem { Text = $"{beschreibung} entfernen" };
        item.Click += (_, _) => _ = viewModel.TeilnehmerAusTopfEntfernenAsync(topf.Id, ausgewaehlt.Select(t => t.EintragId).ToList());
        flyout.Items.Add(item);
        flyout.ShowAt((FrameworkElement)e.OriginalSource);
    }

    /// <summary>Windows-Explorer-Konvention: ist das rechtsgeklickte Element Teil der aktuellen
    /// Mehrfachauswahl, wirkt die Aktion auf die gesamte Auswahl — sonst nur auf dieses eine Element.</summary>
    private static List<TeilnehmerAnzeige> ZielAuswahlErmitteln(ListView lv, TeilnehmerAnzeige rechtsgeklickt) =>
        lv.SelectedItems.Contains(rechtsgeklickt)
            ? lv.SelectedItems.OfType<TeilnehmerAnzeige>().ToList()
            : [rechtsgeklickt];
}
