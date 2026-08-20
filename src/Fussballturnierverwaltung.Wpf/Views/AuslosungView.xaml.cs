using Fussballturnierverwaltung.Wpf.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace Fussballturnierverwaltung.Wpf.Views;

public partial class AuslosungView : UserControl
{
    // ── Drag-State ────────────────────────────────────────────────────────────

    private Point _dragStartPoint;
    private DragData? _pendingDrag;

    // Issue #28: Klick auf ein bereits mehrfach markiertes Item darf die Selektion nicht sofort auf
    // das einzelne Item zurücksetzen (WPF-Standardverhalten bei MouseLeftButtonDown ohne Strg/Umschalt)
    // — sonst wäre ein Drag der gesamten Auswahl nie möglich. Der Kollaps auf das einzelne Item wird
    // in PreviewMouseLeftButtonUp nachgeholt, aber nur, wenn tatsächlich NICHT gezogen wurde.
    private bool _unterdrueckeSelektionsReset;

    public AuslosungView()
    {
        InitializeComponent();
    }

    // ── Drag starten ──────────────────────────────────────────────────────────

    private void ListBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(null);
        _pendingDrag    = null;
        _unterdrueckeSelektionsReset = false;

        var teilnehmer = HoleGezogenenTeilnehmer(e.OriginalSource);
        if (teilnehmer is null) return;

        var listBox = (ListBox)sender;
        var topfVm  = listBox.DataContext as LostopfViewModel;

        // Issue #28: ist das angeklickte Item Teil einer bestehenden Mehrfachauswahl, zieht der Drag
        // die gesamte Auswahl statt nur des einzelnen Items.
        bool teilVonMehrfachauswahl = listBox.SelectedItems.Count > 1 && listBox.SelectedItems.Contains(teilnehmer);
        IReadOnlyList<TeilnehmerAnzeige> gezogeneTeilnehmer = teilVonMehrfachauswahl
            ? listBox.SelectedItems.Cast<TeilnehmerAnzeige>().ToList()
            : [teilnehmer];

        _pendingDrag = new DragData(gezogeneTeilnehmer, topfVm?.Id);

