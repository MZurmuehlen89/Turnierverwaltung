using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Fussballturnierverwaltung.Wpf.Converters;

/// <summary>
/// Liefert GridLength(0) wenn nicht angepinnt, sonst die per ConverterParameter angegebene Breite
/// (z.B. "4" für eine GridSplitter-Spalte, "280" für die Panel-Spalte selbst). Kein ConvertBack
/// nötig — GridSplitter verändert ColumnDefinition.Width per SetCurrentValue, was die bestehende
/// Binding nicht auflöst; ein erneutes Umschalten von Angepinnt setzt die Breite wieder per Convert.
/// </summary>
[ValueConversion(typeof(bool), typeof(GridLength))]
public class BoolZuAngepinnterSpaltenbreiteConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true && parameter is string breiteText && double.TryParse(breiteText, NumberStyles.Float, CultureInfo.InvariantCulture, out var breite)
            ? new GridLength(breite)
            : new GridLength(0);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
