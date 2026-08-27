using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Fussballturnierverwaltung.Wpf.Converters;

[ValueConversion(typeof(Mannschaft), typeof(Visibility))]
public class MannschaftHatWappenConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
        => MannschaftWappenErmittlung.Bytes(value as Mannschaft) is { Length: > 0 } ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
