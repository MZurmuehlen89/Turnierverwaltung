using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Fussballturnierverwaltung.Wpf.Converters;

[ValueConversion(typeof(Mannschaft), typeof(ImageSource))]
public class MannschaftZuWappenBildquelleConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object parameter, CultureInfo culture)
        => BildDatenZuBildquelleConverter.BildAusBytes(MannschaftWappenErmittlung.Bytes(value as Mannschaft));

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
