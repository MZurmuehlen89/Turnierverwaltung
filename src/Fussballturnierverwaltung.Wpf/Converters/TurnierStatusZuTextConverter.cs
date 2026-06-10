using Fussballturnierverwaltung.Domain.Enums;
using System.Globalization;
using System.Windows.Data;

namespace Fussballturnierverwaltung.Wpf.Converters;

public class TurnierStatusZuTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is TurnierStatus s ? StatusAnzeigeName(s) : value?.ToString() ?? string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;

    public static string StatusAnzeigeName(TurnierStatus s) => s switch
    {
        TurnierStatus.Geplant       => "Geplant",
        TurnierStatus.Auslosung     => "Auslosung",
        TurnierStatus.Laufend       => "Laufend",
        TurnierStatus.Abgeschlossen => "Abgeschlossen",
        TurnierStatus.Abgebrochen   => "Abgebrochen",
        _                           => s.ToString()
    };
}
