using Fussballturnierverwaltung.Application.Simulation;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace Fussballturnierverwaltung.WinUi.Converters;

/// <summary>Sichtbarkeit der Trenner-Darstellungen in der Zeitleiste (#689): Wert = die ganze Zeile, Parameter =
/// kommagetrennte <see cref="TrennerArt"/>-Namen. Sichtbar genau dann, wenn die Zeile ein Trenner ist und seine
/// Art in der Liste steht. WinUI3 kennt keinen DataTrigger, darum bekommt jede Darstellung (Linie, normale Pille,
/// gelbe Pille, eigenständige Marke) ihren eigenen Zustand; <c>IstTrenner</c> wird mitgeprüft, weil jede normale
/// Ereigniszeile als Art den Default <c>Abschnitt</c> trägt.</summary>
public class TrennerSichtbarkeitConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not EinzelspielEreignisZeile { IstTrenner: true } zeile || parameter is not string arten)
            return Visibility.Collapsed;
        return arten.Split(',').Contains(zeile.TrennerArt.ToString()) ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}
