using Fussballturnierverwaltung.WinUi.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace Fussballturnierverwaltung.WinUi.Views;

public sealed partial class SpielplanUndBaumPage : UserControl
{
    private SpielplanUndBaumViewModel? _vm;

    public SpielplanUndBaumPage()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (_vm is not null) _vm.PropertyChanged -= Vm_PropertyChanged;
            _vm = DataContext as SpielplanUndBaumViewModel;
            if (_vm is not null) _vm.PropertyChanged += Vm_PropertyChanged;
            BaumBereichAnpassen();
        };
    }

    private void Vm_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SpielplanUndBaumViewModel.ZeigtBaumGesamtansicht))
            BaumBereichAnpassen();
    }

    // Issue #238: Gesamtübersicht über die ganze Breite, sonst feste 600px rechts neben dem Spielplan. Spalte und
    // Breite werden hier gesetzt, weil sich ColumnDefinition/Grid.Column in WinUI 3 nicht zuverlässig binden lassen.
    private void BaumBereichAnpassen()
    {
        bool gesamt = _vm?.ZeigtBaumGesamtansicht == true;
        Grid.SetColumn(BaumContainer, gesamt ? 0 : 2);
        Grid.SetColumnSpan(BaumContainer, gesamt ? 3 : 1);
        BaumContainer.Width = gesamt ? double.NaN : 600;
    }
}
