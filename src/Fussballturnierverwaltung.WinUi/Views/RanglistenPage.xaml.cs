using Fussballturnierverwaltung.WinUi.ViewModels;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace Fussballturnierverwaltung.WinUi.Views;

public sealed partial class RanglistenPage : UserControl
{
    private RanglistenEintraegeViewModel? _eintragsliste;

    public RanglistenPage()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => EintragslisteVerbinden((DataContext as RanglistenViewModel)?.Eintragsliste);
    }

    private void EintragslisteVerbinden(RanglistenEintraegeViewModel? eintragsliste)
    {
        if (ReferenceEquals(eintragsliste, _eintragsliste)) return;
        if (_eintragsliste is not null) _eintragsliste.MarkierungAngefordert -= MarkierungAnwenden;
        _eintragsliste = eintragsliste;
        if (_eintragsliste is not null) _eintragsliste.MarkierungAngefordert += MarkierungAnwenden;
    }

    // Die Mehrfachauswahl der ListView (SelectedItems) lässt sich nicht binden — sie wird hier in beide Richtungen
    // übertragen (#477).
    private void EintraegeListe_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        _eintragsliste?.MarkierungSetzen(EintraegeListe.SelectedItems.OfType<RanglistenEintragZeile>());

    private void MarkierungAnwenden(object? sender, IReadOnlyList<RanglistenEintragZeile> zeilen)
    {
        EintraegeListe.SelectedItems.Clear();
        foreach (var zeile in zeilen)
            EintraegeListe.SelectedItems.Add(zeile);
        if (zeilen.Count > 0) EintraegeListe.ScrollIntoView(zeilen[0]);
    }

    private void Suche_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Enter) return;
        _eintragsliste?.WeiterSuchen();
        e.Handled = true;
    }
}
