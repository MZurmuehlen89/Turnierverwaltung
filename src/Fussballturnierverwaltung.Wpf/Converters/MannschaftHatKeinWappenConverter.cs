using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Fussballturnierverwaltung.Wpf.Converters;

/// <summary>Spiegelbild von <see cref="MannschaftHatWappenConverter"/> - blendet das
/// Platzhalter-Icon nur ein, solange kein Wappen/keine Flagge hinterlegt ist.</summary>
[ValueConversion(typeof(Mannschaft), typeof(Visibility))]
public class MannschaftHatKeinWappenConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
        => MannschaftWappenErmittlung.Bytes(value as Mannschaft) is { Length: > 0 } ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
