using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Fussballturnierverwaltung.Wpf.Converters;

/// <summary>Spiegelbild von <see cref="NullZuGeklapptConverter"/> - für Platzhalter-Elemente,
/// die nur sichtbar sein sollen solange kein Wert (z. B. kein hochgeladenes Bild) vorhanden ist.</summary>
[ValueConversion(typeof(object), typeof(Visibility))]
public class NullZuSichtbarConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
        => value is null ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
