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
/// Ziel löst per Namens-Lookup gegen <see cref="Platzbelegung.VerfuegbarerTeilnehmer"/> auf) statt
/// eines reinen Platzhalter-Strings.</summary>
public sealed partial class VorbelegungPage : UserControl
{
    public VorbelegungPage()
    {
        InitializeComponent();
    }

    private VorbelegungViewModel? ViewModel => DataContext as VorbelegungViewModel;
    private Platzbelegung? Belegung => ViewModel?.Workspace?.Belegung;

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
        var belegung = Belegung;
        if (belegung is null || sender is not ListView lv || lv.SelectedItem is not TeilnehmerAnzeige team) return;

        if (belegung.NaechsterFreierPlatz() is not { } platz) return;
        belegung.Platzieren(team, platz);
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
        var belegung = Belegung;
        if (belegung is null) return;

        var name = await e.DataView.GetTextAsync();
        var team = belegung.VerfuegbarerTeilnehmer(name);
        if (team is null) return;

        if (fe.DataContext is VorbelegungsPlatz platz) belegung.Platzieren(team, platz);
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
        var belegung = Belegung;
        if (belegung is null || sender is not FrameworkElement fe) return;

        if (fe.DataContext is VorbelegungsPlatz { IstLeer: false } platz)
            ZeigeEntfernenFlyout(fe, platz.AnzeigeName, () => belegung.Entfernen(platz));
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
