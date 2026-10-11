using Fussballturnierverwaltung.Application.Stammdaten;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Microsoft.UI.Xaml.Data;

namespace Fussballturnierverwaltung.WinUi.Converters;

/// <summary>Wandelt eine <c>Mannschaft</c> direkt in ihre Icon-Bildquelle um — anders als eine direkte
/// <c>Mannschaft.Wappen</c>-Bindung (wie zuvor in der Gruppentabellen-Karte, Nutzerfund) berücksichtigt
/// <see cref="MannschaftsIcon.Fuer"/>, dass Nationalmannschaften die Landesflagge statt des
/// Wappens zeigen (Issue #230, im Spielplan über <c>SpielZeile.HeimWappen</c>/<c>AuswaertsWappen</c> bereits
/// korrekt, in der Gruppentabelle aber übersehen).</summary>
public class MannschaftZuWappenConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, string language)
        => BildDatenZuBildquelleConverter.BildAusBytes(MannschaftsIcon.Fuer(value as Mannschaft));

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}
