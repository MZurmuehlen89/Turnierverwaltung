using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Fussballturnierverwaltung.WinUi.Views;

/// <summary>Spaltenraster der Tabellen (#607): Platz, Team, Spiele, Tore, Tordifferenz, Vortrag, Punkte, Buchholz. Ein
/// <see cref="Grid"/> mit <c>views:TabellenSpalten.Anwenden="True"</c> bekommt diese Spalten — Kopfzeile,
/// Tabellenzeile und die Zeilen der noch offenen Plätze nutzen dieselben Breiten und laufen nicht auseinander.
/// Die Spalte „Vortrag“ (#355, Punkteübernahme) hat Breite 0, solange <c>views:TabellenSpalten.MitVortrag</c> nicht
/// gesetzt ist – Tabellen ohne Punkteübernahme verlieren dadurch keinen Platz.</summary>
public static class TabellenSpalten
{
    /// <summary>Index der Spalte „Vortrag“ im Raster.</summary>
    public const int VORTRAG_SPALTE = 5;

    private const double VORTRAG_BREITE = 44;

    private static readonly GridLength[] _breiten =
    [
        new(36), new(1, GridUnitType.Star), new(26), new(50), new(34), new(0), new(30), new(34),
    ];

    public static readonly DependencyProperty AnwendenProperty = DependencyProperty.RegisterAttached(
        "Anwenden", typeof(bool), typeof(TabellenSpalten), new PropertyMetadata(false, AnwendenGeaendert));

    public static readonly DependencyProperty MitVortragProperty = DependencyProperty.RegisterAttached(
        "MitVortrag", typeof(bool), typeof(TabellenSpalten), new PropertyMetadata(false, MitVortragGeaendert));

    public static bool GetAnwenden(DependencyObject element) => (bool)element.GetValue(AnwendenProperty);

    public static void SetAnwenden(DependencyObject element, bool wert) => element.SetValue(AnwendenProperty, wert);

    public static bool GetMitVortrag(DependencyObject element) => (bool)element.GetValue(MitVortragProperty);

    public static void SetMitVortrag(DependencyObject element, bool wert) => element.SetValue(MitVortragProperty, wert);

    private static void AnwendenGeaendert(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Grid raster || e.NewValue is not true) return;
        raster.ColumnDefinitions.Clear();
        foreach (var breite in _breiten)
            raster.ColumnDefinitions.Add(new ColumnDefinition { Width = breite });
        VortragBreiteSetzen(raster);
    }

    private static void MitVortragGeaendert(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is Grid raster) VortragBreiteSetzen(raster);
    }

    // Läuft nach Anwenden und nach MitVortrag – in beiden Reihenfolgen, in der WinUI die Eigenschaften setzt.
    private static void VortragBreiteSetzen(Grid raster)
    {
        if (raster.ColumnDefinitions.Count <= VORTRAG_SPALTE) return;
        raster.ColumnDefinitions[VORTRAG_SPALTE].Width = new GridLength(GetMitVortrag(raster) ? VORTRAG_BREITE : 0);
    }
}
