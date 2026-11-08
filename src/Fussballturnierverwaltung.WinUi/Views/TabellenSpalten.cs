using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Fussballturnierverwaltung.WinUi.Views;

/// <summary>Spaltenraster der Tabellen (#607): Platz, Team, Spiele, Tore, Tordifferenz, Punkte, Buchholz. Ein
/// <see cref="Grid"/> mit <c>views:TabellenSpalten.Anwenden="True"</c> bekommt diese Spalten — Kopfzeile,
/// Tabellenzeile und die Zeilen der noch offenen Plätze nutzen dieselben Breiten und laufen nicht auseinander.</summary>
public static class TabellenSpalten
{
    private static readonly GridLength[] _breiten =
    [
        new(36), new(1, GridUnitType.Star), new(26), new(50), new(34), new(30), new(34),
    ];

    public static readonly DependencyProperty AnwendenProperty = DependencyProperty.RegisterAttached(
        "Anwenden", typeof(bool), typeof(TabellenSpalten), new PropertyMetadata(false, AnwendenGeaendert));

    public static bool GetAnwenden(DependencyObject element) => (bool)element.GetValue(AnwendenProperty);

    public static void SetAnwenden(DependencyObject element, bool wert) => element.SetValue(AnwendenProperty, wert);

    private static void AnwendenGeaendert(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Grid raster || e.NewValue is not true) return;
        raster.ColumnDefinitions.Clear();
        foreach (var breite in _breiten)
            raster.ColumnDefinitions.Add(new ColumnDefinition { Width = breite });
    }
}
