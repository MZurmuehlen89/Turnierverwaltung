using Fussballturnierverwaltung.Application.Stammdaten;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace Fussballturnierverwaltung.WinUi.Converters;

/// <summary>Visibility-Pendant zu <see cref="MannschaftZuWappenConverter"/> — prüft dieselbe Icon-Quelle
/// (Wappen oder, bei Nationalmannschaften, Landesflagge) statt direkt <c>Mannschaft.Wappen</c>.</summary>
public class MannschaftHatWappenConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => MannschaftsIcon.Fuer(value as Mannschaft) is { Length: > 0 }
            ? Visibility.Visible
            : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}