        if (teilVonMehrfachauswahl && Keyboard.Modifiers is not (ModifierKeys.Control or ModifierKeys.Shift))
        {
            _unterdrueckeSelektionsReset = true;
            e.Handled = true;
        }
    }

    private void ListBox_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_unterdrueckeSelektionsReset) return;
        _unterdrueckeSelektionsReset = false;

        var teilnehmer = HoleGezogenenTeilnehmer(e.OriginalSource);
        if (teilnehmer is null) return;

        ((ListBox)sender).SelectedItem = teilnehmer;
    }

    private void ListBox_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _pendingDrag is null) return;

        var pos  = e.GetPosition(null);
        var diff = _dragStartPoint - pos;
        if (Math.Abs(diff.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(diff.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        _unterdrueckeSelektionsReset = false; // ein echter Drag hat begonnen — Klick-Selektion nicht nachholen
        var data = new DataObject(typeof(DragData), _pendingDrag);
        DragDrop.DoDragDrop((DependencyObject)sender, data, DragDropEffects.Move);
        _pendingDrag = null;
    }

    // ── Drop-Handler ──────────────────────────────────────────────────────────

    private async void VerfuegbarListBox_Drop(object sender, DragEventArgs e)
    {
        ResetDragVisual(sender);
        if (e.Data.GetData(typeof(DragData)) is not DragData data) return;
        if (!data.QuellTopfId.HasValue) return;

        var vm = (AuslosungViewModel)DataContext;
        await vm.TeilnehmerZuVerfuegbarZurueck(data.Teilnehmer, data.QuellTopfId.Value);
        e.Handled = true;
    }

    private async void TopfListBox_Drop(object sender, DragEventArgs e)
    {
        ResetDragVisual(sender);
        if (e.Data.GetData(typeof(DragData)) is not DragData data) return;

        var listBox = (ListBox)sender;
        if (listBox.DataContext is not LostopfViewModel ziel) return;
        if (ziel.Id == data.QuellTopfId) return;

        var vm = (AuslosungViewModel)DataContext;
        await vm.TeilnehmerZuTopfVerschieben(data.Teilnehmer, data.QuellTopfId, ziel.Id);
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

    // ── Fix #4: Doppelklick auf Verfügbar-Liste ───────────────────────────────

    private async void VerfuegbarListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        var vm = (AuslosungViewModel)DataContext;
        if (vm.AusgewaehlterVerfuegbarTeilnehmer is null) return;

        var lostoepfe = vm.Lostoepfe;
        if (!lostoepfe.Any()) return;

        if (lostoepfe.Count == 1)
        {
            await vm.TeilnehmerZuTopfHinzufuegenCommand.ExecuteAsync(lostoepfe[0].Id);
        }
        else
        {
            // Show popup to pick which pot
            ZeigePotAuswahlPopup(vm, vm.AusgewaehlterVerfuegbarTeilnehmer, (ListBox)sender);
        }
    }

    // ── Fix #4: Rechtsklick auf Verfügbar-Item ────────────────────────────────

    private void VerfuegbarItem_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ListBoxItem item) return;
        if (item.DataContext is not TeilnehmerAnzeige teilnehmer) return;

        var vm = (AuslosungViewModel)DataContext;
        if (!vm.Lostoepfe.Any()) return;

        // Issue #28: bei aktiver Mehrfachauswahl gilt das Kontextmenü für alle markierten Teams.
        var listBox = FindParent<ListBox>(item);
        bool mehrfach = listBox is not null && listBox.SelectedItems.Count > 1 && listBox.SelectedItems.Contains(teilnehmer);
        IReadOnlyList<TeilnehmerAnzeige> ausgewaehlt = mehrfach
            ? listBox!.SelectedItems.Cast<TeilnehmerAnzeige>().ToList()
            : [teilnehmer];
        string bezeichnung = mehrfach ? $"{ausgewaehlt.Count} Teams" : $"'{teilnehmer.Name}'";

        var menu = new ContextMenu();
        var header = new MenuItem { Header = $"{bezeichnung} in Topf:", IsEnabled = false };
        menu.Items.Add(header);
        menu.Items.Add(new Separator());

        foreach (var topf in vm.Lostoepfe)
        {
            var topfCopy = topf;
            var menuItem = new MenuItem { Header = topfCopy.Name };
            menuItem.Click += async (_, _) => await vm.TeilnehmerZuTopfVerschieben(ausgewaehlt, null, topfCopy.Id);
            menu.Items.Add(menuItem);
        }

        item.ContextMenu = menu;
        item.ContextMenu.IsOpen = true;
        e.Handled = true;
    }

    // ── Rechtsklick auf Item im Topf ──────────────────────────────────────────

    private void TopfItem_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ListBoxItem item) return;
        if (item.DataContext is not TeilnehmerAnzeige teilnehmer) return;

        // Find which pot this item belongs to
        var listBox = FindParent<ListBox>(item);
        if (listBox?.DataContext is not LostopfViewModel quellTopf) return;

        var vm = (AuslosungViewModel)DataContext;

        // Issue #28: bei aktiver Mehrfachauswahl gilt das Kontextmenü für alle markierten Teams.
        bool mehrfach = listBox.SelectedItems.Count > 1 && listBox.SelectedItems.Contains(teilnehmer);
        IReadOnlyList<TeilnehmerAnzeige> ausgewaehlt = mehrfach
            ? listBox.SelectedItems.Cast<TeilnehmerAnzeige>().ToList()
            : [teilnehmer];

        var menu = new ContextMenu();

        var header = new MenuItem { Header = mehrfach ? $"{ausgewaehlt.Count} Teams" : $"'{teilnehmer.Name}'", IsEnabled = false };
        menu.Items.Add(header);
        menu.Items.Add(new Separator());

        var entfernenHeader = mehrfach ? $"← Alle ausgewählten ({ausgewaehlt.Count}) zurück zu Verfügbar" : "← Zurück zu Verfügbar";
        var entfernen = new MenuItem { Header = entfernenHeader };
        entfernen.Click += async (_, _) =>
            await vm.TeilnehmerZuVerfuegbarZurueck(ausgewaehlt, quellTopf.Id);
        menu.Items.Add(entfernen);

        var anderen = vm.Lostoepfe.Where(l => l.Id != quellTopf.Id).ToList();
        if (anderen.Any())
        {
            menu.Items.Add(new Separator());
            foreach (var ziel in anderen)
            {
                var zielCopy = ziel;
                var verschiebenHeader = mehrfach
                    ? $"→ Alle ausgewählten ({ausgewaehlt.Count}) in {zielCopy.Name}"
                    : $"→ {zielCopy.Name}";
                var verschieben = new MenuItem { Header = verschiebenHeader };
                verschieben.Click += async (_, _) =>
                    await vm.TeilnehmerZuTopfVerschieben(ausgewaehlt, quellTopf.Id, zielCopy.Id);
                menu.Items.Add(verschieben);
            }
        }

        item.ContextMenu = menu;
        item.ContextMenu.IsOpen = true;
        e.Handled = true;
    }

    // ── Manuelle Auslosung: Doppelklick aus Lostopf ──────────────────────────

    private void TopfListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        var vm = (AuslosungViewModel)DataContext;
        if (!vm.IstManuellerModus) return;

        var listBox   = (ListBox)sender;
        var teilnehmer = HoleGezogenenTeilnehmer(e.OriginalSource);
        if (teilnehmer is null) return;

        var topfVm = listBox.DataContext as LostopfViewModel;
        if (topfVm is null) return;

        string? fehler;
        if (vm.IstManuellKoModus)
        {
            var slot = vm.NaechsterFreierKoSlot();
            if (slot is null) return;
            fehler = vm.TeamInKoSlotPlatzieren(teilnehmer, topfVm.Id, slot);
        }
        else
        {
            var slot = vm.NaechsterFreierGruppenSlot();
            if (slot is null) return;
            fehler = vm.TeamInGruppenSlotPlatzieren(teilnehmer, topfVm.Id, slot);
        }

        e.Handled = true;
    }

    // ── Manuelle Auslosung: Slot-Drop ─────────────────────────────────────────

    private void ManuellerSlot_Drop(object sender, DragEventArgs e)
    {
        ResetSlotDragVisual(sender);
        if (e.Data.GetData(typeof(DragData)) is not DragData data) return;
        if (!data.QuellTopfId.HasValue) return;
        // Issue #28 bewusst außen vor: ein Slot nimmt genau ein Team auf, Mehrfachauswahl ergibt hier
        // keinen Sinn — ein Drag aus einer Mehrfachauswahl heraus wird auf dem Slot ignoriert.
        if (data.Teilnehmer.Count != 1) return;

        var vm = (AuslosungViewModel)DataContext;
        if (sender is FrameworkElement fe)
        {
            var teilnehmer = data.Teilnehmer[0];
            if (fe.Tag is ManuellerKoSlot koSlot)
                vm.TeamInKoSlotPlatzieren(teilnehmer, data.QuellTopfId.Value, koSlot);
            else if (fe.Tag is ManuellerGruppenSlot gruppenSlot)
                vm.TeamInGruppenSlotPlatzieren(teilnehmer, data.QuellTopfId.Value, gruppenSlot);
        }
        e.Handled = true;
    }

    private void ManuellerSlot_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(typeof(DragData))
            ? DragDropEffects.Move
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void ManuellerSlot_DragEnter(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(typeof(DragData))) return;
        if (sender is Border border)
        {
            border.BorderBrush     = Brushes.CornflowerBlue;
            border.BorderThickness = new Thickness(2);
        }
    }

    private void ManuellerSlot_DragLeave(object sender, DragEventArgs e)
        => ResetSlotDragVisual(sender);

    private void ManuellerSlot_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement fe) return;
        var vm = (AuslosungViewModel)DataContext;

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

    // ── Hilfsmethoden ─────────────────────────────────────────────────────────

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

    private static T? FindParent<T>(DependencyObject child) where T : DependencyObject
    {
        var parent = VisualTreeHelper.GetParent(child);
        while (parent is not null)
        {
            if (parent is T result) return result;
            parent = VisualTreeHelper.GetParent(parent);
        }
        return null;
    }

    private static void ZeigePotAuswahlPopup(AuslosungViewModel vm, TeilnehmerAnzeige teilnehmer, UIElement target)
    {
        var popup = new Popup
        {
            PlacementTarget = target,
            Placement       = PlacementMode.MousePoint,
            StaysOpen       = false,
            AllowsTransparency = true
        };

        var panel = new StackPanel
        {
            Background = Brushes.White,
            MinWidth   = 140
        };
        panel.Children.Add(new TextBlock
        {
            Text       = "In Topf verschieben:",
            FontSize   = 11,
            FontWeight = FontWeights.Bold,
            Foreground = Brushes.Gray,
            Margin     = new Thickness(8, 6, 8, 4)
        });

        foreach (var topf in vm.Lostoepfe)
        {
            var topfCopy = topf;
            var btn = new Button
            {
                Content         = topf.Name,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Padding         = new Thickness(8, 5, 8, 5),
                BorderThickness = new Thickness(0),
                Background      = Brushes.Transparent,
                FontSize        = 13,
                Cursor          = Cursors.Hand
            };
            btn.Click += async (_, _) =>
            {
                popup.IsOpen = false;
                vm.AusgewaehlterVerfuegbarTeilnehmer = teilnehmer;
                await vm.TeilnehmerZuTopfHinzufuegenCommand.ExecuteAsync(topfCopy.Id);
            };
            panel.Children.Add(btn);
        }

        var border = new Border
        {
            Child           = panel,
            BorderBrush     = Brushes.LightGray,
            BorderThickness = new Thickness(1),
            CornerRadius    = new CornerRadius(4)
        };
        popup.Child  = border;
        popup.IsOpen = true;
    }

    // ── Drag-Daten ────────────────────────────────────────────────────────────

    private sealed record DragData(IReadOnlyList<TeilnehmerAnzeige> Teilnehmer, int? QuellTopfId);
}
