using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Wpf.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Fussballturnierverwaltung.Wpf.Views;

public partial class AuslosungView : UserControl
{
    // ── Drag-State ────────────────────────────────────────────────────────────

    private Point _dragStartPoint;
    private DragData? _pendingDrag;

    public AuslosungView()
    {
        InitializeComponent();
    }

    // ── Drag starten ──────────────────────────────────────────────────────────

    private void ListBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(null);
        _pendingDrag    = null;

        var mannschaft = HoleGezogenesMannschaft(e.OriginalSource);
        if (mannschaft is null) return;

        var listBox = (ListBox)sender;
        var topfVm  = listBox.DataContext as LostopfViewModel;
        _pendingDrag = new DragData(mannschaft.Id, topfVm?.Id);
    }

    private void ListBox_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _pendingDrag is null) return;

        var pos  = e.GetPosition(null);
        var diff = _dragStartPoint - pos;
        if (Math.Abs(diff.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(diff.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        var data = new DataObject(typeof(DragData), _pendingDrag);
        DragDrop.DoDragDrop((DependencyObject)sender, data, DragDropEffects.Move);
        _pendingDrag = null;
    }

    // ── Drop-Handler ──────────────────────────────────────────────────────────

    private async void VerfuegbarListBox_Drop(object sender, DragEventArgs e)
    {
        ResetDragVisual(sender);
        if (e.Data.GetData(typeof(DragData)) is not DragData data) return;
        if (!data.QuellTopfId.HasValue) return;  // bereits in Verfügbar, kein Effekt

        var vm = (AuslosungViewModel)DataContext;
        await vm.MannschaftZuVerfuegbarZurueck(data.MannschaftId, data.QuellTopfId.Value);
        e.Handled = true;
    }

    private async void TopfListBox_Drop(object sender, DragEventArgs e)
    {
        ResetDragVisual(sender);
        if (e.Data.GetData(typeof(DragData)) is not DragData data) return;

        var listBox = (ListBox)sender;
        if (listBox.DataContext is not LostopfViewModel ziel) return;
        if (ziel.Id == data.QuellTopfId) return;  // gleicher Topf → kein Effekt

        var vm = (AuslosungViewModel)DataContext;
        await vm.MannschaftZuTopfVerschieben(data.MannschaftId, data.QuellTopfId, ziel.Id);
        e.Handled = true;
    }

    // ── DragOver / Visuals ────────────────────────────────────────────────────

    private void ListBox_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(typeof(DragData))
            ? DragDropEffects.Move
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void VerfuegbarListBox_DragEnter(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(typeof(DragData))) return;
        var lb = (ListBox)sender;
        lb.BorderBrush     = Brushes.CornflowerBlue;
        lb.BorderThickness = new Thickness(2);
    }

    private void TopfListBox_DragEnter(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(typeof(DragData))) return;
        var lb = (ListBox)sender;
        lb.BorderBrush     = Brushes.CornflowerBlue;
        lb.BorderThickness = new Thickness(2);
    }

    private void ListBox_DragLeave(object sender, DragEventArgs e)
        => ResetDragVisual(sender);

    private static void ResetDragVisual(object sender)
    {
        if (sender is ListBox lb)
        {
            lb.ClearValue(ListBox.BorderBrushProperty);
            lb.ClearValue(ListBox.BorderThicknessProperty);
        }
    }

    // ── Hilfsmethoden ─────────────────────────────────────────────────────────

    private static Mannschaft? HoleGezogenesMannschaft(object originalSource)
    {
        var element = originalSource as DependencyObject;
        while (element is not null)
        {
            if (element is FrameworkElement fe && fe.DataContext is Mannschaft m)
                return m;
            element = VisualTreeHelper.GetParent(element);
        }
        return null;
    }

    // ── Drag-Daten ────────────────────────────────────────────────────────────

    private sealed record DragData(int MannschaftId, int? QuellTopfId);
}
