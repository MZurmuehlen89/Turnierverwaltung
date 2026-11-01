using Fussballturnierverwaltung.WinUi.ViewModels;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System.ComponentModel;
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
        DataContextChanged += (_, _) => ReiterAnbinden();
    }

    private AuslosungViewModel? ViewModel => DataContext as AuslosungViewModel;

    // ── Reiter Teilnehmerpool | Lostöpfe (#576) ──────────────────────────────
    // Im Code-Behind statt als Bindung: der SelectorBar wählt per SelectedItem, das ViewModel kennt nur „Pool offen“.

    private TeilnehmerpoolViewModel? _gebundenerPool;

    private void ReiterAnbinden()
    {
        if (_gebundenerPool is not null) _gebundenerPool.PropertyChanged -= BeiPoolAenderung;
        _gebundenerPool = ViewModel?.Teilnehmerpool;
        if (_gebundenerPool is not null) _gebundenerPool.PropertyChanged += BeiPoolAenderung;
        ReiterAnzeigen();
    }

    private void BeiPoolAenderung(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TeilnehmerpoolViewModel.PoolReiterAktiv)) ReiterAnzeigen();
    }

    private void ReiterAnzeigen()
    {
        var soll = _gebundenerPool?.PoolReiterAktiv == true ? ReiterTeilnehmerpool : ReiterLostoepfe;
        if (!ReferenceEquals(ReiterLeiste.SelectedItem, soll)) ReiterLeiste.SelectedItem = soll;
    }

    private void ReiterLeiste_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        if (_gebundenerPool is null || sender.SelectedItem is null) return;
        _gebundenerPool.PoolReiterAktiv = ReferenceEquals(sender.SelectedItem, ReiterTeilnehmerpool);
    }

    private void ZumTeilnehmerpool_Click(object sender, RoutedEventArgs e)
    {
        if (_gebundenerPool is not null) _gebundenerPool.PoolReiterAktiv = true;
    }

    // ── Gegner-Matrix: Team-Fokus per Klick ──────────────────────────────────
    // Im Code-Behind statt als Befehlsbindung je Zelle (#428): die Zellen sind KlickFlaechen ohne Command, und
    // eine ElementName-Bindung auf die Seite je Zelle entfällt.

    private void MatrixZelle_Ausgeloest(object? sender, KlickFlaecheAusgeloestEventArgs e) =>
        ViewModel?.Ergebnis.GegnerMatrix.TeamFokussierenCommand.Execute((sender as FrameworkElement)?.DataContext as GegnerMatrixZelle);

    private void MatrixTeam_Ausgeloest(object? sender, KlickFlaecheAusgeloestEventArgs e) =>
        ViewModel?.Ergebnis.GegnerMatrix.TeamFokussierenCommand.Execute(((sender as FrameworkElement)?.DataContext as GegnerMatrixZeile)?.Team);

    // Quelle des laufenden Drags (Precedent: Wpf AuslosungView-Feld _pendingDrag) — die tatsächlichen
    // Teilnehmer + Quell-Lostopf-Id kommen aus diesem Feld statt aus dem DataPackage-Text: Teams, die
    // bereits in einem Lostopf liegen, lassen sich per Namen nicht mehr über VerfuegbareTeilnehmer
    // auflösen (Nutzerfund: Drag&Drop von einem Lostopf in einen anderen tat nichts).
    private (IReadOnlyList<TeilnehmerAnzeige> Teilnehmer, int? QuellTopfId)? _pendingDrag;

    // ── Drag starten (aus "Verfügbar") ───────────────────────────────────────

    private void VerfuegbarListView_DragItemsStarting(object sender, DragItemsStartingEventArgs e)
    {
        var teams = e.Items.OfType<TeilnehmerAnzeige>().ToList();
        if (teams.Count == 0) return;
        _pendingDrag = (teams, null);
        e.Data.SetText(string.Join("\n", teams.Select(t => t.Name)));
        e.Data.RequestedOperation = DataPackageOperation.Move;
    }

    // ── Drag starten (aus einem Lostopf) ─────────────────────────────────────

    private void TopfListView_DragItemsStarting(object sender, DragItemsStartingEventArgs e)
    {
        if (sender is not ListView { DataContext: LostopfViewModel topf }) return;
        var teams = e.Items.OfType<TeilnehmerAnzeige>().ToList();
        if (teams.Count == 0) return;
        _pendingDrag = (teams, topf.Id);
        e.Data.SetText(string.Join("\n", teams.Select(t => t.Name)));
        e.Data.RequestedOperation = DataPackageOperation.Move;
    }

    // ── Doppeltipp auf Verfügbar-Item: in den ersten Lostopf ─────────────────

    private async void VerfuegbarListView_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        var viewModel = ViewModel;
        if (viewModel is null || sender is not ListView lv || lv.SelectedItem is not TeilnehmerAnzeige team) return;

        var ersterTopf = viewModel.Arbeitsbereich.Lostoepfe.OrderBy(l => l.Reihenfolge).FirstOrDefault();
        if (ersterTopf is null) return;

        await viewModel.Arbeitsbereich.TeilnehmerZuTopfHinzufuegenAsync(ersterTopf.Id, team);
        e.Handled = true;
    }

    // ── Rechtsklick auf "Verfügbar": Zu einem Lostopf hinzufügen ─────────────

    private void VerfuegbarListView_RightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        var viewModel = ViewModel;
        if (viewModel is null || sender is not ListView lv) return;
        if (e.OriginalSource is not FrameworkElement { DataContext: TeilnehmerAnzeige team }) return;

        var ausgewaehlt = ListViewAuswahl.Ziel(lv, team);
        var toepfe = viewModel.Arbeitsbereich.Lostoepfe.OrderBy(l => l.Reihenfolge).ToList();
        if (toepfe.Count == 0) return;

        var flyout = new MenuFlyout();
        foreach (var topf in toepfe)
        {
            var item = new MenuFlyoutItem { Text = $"Zu '{topf.Name}' hinzufügen" };
            item.Click += (_, _) => _ = viewModel.Arbeitsbereich.TeilnehmerZuTopfHinzufuegenAsync(topf.Id, ausgewaehlt);
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

        // Ziel-Topf VOR dem await lesen (siehe VorbelegungPage-Precedent): ein await gibt die Kontrolle an
        // die Message-Loop zurück, in der Lücke könnte ein Rundenwechsel die Collections neu aufbauen.
        if (sender is not ListView { DataContext: LostopfViewModel topf }) return;
        var viewModel = ViewModel;
        if (viewModel is null) return;

        var drag = _pendingDrag;
        _pendingDrag = null;
        if (drag is null || drag.Value.QuellTopfId == topf.Id) return;

        await viewModel.Arbeitsbereich.TeilnehmerZuTopfVerschiebenAsync(drag.Value.Teilnehmer, drag.Value.QuellTopfId, topf.Id);
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

        var ausgewaehlt = ListViewAuswahl.Ziel(lv, team);
        var beschreibung = ausgewaehlt.Count == 1 ? $"'{ausgewaehlt[0].Name}'" : $"{ausgewaehlt.Count} Teams";

        var flyout = new MenuFlyout();
        var item = new MenuFlyoutItem { Text = $"{beschreibung} entfernen" };
        item.Click += (_, _) => _ = viewModel.Arbeitsbereich.TeilnehmerAusTopfEntfernenAsync(topf.Id, ausgewaehlt.Select(t => t.EintragId).ToList());
        flyout.Items.Add(item);
        flyout.ShowAt((FrameworkElement)e.OriginalSource);
    }
}
