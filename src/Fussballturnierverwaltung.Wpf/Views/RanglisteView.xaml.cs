using Fussballturnierverwaltung.Wpf.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Fussballturnierverwaltung.Wpf.Views;

public partial class RanglisteView : UserControl
{
    private Point _dragStartPoint;

    public RanglisteView()
    {
        InitializeComponent();
    }

    // Drag&Drop-Umsortierung der Einträge-Zeilen (Rang ergibt sich aus der Position).

    private void EintraegeRow_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        => _dragStartPoint = e.GetPosition(null);

    private void EintraegeRow_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;

        var position = e.GetPosition(null);
        if (Math.Abs(position.X - _dragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(position.Y - _dragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        if (sender is DataGridRow { Item: RanglistenEintragZeile zeile } row)
            DragDrop.DoDragDrop(row, zeile, DragDropEffects.Move);
    }

    private void EintraegeRow_Drop(object sender, DragEventArgs e)
    {
        if (sender is not DataGridRow { Item: RanglistenEintragZeile zielZeile }) return;
        if (e.Data.GetData(typeof(RanglistenEintragZeile)) is not RanglistenEintragZeile quelleZeile) return;
        if (ReferenceEquals(quelleZeile, zielZeile)) return;

        if (DataContext is RanglisteViewModel vm)
            vm.EintragVerschieben(quelleZeile, zielZeile);
    }
}
