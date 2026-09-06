using Fussballturnierverwaltung.WinUi.ViewModels;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using Windows.ApplicationModel.DataTransfer;

namespace Fussballturnierverwaltung.WinUi.Views;

/// <summary>Drag&amp;Drop-Mechanik für den "Vorbelegung"-Tab (Issue #297, Nachfolger von #277's
/// "Paarungen"-Tab) — analog WPF-Original <c>PaarungsfestlegungView.xaml.cs</c>, adaptiert auf die
/// WinUI3-Drag&amp;Drop-APIs (<see cref="DataPackage"/> statt <c>DataObject</c>). Anders als der entfallene
/// "Paarungen"-Tab wird hier ein <see cref="TeilnehmerAnzeige"/>-Objekt gezogen (Name als Text-Payload,
/// Ziel löst per Namens-Lookup gegen <see cref="VorbelegungWorkspace.VerfuegbareTeilnehmer"/> auf) statt
/// eines reinen Platzhalter-Strings.</summary>
public sealed partial class VorbelegungPage : UserControl
{
    public VorbelegungPage()
    {
        InitializeComponent();
    }

    private VorbelegungViewModel? ViewModel => DataContext as VorbelegungViewModel;
    private VorbelegungWorkspace? Workspace => ViewModel?.Workspace;

    // ── Drag starten (aus "Verfügbar") ───────────────────────────────────────

    private void VerfuegbarListView_DragItemsStarting(object sender, DragItemsStartingEventArgs e)
    {
        if (e.Items.Count == 0 || e.Items[0] is not TeilnehmerAnzeige team) return;
        e.Data.SetText(team.Name);
        e.Data.RequestedOperation = DataPackageOperation.Move;
    }

    // ── Doppeltipp auf Verfügbar-Item: in den nächsten freien Slot ───────────

    private void VerfuegbarListView_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        var workspace = Workspace;
        if (workspace is null || sender is not ListView lv || lv.SelectedItem is not TeilnehmerAnzeige team) return;

        if (workspace.IstKoModus)
        {
            var slot = workspace.NaechsterFreierKoSlot();
            if (slot is null) return;
            workspace.TeamInKoSlotPlatzieren(team, slot);
        }
        else
        {
            var slot = workspace.NaechsterFreierGruppenSlot();
            if (slot is null) return;
            workspace.TeamInGruppenSlotPlatzieren(team, slot);
        }
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
        // zurück, und falls in dieser Lücke die Slot-Collections neu aufgebaut werden (z.B. durch einen
        // Rundenwechsel), zeigt fe.DataContext danach auf ein verwaistes Slot-Objekt.
        if (sender is not FrameworkElement fe) return;
        var workspace = Workspace;
        if (workspace is null) return;

        var name = await e.DataView.GetTextAsync();
        var team = workspace.VerfuegbareTeilnehmer.FirstOrDefault(t => t.Name == name);
        if (team is null) return;

        switch (fe.DataContext)
        {
            case ManuellerKoSlot koSlot:
                workspace.TeamInKoSlotPlatzieren(team, koSlot);
                break;
            case ManuellerGruppenSlot gruppenSlot:
                workspace.TeamInGruppenSlotPlatzieren(team, gruppenSlot);
                break;
        }
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

    // ── Rechtsklick entfernt eine Zuweisung ──────────────────────────────────

    private void Slot_RightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        var workspace = Workspace;
        if (workspace is null || sender is not FrameworkElement fe) return;

        switch (fe.DataContext)
        {
            case ManuellerKoSlot { IstLeer: false } koSlot:
                ZeigeEntfernenFlyout(fe, koSlot.AnzeigeName, () => workspace.TeamAusKoSlotEntfernen(koSlot));
                break;
            case ManuellerGruppenSlot { IstLeer: false } gruppenSlot:
                ZeigeEntfernenFlyout(fe, gruppenSlot.AnzeigeName, () => workspace.TeamAusGruppenSlotEntfernen(gruppenSlot));
                break;
        }
    }

    private static void ZeigeEntfernenFlyout(FrameworkElement anker, string name, Action entfernen)
    {
        var flyout = new MenuFlyout();
        var item = new MenuFlyoutItem { Text = $"'{name}' entfernen" };
        item.Click += (_, _) => entfernen();
        flyout.Items.Add(item);
        flyout.ShowAt(anker);
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
