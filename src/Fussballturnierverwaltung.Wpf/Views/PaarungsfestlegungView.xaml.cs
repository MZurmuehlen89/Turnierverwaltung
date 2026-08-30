using Fussballturnierverwaltung.Wpf.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Fussballturnierverwaltung.Wpf.Views;

/// <summary>
/// Drag&amp;Drop-Mechanik für den Paarungs-Workspace (Issue #251) — vereinfacht gegenüber
/// AuslosungView.xaml.cs: kein Lostopf-Zwischenspeicher, daher wird direkt aus der "Verfügbar"-Liste
/// gezogen (Payload-Typ <see cref="TeilnehmerAnzeige"/> statt eines DragData-Wrappers mit Topf-Id).
/// </summary>
public partial class PaarungsfestlegungView : UserControl
{
    private Point _dragStartPoint;
    private TeilnehmerAnzeige? _pendingDrag;

    public PaarungsfestlegungView()
    {
        InitializeComponent();
    }

    private ManuellePaarungWorkspaceViewModel? Workspace => (DataContext as PaarungsfestlegungViewModel)?.Workspace;

    // ── Drag starten (aus "Verfügbar") ───────────────────────────────────────

    private void VerfuegbarListBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(null);
        _pendingDrag = HoleGezogenenTeilnehmer(e.OriginalSource);
    }

    private void VerfuegbarListBox_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _pendingDrag is null) return;

        var pos = e.GetPosition(null);
        var diff = _dragStartPoint - pos;
        if (Math.Abs(diff.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(diff.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        var data = new DataObject(typeof(TeilnehmerAnzeige), _pendingDrag);
        DragDrop.DoDragDrop((DependencyObject)sender, data, DragDropEffects.Move);
        _pendingDrag = null;
    }

    // ── Doppelklick auf Verfügbar-Item: in den nächsten freien Slot ──────────

    private void VerfuegbarListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        var vm = Workspace;
        if (vm is null) return;
        var teilnehmer = HoleGezogenenTeilnehmer(e.OriginalSource);
        if (teilnehmer is null) return;

        if (vm.IstKoModus)
        {
            var slot = vm.NaechsterFreierKoSlot();
            if (slot is null) return;
            vm.TeamInKoSlotPlatzieren(teilnehmer, slot);
        }
        else
        {
            var slot = vm.NaechsterFreierGruppenSlot();
            if (slot is null) return;
            vm.TeamInGruppenSlotPlatzieren(teilnehmer, slot);
        }
        e.Handled = true;
    }

    // ── Slot-Drop ─────────────────────────────────────────────────────────────

    private void ManuellerSlot_Drop(object sender, DragEventArgs e)
    {
        ResetSlotDragVisual(sender);
        if (e.Data.GetData(typeof(TeilnehmerAnzeige)) is not TeilnehmerAnzeige teilnehmer) return;
        var vm = Workspace;
        if (vm is null || sender is not FrameworkElement fe) return;

        if (fe.Tag is ManuellerKoSlot koSlot)
            vm.TeamInKoSlotPlatzieren(teilnehmer, koSlot);
        else if (fe.Tag is ManuellerGruppenSlot gruppenSlot)
            vm.TeamInGruppenSlotPlatzieren(teilnehmer, gruppenSlot);
        e.Handled = true;
    }

    private void ManuellerSlot_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(typeof(TeilnehmerAnzeige))
            ? DragDropEffects.Move
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void ManuellerSlot_DragEnter(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(typeof(TeilnehmerAnzeige))) return;
        if (sender is Border border)
        {
            border.BorderBrush = Brushes.CornflowerBlue;
            border.BorderThickness = new Thickness(2);
        }
    }

    private void ManuellerSlot_DragLeave(object sender, DragEventArgs e)
        => ResetSlotDragVisual(sender);

    private void ManuellerSlot_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement fe) return;
        var vm = Workspace;
        if (vm is null) return;

        var menu = new ContextMenu();
        if (fe.Tag is ManuellerKoSlot koSlot && !koSlot.IstLeer)
        {
            var item = new MenuItem { Header = $"'{koSlot.Team!.Name}' entfernen" };
            item.Click += (_, _) => vm.TeamAusKoSlotEntfernen(koSlot);
            menu.Items.Add(item);
            fe.ContextMenu = menu;
            fe.ContextMenu.IsOpen = true;
        }
        else if (fe.Tag is ManuellerGruppenSlot gruppenSlot && !gruppenSlot.IstLeer)
        {
            var item = new MenuItem { Header = $"'{gruppenSlot.Team!.Name}' entfernen" };
            item.Click += (_, _) => vm.TeamAusGruppenSlotEntfernen(gruppenSlot);
            menu.Items.Add(item);
            fe.ContextMenu = menu;
            fe.ContextMenu.IsOpen = true;
        }
        e.Handled = true;
    }

    private static void ResetSlotDragVisual(object sender)
    {
        if (sender is Border border)
        {
            border.ClearValue(Border.BorderBrushProperty);
            border.ClearValue(Border.BorderThicknessProperty);
        }
    }

    private static TeilnehmerAnzeige? HoleGezogenenTeilnehmer(object originalSource)
    {
        var element = originalSource as DependencyObject;
        while (element is not null)
        {
            if (element is FrameworkElement fe && fe.DataContext is TeilnehmerAnzeige t)
                return t;
            element = VisualTreeHelper.GetParent(element);
        }
        return null;
    }
}
